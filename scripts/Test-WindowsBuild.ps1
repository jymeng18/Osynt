param(
    [Parameter(Mandatory = $true)]
    [string]$ExecutablePath
)

$ErrorActionPreference = 'Stop'
$previousDataDirectory = $env:OSYNT_DATA_DIR
$smokeDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "OsyntSmoke-$([Guid]::NewGuid().ToString('N'))"
$application = $null

try {
    # Keep the smoke test separate from the user's Windows workspace.
    $env:OSYNT_DATA_DIR = $smokeDirectory
    $application = Start-Process -FilePath $ExecutablePath -WorkingDirectory ([System.IO.Path]::GetTempPath()) -PassThru
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    $ready = $false

    while ([DateTime]::UtcNow -lt $deadline) {
        $application.Refresh()
        if ($application.HasExited) {
            throw "Windows app exited during startup with code $($application.ExitCode)."
        }

        if ($application.MainWindowHandle -ne [IntPtr]::Zero -and $application.Responding) {
            $ready = $true
            break
        }
        Start-Sleep -Milliseconds 200
    }

    if (-not $ready) {
        throw 'Windows app did not create a responding window within 30 seconds.'
    }
    if (-not $application.Responding) {
        throw 'Windows app is not responding.'
    }

    Write-Host "PASS Native Windows window: $($application.MainWindowTitle)"
    Write-Host 'PASS Windows application is responding'
}
finally {
    if ($null -ne $application -and -not $application.HasExited) {
        $null = $application.CloseMainWindow()
        if (-not $application.WaitForExit(5000)) {
            Stop-Process -Id $application.Id -Force
        }
    }
    $env:OSYNT_DATA_DIR = $previousDataDirectory
}
