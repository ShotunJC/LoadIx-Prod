[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$environmentFile = Join-Path $repositoryRoot '.env'
$exampleEnvironmentFile = Join-Path $repositoryRoot '.env.example'

if (-not (Test-Path -LiteralPath $environmentFile)) {
    Copy-Item -LiteralPath $exampleEnvironmentFile -Destination $environmentFile
    Write-Host 'Created .env from .env.example.'
}

docker compose --file (Join-Path $repositoryRoot 'compose.yaml') up --build --detach --wait
docker compose --file (Join-Path $repositoryRoot 'compose.yaml') ps

Write-Host 'LoadIx web:  http://localhost:5173'
Write-Host 'LoadIx API:  http://localhost:8080'
Write-Host 'OpenAPI:     http://localhost:8080/openapi/v1.json'
