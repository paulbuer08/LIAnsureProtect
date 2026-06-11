param(
    [bool] $SuppressEfCommandLogs = $true
)

. (Join-Path $PSScriptRoot "common.ps1")

$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$efCommandLogLevelVariableName = "Logging__LogLevel__Microsoft.EntityFrameworkCore.Database.Command"
$previousEfCommandLogLevel = [Environment]::GetEnvironmentVariable($efCommandLogLevelVariableName, "Process")

Push-Location $repositoryRoot
try {
    Invoke-CheckedCommand "dotnet" @("tool", "restore")

    if ($SuppressEfCommandLogs) {
        [Environment]::SetEnvironmentVariable($efCommandLogLevelVariableName, "Critical", "Process")
    }

    Invoke-CheckedCommand "dotnet" @(
        "ef",
        "database",
        "update",
        "--project",
        "src\LIAnsureProtect.Infrastructure\LIAnsureProtect.Infrastructure.csproj",
        "--startup-project",
        "src\LIAnsureProtect.Api\LIAnsureProtect.Api.csproj",
        "--context",
        "SubmissionDbContext"
    )
}
finally {
    [Environment]::SetEnvironmentVariable($efCommandLogLevelVariableName, $previousEfCommandLogLevel, "Process")
    Pop-Location
}
