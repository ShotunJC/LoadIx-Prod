[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repositoryRoot

try {
    dotnet tool restore
    dotnet ef database update `
        --project src/LoadIx.Infrastructure `
        --startup-project src/LoadIx.Api
    dotnet run `
        --project src/LoadIx.Api `
        --no-launch-profile `
        -- --environment Development --seed-development
}
finally {
    Pop-Location
}
