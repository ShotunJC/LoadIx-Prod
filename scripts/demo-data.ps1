param([switch] $Reset)

$previousEnvironment = $env:ASPNETCORE_ENVIRONMENT
$previousGuard = $env:LOADIX_DEMO_SEED_ENABLED
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:LOADIX_DEMO_SEED_ENABLED = 'true'

try {
    $command = if ($Reset) { '--reset-demo' } else { '--seed-demo' }
    dotnet run --project src/LoadIx.Api --no-launch-profile -- $command
    if ($LASTEXITCODE -ne 0) {
        throw "The LoadIx demo data command failed with exit code $LASTEXITCODE."
    }
}
finally {
    $env:ASPNETCORE_ENVIRONMENT = $previousEnvironment
    $env:LOADIX_DEMO_SEED_ENABLED = $previousGuard
}
