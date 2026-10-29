param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
[xml]$properties = Get-Content (Join-Path $repositoryRoot 'Directory.Build.props') -Raw
$version = [string]$properties.Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+$') {
    throw 'Set a three-part release Version in Directory.Build.props before packaging.'
}
if ($env:GITHUB_REF_TYPE -eq 'tag' -and $env:GITHUB_REF_NAME -ne "v$version") {
    throw "The release tag must match the project version v$version."
}

$staging = Join-Path $repositoryRoot "artifacts/package-staging/$version/$Runtime/$([Guid]::NewGuid().ToString('N'))"
& (Join-Path $PSScriptRoot 'Publish-Windows.ps1') -Runtime $Runtime -OutputDirectory $staging
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }

$packageDirectory = Join-Path $repositoryRoot 'artifacts/packages'
$null = New-Item -ItemType Directory -Force -Path $packageDirectory
Copy-Item (Join-Path $repositoryRoot 'README.md') $staging
Copy-Item (Join-Path $repositoryRoot 'docs/local-data.md') $staging
Copy-Item (Join-Path $repositoryRoot 'src/Osynt.Desktop/Assets/Garden/THREE-LICENSE.txt') (Join-Path $staging 'THREE-LICENSE.txt')
$revision = git -c "safe.directory=$repositoryRoot" -C $repositoryRoot rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Could not identify the source revision.' }
$changes = @(git -c "safe.directory=$repositoryRoot" -C $repositoryRoot status --porcelain)
if ($LASTEXITCODE -ne 0) { throw 'Could not identify working-tree changes.' }
$metadata = @{ version = $version; runtime = $Runtime; revision = $revision.Trim(); dirty = ($changes.Count -gt 0) }
$utf8 = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText((Join-Path $staging 'release-info.json'), ($metadata | ConvertTo-Json), $utf8)
$archive = Join-Path $packageDirectory "Osynt-$version-$Runtime.zip"
Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $archive -Force
$checksum = (Get-FileHash -Algorithm SHA256 $archive).Hash.ToLowerInvariant()
[System.IO.File]::WriteAllText("$archive.sha256", "$checksum  $([System.IO.Path]::GetFileName($archive))`n", $utf8)
Write-Host "Package ready: $archive"
Write-Host "SHA256: $checksum"
if ($env:GITHUB_OUTPUT) {
    Add-Content -Path $env:GITHUB_OUTPUT -Value "executable=$(Join-Path $staging 'Osynt.Desktop.exe')"
}
