param([Parameter(Mandatory = $true)][string]$DestinationDirectory)

$ErrorActionPreference = "Stop"
$destination = [System.IO.Path]::GetFullPath($DestinationDirectory)
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$nugetRoot = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE ".nuget\packages" }
$dotnetRoot = Split-Path (Get-Command dotnet).Source
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))

function Copy-RequiredFile([string]$source, [string]$name) {
    if (-not (Test-Path -LiteralPath $source)) {
        throw "Required licence file not found: $source"
    }
    Copy-Item -LiteralPath $source -Destination (Join-Path $destination $name) -Force
}

Copy-RequiredFile (Join-Path $repoRoot "LICENSE") "RMPP-LICENSE.txt"
Copy-RequiredFile (Join-Path $repoRoot "licenses\Apache-2.0.txt") "Apache-2.0.txt"
Copy-RequiredFile (Join-Path $dotnetRoot "LICENSE.txt") "DotNet-LICENSE.txt"
Copy-RequiredFile (Join-Path $dotnetRoot "ThirdPartyNotices.txt") "DotNet-ThirdPartyNotices.txt"
Copy-RequiredFile (Join-Path $nugetRoot "docnet.core\2.6.0\runtimes\win-x64\native\LICENSE") "PDFium-LICENSE.txt"
Copy-RequiredFile (Join-Path $nugetRoot "skiasharp\4.150.1\LICENSE.txt") "SkiaSharp-LICENSE.txt"
Copy-RequiredFile (Join-Path $nugetRoot "skiasharp.harfbuzz\4.150.1\LICENSE.txt") "SkiaSharp.HarfBuzz-LICENSE.txt"
Copy-RequiredFile (Join-Path $nugetRoot "harfbuzzsharp\14.2.1.1\LICENSE.txt") "HarfBuzzSharp-LICENSE.txt"
Copy-RequiredFile (Join-Path $nugetRoot "sourcegear.sqlite3\3.53.3\LICENSE.txt") "SourceGear.sqlite3-LICENSE.txt"
Copy-RequiredFile (Join-Path $nugetRoot "communitytoolkit.mvvm\8.4.0\License.md") "CommunityToolkit.Mvvm-LICENSE.md"
Copy-RequiredFile (Join-Path $nugetRoot "communitytoolkit.mvvm\8.4.0\ThirdPartyNotices.txt") "CommunityToolkit.Mvvm-ThirdPartyNotices.txt"
Copy-RequiredFile (Join-Path $nugetRoot "microsoft.extensions.dependencyinjection\10.0.1\THIRD-PARTY-NOTICES.TXT") "Microsoft.Extensions.DependencyInjection-ThirdPartyNotices.txt"
Copy-RequiredFile (Join-Path $nugetRoot "tools.innosetup\6.4.3\tools\license.txt") "InnoSetup-LICENSE.txt"
Copy-RequiredFile (Join-Path $repoRoot "licenses\InnoSetup-ChineseSimplified-MIT.txt") "InnoSetup-ChineseSimplified-MIT.txt"
Copy-RequiredFile (Join-Path $PSScriptRoot "runtime-license-map.json") "runtime-license-map.json"
$runtimeMap = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $PSScriptRoot "runtime-license-map.json") | ConvertFrom-Json
$generatedNotice = @(
    "RMPP generated third-party package notice",
    "",
    "The complete licence texts are included in this directory.",
    "The application is self-contained and also includes DotNet-LICENSE.txt and DotNet-ThirdPartyNotices.txt.",
    "The installer is compiled with Inno Setup 6.4.3; see InnoSetup-LICENSE.txt.",
    ""
)
foreach ($entry in $runtimeMap | Sort-Object id) {
    $generatedNotice += "$($entry.id) $($entry.version) - $($entry.license) - https://www.nuget.org/packages/$($entry.id)/$($entry.version)"
}
[System.IO.File]::WriteAllLines(
    (Join-Path $destination "THIRD-PARTY-NOTICES.txt"),
    $generatedNotice,
    [System.Text.UTF8Encoding]::new($false))
if (Test-Path -LiteralPath (Join-Path $repoRoot "THIRD-PARTY-NOTICES.md")) {
    Copy-RequiredFile (Join-Path $repoRoot "THIRD-PARTY-NOTICES.md") "THIRD-PARTY-NOTICES.md"
}

Write-Host "Collected third-party licence files in $destination"
