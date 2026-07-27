param(
    [string]$Version = "0.9.0-preview.1",
    [string]$Runtime = "win-x64",
    [switch]$VerifyInstaller
)

$ErrorActionPreference = "Stop"
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$artifactRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot "artifacts"))
$distributionRoot = Join-Path $artifactRoot "distribution\$Version"
$verificationRoot = Join-Path $artifactRoot "distribution-verification\$Version"
$portableName = "rmpp-$Version-$Runtime-portable"
$portableZip = Join-Path $distributionRoot "$portableName.zip"
$setup = Join-Path $distributionRoot "rmpp-$Version-$Runtime-setup.exe"
$artifactHashes = [ordered]@{}
$installerInstallVerified = $false
$installerUpgradeVerified = $false
$installerUninstallVerified = $false
$installerTcpConnectionsObserved = 0

if (-not (Test-Path -LiteralPath $portableZip)) {
    throw "Portable ZIP not found: $portableZip"
}

foreach ($artifact in Get-ChildItem -LiteralPath $distributionRoot -File | Where-Object Extension -in @('.zip', '.exe')) {
    $checksumPath = $artifact.FullName + ".sha256"
    if (-not (Test-Path -LiteralPath $checksumPath)) {
        throw "Checksum not found: $checksumPath"
    }
    $expected = ((Get-Content -Raw -LiteralPath $checksumPath).Trim() -split '\s+')[0]
    $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $artifact.FullName).Hash.ToLowerInvariant()
    if ($expected -ne $actual) {
        throw "Checksum mismatch: $($artifact.Name)"
    }
    $artifactHashes[$artifact.Name] = $actual
}

$resolvedVerification = [System.IO.Path]::GetFullPath($verificationRoot)
if (-not $resolvedVerification.StartsWith($artifactRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Verification directory escaped artifacts: $resolvedVerification"
}
if (Test-Path -LiteralPath $resolvedVerification) {
    Remove-Item -LiteralPath $resolvedVerification -Recurse -Force
}
New-Item -ItemType Directory -Path $resolvedVerification -Force | Out-Null
Expand-Archive -LiteralPath $portableZip -DestinationPath $resolvedVerification

$portableRoot = Join-Path $resolvedVerification $portableName
$portableExe = Join-Path $portableRoot "Rmpp.Desktop.exe"
if (-not (Test-Path -LiteralPath (Join-Path $portableRoot "portable.marker"))) {
    throw "Portable marker is missing."
}
foreach ($requiredLicense in @("RMPP-LICENSE.txt", "Apache-2.0.txt", "DotNet-LICENSE.txt", "DotNet-ThirdPartyNotices.txt", "PDFium-LICENSE.txt", "InnoSetup-LICENSE.txt", "InnoSetup-ChineseSimplified-MIT.txt", "THIRD-PARTY-NOTICES.txt")) {
    if (-not (Test-Path -LiteralPath (Join-Path $portableRoot "licenses\$requiredLicense"))) {
        throw "Portable licence file is missing: $requiredLicense"
    }
}

$storage = Start-Process -FilePath $portableExe -ArgumentList "--storage-smoke" -PassThru -Wait -WindowStyle Hidden
if ($storage.ExitCode -ne 0) {
    throw "Portable storage smoke failed with exit code $($storage.ExitCode)."
}
$storageReport = Join-Path $portableRoot "data\storage-smoke.json"
if (-not (Test-Path -LiteralPath $storageReport)) {
    throw "Portable storage report was not written beside the application."
}
$storageJson = Get-Content -Raw -LiteralPath $storageReport | ConvertFrom-Json
if (-not $storageJson.IsPortable) {
    throw "Portable storage detection returned false."
}

$movedRoot = Join-Path $resolvedVerification "$portableName-moved"
Move-Item -LiteralPath $portableRoot -Destination $movedRoot
$movedExe = Join-Path $movedRoot "Rmpp.Desktop.exe"
$movedStorage = Start-Process -FilePath $movedExe -ArgumentList "--storage-smoke" -PassThru -Wait -WindowStyle Hidden
if ($movedStorage.ExitCode -ne 0 -or -not (Test-Path -LiteralPath (Join-Path $movedRoot "data\storage-smoke.json"))) {
    throw "Moved portable distribution did not retain local storage behavior."
}

$repoPrefix = $repoRoot.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
if (-not $movedExe.StartsWith($repoPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Portable executable escaped the repository: $movedExe"
}
$relativeExe = $movedExe.Substring($repoPrefix.Length)
& (Join-Path $PSScriptRoot "Verify-OfflineRuntime.ps1") -ExecutablePath $relativeExe -ArtifactDirectory "artifacts/distribution-verification/$Version/offline"

if ($VerifyInstaller) {
    if (-not (Test-Path -LiteralPath $setup)) {
        throw "Installer not found: $setup"
    }
    $installRoot = Join-Path $resolvedVerification "installed"
    $installLog = Join-Path $resolvedVerification "install.log"
    $connections = @()
    $installer = Start-Process -FilePath $setup -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/DIR=$installRoot", "/LOG=$installLog") -PassThru -WindowStyle Hidden
    while (-not $installer.HasExited) {
        if (Get-Command Get-NetTCPConnection -ErrorAction SilentlyContinue) {
            $connections += Get-NetTCPConnection -OwningProcess $installer.Id -ErrorAction SilentlyContinue | Where-Object State -notin @('Listen', 'Bound', 'Closed')
        }
        Start-Sleep -Milliseconds 50
        $installer.Refresh()
    }
    if ($installer.ExitCode -ne 0 -or $connections.Count -gt 0) {
        throw "Silent installer smoke failed or opened a TCP connection."
    }
    $installerInstallVerified = $true
    $installerTcpConnectionsObserved = $connections.Count

    $upgrade = Start-Process -FilePath $setup -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/DIR=$installRoot") -PassThru -Wait -WindowStyle Hidden
    if ($upgrade.ExitCode -ne 0) {
        throw "Silent upgrade smoke failed."
    }
    $installerUpgradeVerified = $true

    $uninstaller = Join-Path $installRoot "unins000.exe"
    if (-not (Test-Path -LiteralPath $uninstaller)) {
        throw "Uninstaller was not created."
    }
    $uninstall = Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART') -PassThru -Wait -WindowStyle Hidden
    if ($uninstall.ExitCode -ne 0) {
        throw "Silent uninstall smoke failed."
    }
    $installerUninstallVerified = $true
}

# Persist machine-readable evidence for local and CI distribution verification.
$verificationSummary = [ordered]@{
    SchemaVersion = 1
    Version = $Version
    Runtime = $Runtime
    OsDescription = ([System.Runtime.InteropServices.RuntimeInformation]::OSDescription)
    OsVersion = ([Environment]::OSVersion.VersionString)
    ProcessArchitecture = ([System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture).ToString()
    ArtifactSha256 = $artifactHashes
    PortableStorageVerified = $true
    MovedPortableVerified = $true
    OfflineRuntimeVerified = $true
    InstallerRequested = ([bool]$VerifyInstaller)
    InstallerInstallVerified = $installerInstallVerified
    InstallerUpgradeVerified = $installerUpgradeVerified
    InstallerUninstallVerified = $installerUninstallVerified
    InstallerTcpConnectionsObserved = $installerTcpConnectionsObserved
    VerifiedAtUtc = ([DateTimeOffset]::UtcNow).ToString("O")
}
$verificationSummaryJson = $verificationSummary | ConvertTo-Json -Depth 5
[System.IO.File]::WriteAllText(
    (Join-Path $resolvedVerification "verification-summary.json"),
    $verificationSummaryJson,
    [System.Text.UTF8Encoding]::new($false))

Write-Host "Distribution verification passed."
