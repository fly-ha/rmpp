param(
    [string]$Configuration = "Release",
    [string]$OutputDirectory = "samples"
)

$ErrorActionPreference = "Stop"
$resolvedOutput = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\$OutputDirectory"))
dotnet run --project tools/Rmpp.SampleGenerator/Rmpp.SampleGenerator.csproj -c $Configuration -- $resolvedOutput
if ($LASTEXITCODE -ne 0) {
    throw "Sample generation failed."
}

$samples = Get-ChildItem -LiteralPath $resolvedOutput -Filter *.rmpp
if ($samples.Count -ne 8) {
    throw "Expected 8 sample templates, found $($samples.Count)."
}

Write-Host "Generated $($samples.Count) sample templates in $resolvedOutput"
