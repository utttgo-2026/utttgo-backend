[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $BaseUrl,

    [Parameter(Mandatory = $true)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
    [string] $ImagePath,

    [string] $BearerToken = $env:UTTTGO_MEDIA_BEARER_TOKEN,

    [ValidateSet("Approved", "Rejected", "ProcessingFailed")]
    [string] $ExpectedStatus = "Approved"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($BearerToken)) {
    throw "Provide -BearerToken or UTTTGO_MEDIA_BEARER_TOKEN at runtime."
}

$resolvedPath = (Resolve-Path -LiteralPath $ImagePath).Path
$file = Get-Item -LiteralPath $resolvedPath
if ($file.Length -le 0 -or $file.Length -gt 4000000) {
    throw "The image must be between 1 and 4,000,000 bytes."
}

$mimeType = switch ($file.Extension.ToLowerInvariant()) {
    ".jpg" { "image/jpeg" }
    ".jpeg" { "image/jpeg" }
    ".png" { "image/png" }
    ".webp" { "image/webp" }
    default { throw "Unsupported image extension: $($file.Extension)" }
}

$root = $BaseUrl.TrimEnd("/")
$authHeaders = @{ Authorization = "Bearer $BearerToken" }
$intentBody = @{
    fileName = $file.Name
    mimeType = $mimeType
    size = [int64]$file.Length
} | ConvertTo-Json -Compress

Write-Host "Requesting upload intent..."
$intent = Invoke-RestMethod -Uri "$root/api/v1/media/upload-url" -Method Post `
    -Headers $authHeaders -ContentType "application/json" -Body $intentBody

if ([string]::IsNullOrWhiteSpace($intent.uploadUrl) -or
    [string]::IsNullOrWhiteSpace($intent.mediaAssetId)) {
    throw "The API did not return a usable upload intent."
}

if ([DateTime]::Parse($intent.expiresAt).ToUniversalTime() -le [DateTime]::UtcNow) {
    throw "The upload URL is already expired."
}

$uploadHeaders = @{}
if ($null -ne $intent.headers) {
    foreach ($property in $intent.headers.PSObject.Properties) {
        if ($property.Name -ne "Content-Type") {
            $uploadHeaders[$property.Name] = [string]$property.Value
        }
    }
}

Write-Host "Uploading $($file.Name) to the presigned URL..."
$upload = Invoke-WebRequest -Uri $intent.uploadUrl -Method Put -InFile $resolvedPath `
    -ContentType $mimeType -Headers $uploadHeaders
if ($upload.StatusCode -lt 200 -or $upload.StatusCode -ge 300) {
    throw "Presigned upload failed with HTTP $($upload.StatusCode)."
}

$confirmBody = @{ mediaAssetId = $intent.mediaAssetId } | ConvertTo-Json -Compress
Write-Host "Confirming asset $($intent.mediaAssetId)..."
$confirmation = Invoke-RestMethod -Uri "$root/api/v1/media/confirm" -Method Post `
    -Headers $authHeaders -ContentType "application/json" -Body $confirmBody

$status = [string]$confirmation.status
if ($status -ne $ExpectedStatus) {
    throw "Expected status '$ExpectedStatus', received '$status'."
}

$asset = Invoke-RestMethod -Uri "$root/api/v1/media/$($intent.mediaAssetId)" `
    -Method Get -Headers $authHeaders
if ([string]$asset.status -ne $ExpectedStatus) {
    throw "The follow-up GET returned '$($asset.status)' instead of '$ExpectedStatus'."
}

Write-Host "Media flow completed: $status ($($intent.mediaAssetId))."
