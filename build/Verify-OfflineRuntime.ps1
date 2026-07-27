param(
    [string]$ExecutablePath = "src/Rmpp.Desktop/bin/Release/net10.0-windows/Rmpp.Desktop.exe",
    [string]$ArtifactDirectory = "artifacts/offline"
)

$ErrorActionPreference = "Stop"
$exe = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\$ExecutablePath"))
if (-not (Test-Path -LiteralPath $exe)) { throw "Desktop executable not found: $exe" }
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ("rmpp-offline-" + [Guid]::NewGuid().ToString("N"))
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\$ArtifactDirectory"))
New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null
New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
$connections = @()
try {
    $process = Start-Process -FilePath $exe -ArgumentList @("--offline-smoke", $tempRoot) -PassThru -WindowStyle Hidden
    while (-not $process.HasExited) {
        if (Get-Command Get-NetTCPConnection -ErrorAction SilentlyContinue) {
            $connections += Get-NetTCPConnection -OwningProcess $process.Id -ErrorAction SilentlyContinue |
                Where-Object { $_.State -notin @("Listen", "Bound", "Closed") }
        }
        Start-Sleep -Milliseconds 50
        $process.Refresh()
    }
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) {
        $errorPath = Join-Path $tempRoot "offline-smoke-error.txt"
        $detail = if (Test-Path -LiteralPath $errorPath) { Get-Content -Raw $errorPath } else { "No error report." }
        throw "Offline smoke workflow failed with exit code $($process.ExitCode). $detail"
    }
    if ($connections.Count -gt 0) {
        $connections | Format-Table -AutoSize | Out-String | Set-Content -Encoding utf8 (Join-Path $artifactRoot "unexpected-connections.txt")
        throw "RMPP initiated or held a TCP connection during the offline smoke workflow."
    }
    $report = Join-Path $tempRoot "offline-smoke-report.json"
    if (-not (Test-Path -LiteralPath $report)) { throw "Offline smoke report was not produced." }
    Copy-Item -LiteralPath $report -Destination (Join-Path $artifactRoot "offline-smoke-report.json") -Force
    Write-Host "Offline runtime verification passed: no TCP connections observed."
}
finally {
    $resolvedTemp = [IO.Path]::GetFullPath($tempRoot)
    $systemTemp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if ($resolvedTemp.StartsWith($systemTemp, [StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $resolvedTemp)) {
        Remove-Item -LiteralPath $resolvedTemp -Recurse -Force -ErrorAction SilentlyContinue
    }
}
