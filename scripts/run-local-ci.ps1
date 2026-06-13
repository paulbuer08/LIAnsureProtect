param(
    [bool] $RunSmokeTests = $true,
    [int] $ApiStartupTimeoutSeconds = 60,
    [ValidateSet("Cleanup", "LeaveRunning")]
    [string] $PostgreSqlAfterRun = "Cleanup",
    [bool] $CreateZipArtifact = $true,
    [string] $ResultsRoot = "TestResults"
)

. (Join-Path $PSScriptRoot "common.ps1")

$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$runTimestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$testResultsRoot = Join-Path $repositoryRoot $ResultsRoot
$testResultsDirectory = Join-Path $testResultsRoot "local-ci-$runTimestamp"
$artifactPath = Join-Path $testResultsRoot "local-ci-$runTimestamp.zip"

New-Item -ItemType Directory -Force -Path $testResultsDirectory | Out-Null

$apiProcess = $null

try {
    & (Join-Path $PSScriptRoot "setup-dev.ps1") -RunTests:$true -TestResultsDirectory:$testResultsDirectory

    Push-Location $repositoryRoot
    try {
        Invoke-CheckedCommand "docker" @("compose", "config")
    }
    finally {
        Pop-Location
    }

    if ($RunSmokeTests) {
        $apiOutputPath = Join-Path $testResultsDirectory "api-smoke-stdout.log"
        $apiErrorPath = Join-Path $testResultsDirectory "api-smoke-stderr.log"
        $smokeResultPath = Join-Path $testResultsDirectory "api-smoke-result.json"

        Remove-Item $apiOutputPath, $apiErrorPath, $smokeResultPath -ErrorAction SilentlyContinue

        Push-Location $repositoryRoot
        try {
            $apiArguments = @(
                "run",
                "--project",
                "src\LIAnsureProtect.Api\LIAnsureProtect.Api.csproj",
                "--no-build",
                "--no-restore",
                "--",
                "--urls",
                "http://localhost:5223"
            )

            $apiProcess = Start-Process `
                -FilePath "dotnet" `
                -ArgumentList $apiArguments `
                -WorkingDirectory $repositoryRoot `
                -RedirectStandardOutput $apiOutputPath `
                -RedirectStandardError $apiErrorPath `
                -WindowStyle Hidden `
                -PassThru
        }
        finally {
            Pop-Location
        }

        $deadline = (Get-Date).AddSeconds($ApiStartupTimeoutSeconds)
        $healthResponse = $null

        while ((Get-Date) -lt $deadline) {
            if ($apiProcess.HasExited) {
                throw "API process exited before smoke tests could run. See $apiOutputPath and $apiErrorPath."
            }

            try {
                $healthResponse = Invoke-RestMethod -Uri "http://localhost:5223/api/v1/health" -TimeoutSec 3

                if ($healthResponse -eq "Healthy") {
                    break
                }
            }
            catch {
                Start-Sleep -Seconds 1
            }
        }

        if ($healthResponse -ne "Healthy") {
            throw "API did not become healthy within $ApiStartupTimeoutSeconds seconds. See $apiOutputPath and $apiErrorPath."
        }

        $rootResponse = Invoke-RestMethod -Uri "http://localhost:5223/" -TimeoutSec 10

        $body = @{
            applicantName = "Verification Applicant"
            applicantEmail = "verification@example.com"
            companyName = "Verification Company"
        } | ConvertTo-Json

        $submissionStatusCode = $null

        try {
            Invoke-RestMethod `
                -Method Post `
                -Uri "http://localhost:5223/api/v1/submissions" `
                -ContentType "application/json" `
                -Body $body `
                -TimeoutSec 10 | Out-Null
        }
        catch {
            $submissionStatusCode = [int] $_.Exception.Response.StatusCode
        }

        if ($rootResponse.status -ne "Running") {
            throw "Root endpoint smoke test failed. Expected status Running."
        }

        if ($submissionStatusCode -ne 401) {
            throw "Submission smoke test failed. Expected anonymous request to return 401 Unauthorized."
        }

        @{
            root = $rootResponse
            health = $healthResponse
            anonymousSubmission = @{
                expectedStatusCode = 401
                actualStatusCode = $submissionStatusCode
            }
        } | ConvertTo-Json -Depth 5 | Set-Content -Path $smokeResultPath
    }

    if ($null -ne $apiProcess -and -not $apiProcess.HasExited) {
        Stop-Process -Id $apiProcess.Id -Force
        $apiProcess.WaitForExit()
        $apiProcess = $null
    }

    @{
        status = "Passed"
        runTimestamp = $runTimestamp
        runSmokeTests = $RunSmokeTests
        postgreSqlAfterRun = $PostgreSqlAfterRun
        resultsDirectory = $testResultsDirectory
        artifactPath = if ($CreateZipArtifact) { $artifactPath } else { $null }
    } | ConvertTo-Json -Depth 5 | Set-Content -Path (Join-Path $testResultsDirectory "verification-summary.json")

    if ($CreateZipArtifact) {
        Compress-Archive -Path $testResultsDirectory -DestinationPath $artifactPath -Force

        try {
            Remove-Item -LiteralPath $testResultsDirectory -Recurse -Force
        }
        catch {
            Write-Warning "Artifact zip was created, but the source result folder could not be removed: $testResultsDirectory"
            Write-Warning $_.Exception.Message
        }
    }

    Write-Output "Local CI passed."
    if ($CreateZipArtifact) {
        Write-Output "Artifact zip: $artifactPath"
    }
    else {
        Write-Output "Test results are in: $testResultsDirectory"
    }
}
finally {
    if ($null -ne $apiProcess -and -not $apiProcess.HasExited) {
        Stop-Process -Id $apiProcess.Id -Force
        $apiProcess.WaitForExit()
    }

    if ($PostgreSqlAfterRun -eq "Cleanup") {
        & (Join-Path $PSScriptRoot "stop-dependencies.ps1") -RemoveVolumes:$true
    }
}