param([string]$Configuration = "Release")

$ErrorActionPreference = "Stop"

dotnet test tests/Rmpp.Desktop.Tests/Rmpp.Desktop.Tests.csproj -c $Configuration --filter "FullyQualifiedName~PrivacyBoundaryTests" --logger "console;verbosity=normal"
if ($LASTEXITCODE -ne 0) {
    throw "Privacy boundary verification failed."
}

$offlineReport = Join-Path $PSScriptRoot "..\artifacts\offline\offline-smoke-report.json"
if (Test-Path -LiteralPath $offlineReport) {
    $text = Get-Content -Raw -LiteralPath $offlineReport
    foreach ($value in @("RMPP-001", "RMPP-002")) {
        if ($text.IndexOf($value, [StringComparison]::Ordinal) -ge 0) {
            throw "Offline report leaked imported row value: $value"
        }
    }
}

Write-Host "Privacy verification passed."
