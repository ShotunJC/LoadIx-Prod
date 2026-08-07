[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [Uri] $BaseUri
)

$ErrorActionPreference = 'Stop'
if ($BaseUri.Scheme -ne 'https') {
    throw 'Smoke tests require an HTTPS base URL.'
}
$base = $BaseUri.AbsoluteUri.TrimEnd('/')

function Invoke-CheckedRequest {
    param([Parameter(Mandatory = $true)][Uri] $Uri)
    return Invoke-WebRequest -Uri $Uri -UseBasicParsing
}

$web = Invoke-CheckedRequest ([Uri] "$base/")
if ($web.Headers['X-Content-Type-Options'] -ne 'nosniff') {
    throw 'The web shell did not return X-Content-Type-Options: nosniff.'
}
if ([string]::IsNullOrWhiteSpace($web.Headers['Strict-Transport-Security'])) {
    throw 'The web shell did not return Strict-Transport-Security.'
}

$live = Invoke-CheckedRequest ([Uri] "$base/health/live")
if ($live.StatusCode -ne 200) {
    throw "The API liveness probe returned $($live.StatusCode)."
}

$ready = Invoke-CheckedRequest ([Uri] "$base/health/ready")
if ($ready.StatusCode -ne 200) {
    throw "The API readiness probe returned $($ready.StatusCode)."
}

$system = Invoke-CheckedRequest ([Uri] "$base/api/v1/system/info")
$payload = $system.Content | ConvertFrom-Json
if ($payload.apiVersion -ne 'v1') {
    throw "The API reported an unexpected version: $($payload.apiVersion)."
}

$protected = Invoke-WebRequest `
    -Uri ([Uri] "$base/api/v1/edge-devices/?page=1&pageSize=1") `
    -UseBasicParsing `
    -SkipHttpErrorCheck
if ($protected.StatusCode -ne 401) {
    throw "The protected route returned $($protected.StatusCode), expected 401."
}

Write-Output "LoadIx smoke tests passed for $base"
