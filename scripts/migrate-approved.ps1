[CmdletBinding()]
param(
    [bool]$DryRun = $true,
    [string]$MongoUri = $env:MONGODB_URI,
    [string]$Database = $(if ($env:MONGODB_DATABASE) { $env:MONGODB_DATABASE } else { "utttgo" }),
    [string]$Bucket = $env:S3_BUCKET_IMAGES,
    [string]$Region = $(if ($env:AWS_REGION) { $env:AWS_REGION } else { "us-east-2" })
)

$ErrorActionPreference = "Stop"

function Invoke-MongoJson {
    param([Parameter(Mandatory)][string]$Expression)

    if ([string]::IsNullOrWhiteSpace($MongoUri)) { throw "MONGODB_URI is required." }
    $errorFile = [System.IO.Path]::GetTempFileName()
    try {
        $output = & mongosh --quiet $MongoUri --eval $Expression 2> $errorFile
        if ($LASTEXITCODE -ne 0) { throw "MongoDB query failed." }
        return ($output -join "`n")
    }
    finally {
        Remove-Item -LiteralPath $errorFile -Force -ErrorAction SilentlyContinue
    }
}

function Invoke-AwsJson {
    param([Parameter(Mandatory)][string[]]$Arguments)

    $errorFile = [System.IO.Path]::GetTempFileName()
    try {
        $output = & aws @Arguments 2> $errorFile
        if ($LASTEXITCODE -ne 0) { return $null }
        return ($output -join "`n") | ConvertFrom-Json
    }
    finally {
        Remove-Item -LiteralPath $errorFile -Force -ErrorAction SilentlyContinue
    }
}

if ([string]::IsNullOrWhiteSpace($Bucket)) { throw "S3_BUCKET_IMAGES is required." }
if ($Database -notmatch '^[A-Za-z0-9_-]+$') { throw "Database name is invalid." }

$query = @"
db = db.getSiblingDB('$Database');
const seen = new Set();
const result = [];
db.products.find({ mediaAssetId: { `$type: 'string' } }).forEach(product => {
  if (seen.has(product.mediaAssetId)) return;
  const asset = db.mediaAssets.findOne({ _id: ObjectId(product.mediaAssetId) });
  if (!asset || asset.status !== 'Approved' || typeof asset.objectKey !== 'string' ||
      !asset.objectKey.startsWith('staging/')) return;
  seen.add(product.mediaAssetId);
  result.push({
    id: product.mediaAssetId,
    owner: asset.ownerUserId,
    sourceKey: asset.objectKey,
    mimeType: asset.mimeType,
    size: asset.size,
    version: asset.version
  });
});
print(JSON.stringify(result));
"@

$records = @(Invoke-MongoJson $query | ConvertFrom-Json)
$found = $records.Count
$migrated = 0
$failed = 0
$already = 0

foreach ($record in $records) {
    $targetKey = "approved/{0}/{1}" -f $record.owner, $record.id
    $sourceHead = Invoke-AwsJson @("s3api", "head-object", "--bucket", $Bucket,
        "--key", $record.sourceKey, "--region", $Region, "--output", "json")
    if ($null -eq $sourceHead -or [long]$sourceHead.ContentLength -ne [long]$record.size -or
        $sourceHead.ContentType -ne $record.mimeType) {
        $failed++
        continue
    }

    $destinationHead = Invoke-AwsJson @("s3api", "head-object", "--bucket", $Bucket,
        "--key", $targetKey, "--region", $Region, "--output", "json")
    $destinationExists = $null -ne $destinationHead
    if ($destinationExists -and [long]$destinationHead.ContentLength -eq [long]$record.size -and
        $destinationHead.ContentType -eq $record.mimeType) {
        $already++
        if ($DryRun) { continue }
    }
    elseif ($destinationExists) {
        $failed++
        continue
    }

    if ($DryRun) { $migrated++; continue }

    $copyError = [System.IO.Path]::GetTempFileName()
    try {
        & aws s3api copy-object --copy-source "$Bucket/$($record.sourceKey)" --bucket $Bucket `
            --key $targetKey --metadata-directive REPLACE --content-type $record.mimeType `
            --region $Region --output json 2> $copyError | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "S3 copy failed." }

        $verify = Invoke-AwsJson @("s3api", "head-object", "--bucket", $Bucket,
            "--key", $targetKey, "--region", $Region, "--output", "json")
        if ($null -eq $verify -or [long]$verify.ContentLength -ne [long]$record.size -or
            $verify.ContentType -ne $record.mimeType) { throw "S3 destination verification failed." }

        $update = @"
db = db.getSiblingDB('$Database');
const result = db.mediaAssets.updateOne(
  { _id: ObjectId('$($record.id)'), ownerUserId: '$($record.owner)', version: $($record.version),
    status: 'Approved', objectKey: '$($record.sourceKey)' },
  { `$set: { objectKey: '$targetKey' }, `$inc: { version: 1 } });
print(result.modifiedCount);
"@
        $modified = [int](Invoke-MongoJson $update)
        if ($modified -ne 1) {
            if (-not $destinationExists) {
                & aws s3api delete-object --bucket $Bucket --key $targetKey --region $Region --output json 2> $copyError | Out-Null
            }
            throw "MongoDB CAS did not update the asset."
        }

        & aws s3api delete-object --bucket $Bucket --key $record.sourceKey --region $Region --output json 2> $copyError | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "S3 source cleanup failed." }
        $migrated++
    }
    catch {
        $failed++
    }
    finally {
        Remove-Item -LiteralPath $copyError -Force -ErrorAction SilentlyContinue
    }
}

Write-Output ("found={0} migrated={1} already={2} failed={3} dryRun={4}" -f
    $found, $migrated, $already, $failed, $DryRun)
if ($failed -gt 0) { exit 1 }
