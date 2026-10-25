param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repositoryRoot 'src/Osynt.Desktop/Osynt.Desktop.csproj'
$outputDirectory = Join-Path $repositoryRoot "artifacts/windows/$Runtime"

dotnet publish $project `
    --configuration Release `
    --runtime $Runtime `
    --self-contained true `
    --output $outputDirectory `
    -p:PublishProfile=WindowsPortable `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -m:1

if ($LASTEXITCODE -ne 0) {
    throw "Windows publish failed with exit code $LASTEXITCODE."
}

Write-Host "Windows build ready: $(Join-Path $outputDirectory 'Osynt.Desktop.exe')"
