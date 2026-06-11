. (Join-Path $PSScriptRoot "common.ps1")

$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot "..")

Push-Location $repositoryRoot
try {
    Invoke-CheckedCommand "docker" @("compose", "up", "-d", "--wait")
}
finally {
    Pop-Location
}
