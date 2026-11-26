[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

function Get-RequiredEnvironmentValue {
    param([Parameter(Mandatory = $true)][string]$Name)

    $value = [Environment]::GetEnvironmentVariable($Name)
    if ([string]::IsNullOrWhiteSpace($value)) { exit 2 }
    return $value
}

function Invoke-SmokeRequest {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Uri,
        [Parameter(Mandatory = $true)][ValidateSet('GET', 'POST')][string]$Method,
        [hashtable]$Headers,
        [string]$JsonBody
    )

    $request = @{
        Uri = $Uri
        Method = $Method
        UseBasicParsing = $true
        ErrorAction = 'Stop'
    }
    if ($null -ne $Headers) { $request.Headers = $Headers }
    if ($null -ne $JsonBody) {
        $request.Body = $JsonBody
        $request.ContentType = 'application/json'
    }

    $statusCode = 0
    $content = ''
    $timer = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $response = Invoke-WebRequest @request
        $statusCode = [int]$response.StatusCode
        $content = [string]$response.Content
    }
    catch {
        $response = $_.Exception.Response
        if ($null -ne $response) {
            $statusCode = [int]$response.StatusCode
            try {
                $reader = New-Object System.IO.StreamReader($response.GetResponseStream())
                $content = $reader.ReadToEnd()
                $reader.Dispose()
            }
            catch { $content = '' }
        }
    }
    finally { $timer.Stop() }

    # Only the probe name, HTTP status and elapsed time are emitted.
    Write-Host ('{0} {1} {2}ms' -f $Name, $statusCode, $timer.ElapsedMilliseconds)
    return [pscustomobject]@{ Code = $statusCode; Content = $content }
}

function Get-JsonObject {
    param([Parameter(Mandatory = $true)][string]$Content)
    try { return $Content | ConvertFrom-Json }
    catch { exit 1 }
}

function Assert-Status {
    param(
        [Parameter(Mandatory = $true)]$Response,
        [Parameter(Mandatory = $true)][int[]]$Allowed
    )
    if ($Allowed -notcontains $Response.Code) { exit 1 }
}

$baseUrl = (Get-RequiredEnvironmentValue -Name 'SMOKE_BASE_URL').TrimEnd('/')
$email = Get-RequiredEnvironmentValue -Name 'SMOKE_EMAIL'
$password = Get-RequiredEnvironmentValue -Name 'SMOKE_PASSWORD'
$deviceTokensPath = [Environment]::GetEnvironmentVariable('SMOKE_DEVICE_TOKENS_PATH')
if ([string]::IsNullOrWhiteSpace($deviceTokensPath)) {
    $deviceTokensPath = '/api/v1/notifications/device-tokens'
}

$health = Invoke-SmokeRequest -Name 'health' -Uri "$baseUrl/health" -Method GET
Assert-Status -Response $health -Allowed @(200)

$login = Invoke-SmokeRequest -Name 'login' -Uri "$baseUrl/api/v1/auth/login" -Method POST -JsonBody (@{
    email = $email
    password = $password
} | ConvertTo-Json -Compress)
Assert-Status -Response $login -Allowed @(200)
$accessToken = [string](Get-JsonObject -Content $login.Content).accessToken
if ([string]::IsNullOrWhiteSpace($accessToken)) { exit 1 }
$headers = @{ Authorization = "Bearer $accessToken" }

$me = Invoke-SmokeRequest -Name 'me' -Uri "$baseUrl/api/v1/users/me" -Method GET -Headers $headers
Assert-Status -Response $me -Allowed @(200)

$catalog = Invoke-SmokeRequest -Name 'catalog' -Uri "$baseUrl/api/v1/products?page=1&pageSize=1" -Method GET
Assert-Status -Response $catalog -Allowed @(200)

# The current API exposes device-token registration/removal, not a read listing.
# GET is intentionally used so this probe cannot create, alter or delete data.
$deviceTokens = Invoke-SmokeRequest -Name 'device-tokens' -Uri "$baseUrl$deviceTokensPath" -Method GET -Headers $headers
Assert-Status -Response $deviceTokens -Allowed @(200, 404, 405)
