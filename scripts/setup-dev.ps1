param(
    [bool] $ResetContainers = $true,
    [bool] $RemoveLocalDbVolume = $true,
    [bool] $RunTests = $false,
    [bool] $RunApi = $false,
    [string] $TestResultsDirectory = "TestResults"
)

. (Join-Path $PSScriptRoot "common.ps1")

$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$postgresImage = $env:LIANSUREPROTECT_POSTGRES_IMAGE

if ([string]::IsNullOrWhiteSpace($postgresImage)) {
    $postgresImage = "pgvector/pgvector:0.8.2-pg16-trixie"
}

if ($RemoveLocalDbVolume -and -not $ResetContainers) {
    throw "RemoveLocalDbVolume requires ResetContainers because Docker volumes cannot be removed while the Compose stack is left running."
}

if ($RemoveLocalDbVolume) {
    Assert-SubmissionMigrationsExist $repositoryRoot
}

if ($ResetContainers) {
    & (Join-Path $PSScriptRoot "stop-dependencies.ps1") -RemoveVolumes:$RemoveLocalDbVolume
}

if (-not (Test-DockerImageExists $postgresImage)) {
    Invoke-CheckedCommand "docker" @("pull", $postgresImage)
}

& (Join-Path $PSScriptRoot "start-dependencies.ps1")

Push-Location $repositoryRoot
try {
    Invoke-CheckedCommand "dotnet" @("restore", "LIAnsureProtect.slnx")
    Invoke-CheckedCommand "dotnet" @("build", "LIAnsureProtect.slnx", "--no-restore")
}
finally {
    Pop-Location
}

if ($RemoveLocalDbVolume) {
    & (Join-Path $PSScriptRoot "update-database.ps1")
}

if ($RunTests) {
    Push-Location $repositoryRoot
    $previousPostgreSqlTestFlag = $env:LIANSUREPROTECT_RUN_POSTGRES_TESTS
    $previousPostgreSqlTestConnectionString = $env:LIANSUREPROTECT_TEST_POSTGRES_CONNECTION_STRING

    try {
        $env:LIANSUREPROTECT_RUN_POSTGRES_TESTS = "true"
        $env:LIANSUREPROTECT_TEST_POSTGRES_CONNECTION_STRING = "Host=localhost;Port=5432;Database=liansureprotect;Username=postgres;Password=postgres"
        Invoke-CheckedCommand "dotnet" @("test", "LIAnsureProtect.slnx", "--no-build", "--logger", "trx", "--results-directory", $TestResultsDirectory)
    }
    finally {
        if ($null -eq $previousPostgreSqlTestFlag) {
            Remove-Item Env:\LIANSUREPROTECT_RUN_POSTGRES_TESTS -ErrorAction SilentlyContinue
        }
        else {
            $env:LIANSUREPROTECT_RUN_POSTGRES_TESTS = $previousPostgreSqlTestFlag
        }

        if ($null -eq $previousPostgreSqlTestConnectionString) {
            Remove-Item Env:\LIANSUREPROTECT_TEST_POSTGRES_CONNECTION_STRING -ErrorAction SilentlyContinue
        }
        else {
            $env:LIANSUREPROTECT_TEST_POSTGRES_CONNECTION_STRING = $previousPostgreSqlTestConnectionString
        }

        Pop-Location
    }
}

if ($RunApi) {
    Push-Location $repositoryRoot
    try {
        Invoke-CheckedCommand "dotnet" @("run", "--project", "src\LIAnsureProtect.Api\LIAnsureProtect.Api.csproj")
    }
    finally {
        Pop-Location
    }
}
