param([Parameter(Mandatory = $true)][string]$Version)

$ErrorActionPreference = "Stop"
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$gates = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $repoRoot "release\release-gates.json") | ConvertFrom-Json
if ($gates.changeId -ne "build-offline-wysiwyg-print-designer") {
    throw "Release gate change id is missing or incorrect."
}

$isStable = $Version -notmatch '-'
if ($isStable) {
    foreach ($property in @("physicalPrinterMatrixVerified", "cleanInstallMatrixVerified", "manualDesignerMatrixVerified")) {
        if (-not $gates.$property) {
            throw "Stable release $Version is blocked because $property is false."
        }
    }
}

Write-Host "Release gates accepted for version $Version."
