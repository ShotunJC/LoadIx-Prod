param(
    [string] $Password = $env:LOADIX_PERFORMANCE_SEED_PASSWORD
)

if ([string]::IsNullOrWhiteSpace($Password)) {
    throw "Provide a password of at least 12 characters through -Password or LOADIX_PERFORMANCE_SEED_PASSWORD."
}

$env:LOADIX_PERFORMANCE_SEED_PASSWORD = $Password
$seedEnvironment = $env:ASPNETCORE_ENVIRONMENT
$env:ASPNETCORE_ENVIRONMENT = 'Development'

try {
    dotnet run --project src/LoadIx.Api --no-launch-profile -- --seed-performance
}
finally {
    $env:ASPNETCORE_ENVIRONMENT = $seedEnvironment
}
