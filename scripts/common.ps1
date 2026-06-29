$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Invoke-CheckedCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string] $FilePath,

        [Parameter(Mandatory = $true)]
        [string[]] $Arguments
    )

    & $FilePath @Arguments

    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $FilePath $($Arguments -join ' ')"
    }
}

function Test-DockerImageExists {
    param(
        [Parameter(Mandatory = $true)]
        [string] $ImageName
    )

    & docker image inspect $ImageName *> $null

    return $LASTEXITCODE -eq 0
}

function Stop-MissingSubmissionMigrations {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Message
    )

    Write-Host ""
    Write-Host "EF Core migration files are missing." -ForegroundColor Red
    Write-Host ""
    Write-Host $Message
    Write-Host ""
    Write-Host "Why this matters:"
    Write-Host "The setup and local CI scripts apply committed EF Core migrations. They do not create migrations automatically."
    Write-Host "Without committed migrations, a fresh PostgreSQL database will not get the vector extension or submissions table."
    Write-Host ""
    Write-Host "Restore the repo-local dotnet-ef tool first:"
    Write-Host "dotnet tool restore" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Then create the first migration:"
    Write-Host "dotnet ef migrations add CreateSubmissionPersistence ``" -ForegroundColor Cyan
    Write-Host "  --project src\LIAnsureProtect.Infrastructure\LIAnsureProtect.Infrastructure.csproj ``" -ForegroundColor Cyan
    Write-Host "  --startup-project src\LIAnsureProtect.Api\LIAnsureProtect.Api.csproj ``" -ForegroundColor Cyan
    Write-Host "  --context SubmissionDbContext ``" -ForegroundColor Cyan
    Write-Host "  --output-dir Persistence\Migrations" -ForegroundColor Cyan
    Write-Host ""

    throw "EF Core migration files are missing. See recovery steps above."
}

function Assert-SubmissionMigrationsExist {
    param(
        [Parameter(Mandatory = $true)]
        [string] $RepositoryRoot
    )

    $migrationsDirectory = Join-Path $RepositoryRoot "src\LIAnsureProtect.Infrastructure\Persistence\Migrations"

    if (-not (Test-Path $migrationsDirectory -PathType Container)) {
        Stop-MissingSubmissionMigrations "Expected folder: $migrationsDirectory"
    }

    $migrationFiles = @(
        Get-ChildItem -Path $migrationsDirectory -Filter "*.cs" -File |
            Where-Object { $_.Name -ne "SubmissionDbContextModelSnapshot.cs" }
    )

    if ($migrationFiles.Count -eq 0) {
        Stop-MissingSubmissionMigrations "Expected at least one migration file in: $migrationsDirectory`nFound only the model snapshot or no C# migration files."
    }

    # Each bounded-context module owns its own DbContext + migrations; the Notifications module
    # must have its committed migrations too, or a fresh database will be missing the notifications schema.
    $notificationsMigrationsDirectory = Join-Path $RepositoryRoot "src\Modules\Notifications\LIAnsureProtect.Modules.Notifications.Infrastructure\Migrations"

    if (-not (Test-Path $notificationsMigrationsDirectory -PathType Container)) {
        Stop-MissingSubmissionMigrations "Expected folder: $notificationsMigrationsDirectory"
    }

    $notificationsMigrationFiles = @(
        Get-ChildItem -Path $notificationsMigrationsDirectory -Filter "*.cs" -File |
            Where-Object { $_.Name -ne "NotificationsDbContextModelSnapshot.cs" }
    )

    if ($notificationsMigrationFiles.Count -eq 0) {
        Stop-MissingSubmissionMigrations "Expected at least one migration file in: $notificationsMigrationsDirectory`nFound only the model snapshot or no C# migration files."
    }
}
