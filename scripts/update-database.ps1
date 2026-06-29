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

    # Each bounded-context module owns its own DbContext (and PostgreSQL schema), so migrations are
    # applied once per context. Add a line here when a new module introduces its own DbContext.
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

    Invoke-CheckedCommand "dotnet" @(
        "ef",
        "database",
        "update",
        "--project",
        "src\Modules\Notifications\LIAnsureProtect.Modules.Notifications.Infrastructure\LIAnsureProtect.Modules.Notifications.Infrastructure.csproj",
        "--startup-project",
        "src\LIAnsureProtect.Api\LIAnsureProtect.Api.csproj",
        "--context",
        "NotificationsDbContext"
    )
}
finally {
    [Environment]::SetEnvironmentVariable($efCommandLogLevelVariableName, $previousEfCommandLogLevel, "Process")
    Pop-Location
}
