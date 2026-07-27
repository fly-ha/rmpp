param(
    [string]$NoticesPath = (Join-Path $PSScriptRoot "..\THIRD-PARTY-NOTICES.md"),
    [string]$ProjectPath = "src/Rmpp.Desktop/Rmpp.Desktop.csproj",
    [string]$PublishDirectory
)

$ErrorActionPreference = "Stop"
$map = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $PSScriptRoot "runtime-license-map.json") | ConvertFrom-Json
$noticeText = if (Test-Path -LiteralPath $NoticesPath) {
    Get-Content -Raw -Encoding UTF8 -LiteralPath $NoticesPath
}
else {
    $map | ConvertTo-Json -Depth 4
}
$packageJsonText = dotnet list $ProjectPath package --include-transitive --format json | Out-String
if ($LASTEXITCODE -ne 0) {
    throw "Could not enumerate runtime packages."
}
$packageJson = $packageJsonText | ConvertFrom-Json
$packages = @()
foreach ($project in $packageJson.projects) {
    foreach ($framework in $project.frameworks) {
        $packages += $framework.topLevelPackages | ForEach-Object { [pscustomobject]@{ id = $_.id; version = $_.resolvedVersion } }
        $packages += $framework.transitivePackages | ForEach-Object { [pscustomobject]@{ id = $_.id; version = $_.resolvedVersion } }
    }
}

foreach ($package in $packages | Sort-Object id -Unique) {
    $approval = $map | Where-Object { [string]::Equals($_.id, $package.id, [StringComparison]::OrdinalIgnoreCase) } | Select-Object -First 1
    if ($null -eq $approval) {
        throw "Runtime package has no licence approval: $($package.id) $($package.version)"
    }
    if (-not [string]::Equals($approval.version, $package.version, [StringComparison]::Ordinal)) {
        throw "Approved version mismatch for $($package.id): expected $($approval.version), resolved $($package.version)"
    }
    if ($noticeText.IndexOf($approval.noticeToken, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
        throw "Notice token missing for $($package.id): $($approval.noticeToken)"
    }
    if ($approval.license -match "GPL|AGPL|SSPL|BUSL") {
        throw "Unapproved reciprocal or source-available licence detected for $($package.id): $($approval.license)"
    }
}

if ($PublishDirectory) {
    $licenseDirectory = Join-Path ([System.IO.Path]::GetFullPath($PublishDirectory)) "licenses"
    foreach ($required in @("RMPP-LICENSE.txt", "Apache-2.0.txt", "DotNet-LICENSE.txt", "DotNet-ThirdPartyNotices.txt", "PDFium-LICENSE.txt", "SkiaSharp-LICENSE.txt", "HarfBuzzSharp-LICENSE.txt", "SourceGear.sqlite3-LICENSE.txt", "InnoSetup-LICENSE.txt", "InnoSetup-ChineseSimplified-MIT.txt", "runtime-license-map.json", "THIRD-PARTY-NOTICES.txt")) {
        if (-not (Test-Path -LiteralPath (Join-Path $licenseDirectory $required))) {
            throw "Published licence file is missing: $required"
        }
    }
}

Write-Host "Licence verification passed for $($packages.Count) resolved runtime package entries."
