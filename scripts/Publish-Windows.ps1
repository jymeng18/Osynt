param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Runtime = 'win-x64',
    [string]$OutputDirectory = ''
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repositoryRoot 'src/Osynt.Desktop/Osynt.Desktop.csproj'
$buildArtifacts = Join-Path $repositoryRoot 'artifacts/dotnet-windows'
if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $repositoryRoot "artifacts/windows/$Runtime"
}

dotnet publish $project `
    --configuration Release `
    --runtime $Runtime `
    --self-contained true `
    --artifacts-path $buildArtifacts `
    --output $OutputDirectory `
    -p:PublishProfile=WindowsPortable `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -m:1

if ($LASTEXITCODE -ne 0) {
    throw "Windows publish failed with exit code $LASTEXITCODE."
}

Write-Host "Windows build ready: $(Join-Path $OutputDirectory 'Osynt.Desktop.exe')"
