param(
    [string]$Version = "0.9.0-preview.1",
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SkipInstaller
)

$ErrorActionPreference = "Stop"
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$artifactRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot "artifacts"))
$distributionRoot = Join-Path $artifactRoot "distribution\$Version"
$publishRoot = Join-Path $artifactRoot "publish\$Runtime"
$portableName = "rmpp-$Version-$Runtime-portable"
$portableRoot = Join-Path $distributionRoot $portableName
$portableZip = Join-Path $distributionRoot "$portableName.zip"

foreach ($target in @($distributionRoot, $publishRoot)) {
    $resolved = [System.IO.Path]::GetFullPath($target)
    if (-not $resolved.StartsWith($artifactRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Distribution target escaped the artifacts directory: $resolved"
    }
    if (Test-Path -LiteralPath $resolved) {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}

New-Item -ItemType Directory -Path $distributionRoot -Force | Out-Null
New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null

& (Join-Path $PSScriptRoot "Generate-Samples.ps1") -Configuration $Configuration

dotnet publish src/Rmpp.Desktop/Rmpp.Desktop.csproj -c $Configuration -r $Runtime --self-contained true -p:Version=$Version -p:PublishDir="$publishRoot\"
if ($LASTEXITCODE -ne 0) {
    throw "Self-contained desktop publish failed."
}

Copy-Item -LiteralPath (Join-Path $repoRoot "LICENSE") -Destination $publishRoot -Force
if (Test-Path -LiteralPath (Join-Path $repoRoot "THIRD-PARTY-NOTICES.md")) {
    Copy-Item -LiteralPath (Join-Path $repoRoot "THIRD-PARTY-NOTICES.md") -Destination $publishRoot -Force
}
Copy-Item -LiteralPath (Join-Path $repoRoot "samples") -Destination (Join-Path $publishRoot "samples") -Recurse -Force
& (Join-Path $PSScriptRoot "Collect-ThirdPartyLicenses.ps1") -DestinationDirectory (Join-Path $publishRoot "licenses")
& (Join-Path $PSScriptRoot "Verify-Licenses.ps1") -PublishDirectory $publishRoot

Copy-Item -LiteralPath $publishRoot -Destination $portableRoot -Recurse -Force
New-Item -ItemType File -Path (Join-Path $portableRoot "portable.marker") -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $portableRoot "data") -Force | Out-Null

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::Open($portableZip, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    $timestamp = [DateTimeOffset]::new(2026, 7, 27, 0, 0, 0, [TimeSpan]::Zero)
    $distributionPrefix = $distributionRoot.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    Get-ChildItem -LiteralPath $portableRoot -Recurse -File | Sort-Object FullName | ForEach-Object {
        if (-not $_.FullName.StartsWith($distributionPrefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Portable file escaped the distribution directory: $($_.FullName)"
        }
        $relative = $_.FullName.Substring($distributionPrefix.Length).Replace('\', '/')
        $entry = $zip.CreateEntry($relative, [System.IO.Compression.CompressionLevel]::Optimal)
        $entry.LastWriteTime = $timestamp
        $input = $_.OpenRead()
        $output = $entry.Open()
        try {
            $input.CopyTo($output)
        }
        finally {
            $output.Dispose()
            $input.Dispose()
        }
    }
}
finally {
    $zip.Dispose()
}

if (-not $SkipInstaller) {
    dotnet restore installer/InstallerTools.csproj
    if ($LASTEXITCODE -ne 0) {
        throw "Could not restore the build-only Inno Setup compiler package."
    }
    $nugetRoot = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE ".nuget\packages" }
    $packagedCompiler = Join-Path $nugetRoot "tools.innosetup\6.4.3\tools\ISCC.exe"
    if (-not (Test-Path -LiteralPath $packagedCompiler)) {
        throw "Pinned Inno Setup 6.4.3 compiler was not found after restore. Use -SkipInstaller only for non-distribution builds."
    }

    & $packagedCompiler "/DAppVersion=$Version" "/DSourceDir=$publishRoot" "/DOutputDir=$distributionRoot" (Join-Path $repoRoot "installer\Rmpp.iss")
    if ($LASTEXITCODE -ne 0) {
        throw "Inno Setup compilation failed."
    }
}

$releaseFiles = Get-ChildItem -LiteralPath $distributionRoot -File | Where-Object Extension -in @('.zip', '.exe')
foreach ($file in $releaseFiles) {
    $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $file.FullName).Hash.ToLowerInvariant()
    "$hash  $($file.Name)" | Set-Content -Encoding Ascii -LiteralPath ($file.FullName + ".sha256")
}

Write-Host "Distribution created at $distributionRoot"
