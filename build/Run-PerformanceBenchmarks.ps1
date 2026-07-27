param([string]$Configuration = "Release")

$ErrorActionPreference = "Stop"
dotnet test Rmpp.sln -c $Configuration --filter "Category=Performance" --logger "console;verbosity=normal"
if ($LASTEXITCODE -ne 0) { throw "Performance regression thresholds failed." }
