[CmdletBinding()]
param()

$Profile = "default"
$Region = "us-east-2"
$Bucket = $env:S3_BUCKET_IMAGES

# This script is intentionally opt-in: it performs real S3 writes and deletes.
if (-not (Get-Command aws -ErrorAction SilentlyContinue) -or
    [string]::IsNullOrWhiteSpace($Bucket)) {
    exit 1
}

$runId = [guid]::NewGuid().ToString("N")
$sourceKey = "approved-test/$runId/source.txt"
$copyKey = "approved-test/$runId/copy.txt"
$payloadPath = Join-Path $env:TEMP "utttgo-conditional-write-$runId.txt"
$temporaryFiles = New-Object System.Collections.Generic.List[string]
$createdKeys = New-Object System.Collections.Generic.List[string]
$results = New-Object System.Collections.Generic.List[object]
$failed = $false

Set-Content -LiteralPath $payloadPath -Value "utttgo-conditional-write" -NoNewline -Encoding ASCII
$temporaryFiles.Add($payloadPath)

function Invoke-S3Request {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [switch]$CaptureBody
    )

    $debugPath = [System.IO.Path]::GetTempFileName()
    $outputPath = [System.IO.Path]::GetTempFileName()
    $temporaryFiles.Add($debugPath)
    $temporaryFiles.Add($outputPath)
    try {
        & aws @Arguments --no-cli-pager --debug 1> $outputPath 2> $debugPath
        $exitCode = $LASTEXITCODE
        $debug = Get-Content -LiteralPath $debugPath -Raw -ErrorAction SilentlyContinue
        $matches = [regex]::Matches($debug, 'HTTP/1\.1[" ]+([0-9]{3})')
        $httpCode = $null
        if ($matches.Count -gt 0) {
            $httpCode = $matches[$matches.Count - 1].Groups[1].Value
        }

        $body = $null
        if ($CaptureBody) {
            $body = (Get-Content -LiteralPath $outputPath -Raw -ErrorAction SilentlyContinue).Trim()
        }

        return [pscustomobject]@{
            ExitCode = $exitCode
            HttpCode = $httpCode
            Body = $body
        }
    }
    finally {
        Remove-Item -LiteralPath $debugPath, $outputPath -Force -ErrorAction SilentlyContinue
    }
}

function Add-Result {
    param(
        [string]$Case,
        [string]$HttpCode,
        [string]$Expected,
        [string]$Observed,
        [bool]$Passed
    )
    $results.Add([pscustomobject]@{
        Case = $Case
        HttpCode = $HttpCode
        Expected = $Expected
        Observed = $Observed
        Result = if ($Passed) { "PASS" } else { "FAIL" }
    })
}

function Invoke-SdkConditionalCopy {
    param(
        [string]$SourceKey,
        [string]$DestinationKey
    )

    $sdkRoot = Join-Path $env:TEMP "utttgo-copy-condition-$runId"
    $projectPath = Join-Path $sdkRoot "CopyConditionCheck.csproj"
    $programPath = Join-Path $sdkRoot "Program.cs"
    $outputPath = Join-Path $sdkRoot "run.log"
    $errorPath = Join-Path $sdkRoot "run-error.log"
    New-Item -ItemType Directory -Path $sdkRoot -Force | Out-Null
    $temporaryFiles.Add($sdkRoot)
    Set-Content -LiteralPath $projectPath -Encoding UTF8 -Value @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="AWSSDK.S3" Version="4.1.1" />
  </ItemGroup>
</Project>
"@
    Set-Content -LiteralPath $programPath -Encoding UTF8 -Value @"
using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Amazon.S3;
using Amazon.S3.Model;

var bucket = args[0];
var sourceKey = args[1];
var destinationKey = args[2];
var region = RegionEndpoint.GetBySystemName(args[3]);
var chain = new CredentialProfileStoreChain();
if (!chain.TryGetAWSCredentials("default", out var credentials))
    throw new InvalidOperationException("default profile unavailable");
using var client = new AmazonS3Client(credentials, region);
var request = new CopyObjectRequest
{
    SourceBucket = bucket,
    SourceKey = sourceKey,
    DestinationBucket = bucket,
    DestinationKey = destinationKey,
    IfNoneMatch = "*"
};

try
{
    var first = await client.CopyObjectAsync(request);
    Console.WriteLine($"COPY1:{(int)first.HttpStatusCode}");
}
catch (AmazonS3Exception exception)
{
    Console.WriteLine($"COPY1:{(int)exception.StatusCode}");
}

try
{
    var second = await client.CopyObjectAsync(request);
    Console.WriteLine($"COPY2:{(int)second.HttpStatusCode}");
}
catch (AmazonS3Exception exception)
{
    Console.WriteLine($"COPY2:{(int)exception.StatusCode}");
}
"@

    & dotnet run --project $projectPath --no-launch-profile -- $Bucket $SourceKey $DestinationKey $Region `
        1> $outputPath 2> $errorPath
    $sdkOutput = (Get-Content -LiteralPath $outputPath -Raw -ErrorAction SilentlyContinue) +
        (Get-Content -LiteralPath $errorPath -Raw -ErrorAction SilentlyContinue)
    $copy1 = [regex]::Match($sdkOutput, 'COPY1:([0-9]{3})')
    $copy2 = [regex]::Match($sdkOutput, 'COPY2:([0-9]{3})')
    return [pscustomobject]@{
        ExitCode = $LASTEXITCODE
        Copy1 = if ($copy1.Success) { $copy1.Groups[1].Value } else { "N/A" }
        Copy2 = if ($copy2.Success) { $copy2.Groups[1].Value } else { "N/A" }
    }
}

try {
    $missingKey = "approved-test/$runId/missing.txt"
    $missingHead = Invoke-S3Request @(
        "s3api", "head-object", "--profile", $Profile, "--region", $Region,
        "--bucket", $Bucket, "--key", $missingKey
    )
    $missingObserved = if ($missingHead.HttpCode -in @("403", "404")) {
        "missing object"
    }
    else {
        "unexpected response"
    }
    Add-Result "HeadObject missing" $missingHead.HttpCode "403 or 404" $missingObserved `
        ($missingHead.HttpCode -in @("403", "404"))
    if ($missingHead.HttpCode -notin @("403", "404")) { $failed = $true }

    $put = Invoke-S3Request @(
        "s3api", "put-object", "--profile", $Profile, "--region", $Region,
        "--bucket", $Bucket, "--key", $sourceKey, "--body", $payloadPath,
        "--content-type", "text/plain", "--if-none-match", "*"
    )
    Add-Result "PutObject If-None-Match=*" $put.HttpCode "200" "object created" ($put.HttpCode -eq "200")
    if ($put.HttpCode -ne "200") {
        $failed = $true
    }
    else {
        $createdKeys.Add($sourceKey)
    }

    $secondPut = Invoke-S3Request @(
        "s3api", "put-object", "--profile", $Profile, "--region", $Region,
        "--bucket", $Bucket, "--key", $sourceKey, "--body", $payloadPath,
        "--content-type", "text/plain", "--if-none-match", "*"
    )
    Add-Result "PutObject repeated" $secondPut.HttpCode "412" "existing object protected" ($secondPut.HttpCode -eq "412")
    if ($secondPut.HttpCode -ne "412") {
        $failed = $true
    }

    $head = Invoke-S3Request @(
        "s3api", "head-object", "--profile", $Profile, "--region", $Region,
        "--bucket", $Bucket, "--key", $sourceKey
    )
    Add-Result "HeadObject source" $head.HttpCode "200" "object exists" ($head.HttpCode -eq "200")
    if ($head.HttpCode -ne "200") {
        $failed = $true
    }
    else {
        $copy = Invoke-SdkConditionalCopy $sourceKey $copyKey
        Add-Result "CopyObject destination condition (first)" $copy.Copy1 "200" `
            "destination created conditionally" ($copy.Copy1 -eq "200")
        Add-Result "CopyObject destination condition (second)" $copy.Copy2 "412" `
            "existing destination protected" ($copy.Copy2 -eq "412")
        if ($copy.Copy1 -eq "200") {
            $createdKeys.Add($copyKey)
        }
        if ($copy.Copy1 -ne "200" -or $copy.Copy2 -ne "412") {
            $failed = $true
        }
    }
}
finally {
    foreach ($key in $createdKeys) {
        $delete = Invoke-S3Request @(
            "s3api", "delete-object", "--profile", $Profile, "--region", $Region,
            "--bucket", $Bucket, "--key", $key
        )
        $label = if ($key -eq $sourceKey) { "source" } else { "copy" }
        Add-Result "DeleteObject $label" $delete.HttpCode "204" "object deleted" ($delete.HttpCode -eq "204")
        if ($delete.HttpCode -ne "204") {
            $failed = $true
        }
    }

    foreach ($key in $createdKeys) {
        $afterDelete = Invoke-S3Request @(
            "s3api", "head-object", "--profile", $Profile, "--region", $Region,
            "--bucket", $Bucket, "--key", $key
        )
        $label = if ($key -eq $sourceKey) { "source" } else { "copy" }
        Add-Result "HeadObject after cleanup $label" $afterDelete.HttpCode "403 or 404" `
            "object absent" ($afterDelete.HttpCode -in @("403", "404"))
        if ($afterDelete.HttpCode -notin @("403", "404")) { $failed = $true }
    }

    foreach ($file in $temporaryFiles) {
        Remove-Item -LiteralPath $file -Force -Recurse -ErrorAction SilentlyContinue
    }
}

$results | Format-Table -AutoSize | Out-String | Write-Output
if ($failed) {
    exit 1
}
