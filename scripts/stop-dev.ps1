[CmdletBinding()]
param(
    [switch]$RemoveVolumes
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$arguments = @(
    'compose',
    '--file',
    (Join-Path $repositoryRoot 'compose.yaml'),
    'down'
)

if ($RemoveVolumes) {
    $arguments += '--volumes'
}

& docker $arguments
