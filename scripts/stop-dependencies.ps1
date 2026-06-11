param(
    [bool] $RemoveVolumes = $false
)

. (Join-Path $PSScriptRoot "common.ps1")

$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot "..")

Push-Location $repositoryRoot
try {
    $arguments = @("compose", "down")

    if ($RemoveVolumes) {
        $arguments += "-v"
    }

    Invoke-CheckedCommand "docker" $arguments
}
finally {
    Pop-Location
}
