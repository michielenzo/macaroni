param([string]$Output = "$PSScriptRoot/artifacts/Macaroni")
$ErrorActionPreference = 'Stop'
dotnet run --project "$PSScriptRoot/tests/Macaroni.Tests" -- "$PSScriptRoot/config.example.json"
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
dotnet publish "$PSScriptRoot/src/Macaroni" -c Release --self-contained false -o $Output
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
Write-Host "Built $Output/Macaroni.exe"
