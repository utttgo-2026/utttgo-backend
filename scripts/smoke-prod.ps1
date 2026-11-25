[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

function Get-RequiredEnvironmentValue {
    param([Parameter(Mandatory = $true)][string]$Name)

    $value = [Environment]::GetEnvironmentVariable($Name)
    if ([string]::IsNullOrWhiteSpace($value)) {
        exit 2
    }
    return $value
}

function Invoke-SmokeRequest {
    param(
        [Parameter(Mandatory = $true)][string]$Uri,
        [Parameter(Mandatory = $true)][ValidateSet('GET', 'POST', 'DELETE')][string]$Method,
        [hashtable]$Headers,
        [string]$JsonBody
    )

    $request = @{
        Uri = $Uri
        Method = $Method
        UseBasicParsing = $true
        ErrorAction = 'Stop'
    }
    if ($null -ne $Headers) {
        $request.Headers = $Headers
    }
    if ($null -ne $JsonBody) {
        $request.Body = $JsonBody
        $request.ContentType = 'application/json'
    }

    $statusCode = 0
    $content = ''
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
            catch {
                $content = ''
            }
        }
    }

    Write-Host $statusCode
    return [pscustomobject]@{ Code = $statusCode; Content = $content }
}

function Get-JsonObject {
    param([Parameter(Mandatory = $true)][string]$Content)

    try {
        return $Content | ConvertFrom-Json
    }
    catch {
        exit 1
    }
}

$baseUrl = (Get-RequiredEnvironmentValue -Name 'SMOKE_BASE_URL').TrimEnd('/')
$buyerEmail = Get-RequiredEnvironmentValue -Name 'SMOKE_BUYER_EMAIL'
$buyerPassword = Get-RequiredEnvironmentValue -Name 'SMOKE_BUYER_PASSWORD'
$sellerEmail = Get-RequiredEnvironmentValue -Name 'SMOKE_SELLER_EMAIL'
$sellerPassword = Get-RequiredEnvironmentValue -Name 'SMOKE_SELLER_PASSWORD'
$deviceToken = Get-RequiredEnvironmentValue -Name 'SMOKE_FCM_DEVICE_TOKEN'
$installationId = Get-RequiredEnvironmentValue -Name 'SMOKE_INSTALLATION_ID'
$productId = [Environment]::GetEnvironmentVariable('SMOKE_PRODUCT_ID')
$platform = [Environment]::GetEnvironmentVariable('SMOKE_PLATFORM')
$appVersion = [Environment]::GetEnvironmentVariable('SMOKE_APP_VERSION')

if ($buyerEmail -eq $sellerEmail) {
    exit 2
}
if ([string]::IsNullOrWhiteSpace($platform)) {
    $platform = 'android'
}
if ([string]::IsNullOrWhiteSpace($appVersion)) {
    $appVersion = 'smoke'
}

$buyerLogin = Invoke-SmokeRequest -Uri "$baseUrl/api/v1/auth/login" -Method POST -JsonBody (@{
    email = $buyerEmail
    password = $buyerPassword
} | ConvertTo-Json -Compress)
if ($buyerLogin.Code -ne 200) {
    exit 1
}
$buyerAccessToken = [string](Get-JsonObject -Content $buyerLogin.Content).accessToken
if ([string]::IsNullOrWhiteSpace($buyerAccessToken)) {
    exit 1
}

$sellerLogin = Invoke-SmokeRequest -Uri "$baseUrl/api/v1/auth/login" -Method POST -JsonBody (@{
    email = $sellerEmail
    password = $sellerPassword
} | ConvertTo-Json -Compress)
if ($sellerLogin.Code -ne 200) {
    exit 1
}

$buyerHeaders = @{ Authorization = "Bearer $buyerAccessToken" }

$usersMe = Invoke-SmokeRequest -Uri "$baseUrl/api/v1/users/me" -Method GET -Headers $buyerHeaders
if ($usersMe.Code -ne 200) {
    exit 1
}

$catalog = Invoke-SmokeRequest -Uri "$baseUrl/api/v1/products?page=1&pageSize=1" -Method GET
if ($catalog.Code -ne 200) {
    exit 1
}
if ([string]::IsNullOrWhiteSpace($productId)) {
    $productId = [string](Get-JsonObject -Content $catalog.Content).items[0].id
}
if ([string]::IsNullOrWhiteSpace($productId)) {
    exit 1
}

$order = Invoke-SmokeRequest -Uri "$baseUrl/api/v1/orders" -Method POST -Headers $buyerHeaders -JsonBody (@{
    productId = $productId
    quantity = 1
} | ConvertTo-Json -Compress)
if ($order.Code -ne 201) {
    exit 1
}

$deviceRegistration = Invoke-SmokeRequest -Uri "$baseUrl/api/v1/notifications/device-tokens" -Method POST -Headers $buyerHeaders -JsonBody (@{
    token = $deviceToken
    installationId = $installationId
    platform = $platform
    appVersion = $appVersion
} | ConvertTo-Json -Compress)
if ($deviceRegistration.Code -ne 201) {
    exit 1
}
$deviceTokenId = [string](Get-JsonObject -Content $deviceRegistration.Content).deviceTokenId
if ([string]::IsNullOrWhiteSpace($deviceTokenId)) {
    exit 1
}

$deviceRemoval = Invoke-SmokeRequest -Uri "$baseUrl/api/v1/notifications/device-tokens/$deviceTokenId" -Method DELETE -Headers $buyerHeaders
if ($deviceRemoval.Code -ne 204) {
    exit 1
}

$logout = Invoke-SmokeRequest -Uri "$baseUrl/api/v1/auth/logout" -Method POST -Headers $buyerHeaders
if ($logout.Code -ne 204) {
    exit 1
}

$revokedUsersMe = Invoke-SmokeRequest -Uri "$baseUrl/api/v1/users/me" -Method GET -Headers $buyerHeaders
if ($revokedUsersMe.Code -ne 401) {
    exit 1
}
