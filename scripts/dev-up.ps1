param(
    [bool] $ResetContainers = $true,
    [bool] $RemoveLocalDbVolume = $true,
    [bool] $RunTests = $false
)

. (Join-Path $PSScriptRoot "common.ps1")

& (Join-Path $PSScriptRoot "setup-dev.ps1") `
    -ResetContainers:$ResetContainers `
    -RemoveLocalDbVolume:$RemoveLocalDbVolume `
    -RunTests:$RunTests `
    -RunApi:$true
