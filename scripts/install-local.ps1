<#
Builds SuperDictate, runs its self-test, and installs it the way customers get
it: the new exe installs itself (SuperDictate.exe --install), replacing
SuperDictate.exe in the install folder and keeping Data, Models and Runtime.
Then it starts the installed copy.

-InstallRoot picks the folder on a first install; later installs stay in the
folder recorded in the Apps entry.
#>
[CmdletBinding()]
param(
    [string]$InstallRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Built next to the project, not in the system drive's temp folder.
$staging = Join-Path $PSScriptRoot "..\dist\local-$PID"

try {
    Write-Host "Building..." -ForegroundColor Cyan
    & "$PSScriptRoot\build-app.ps1" -OutputDir $staging
    $stagedExe = Join-Path $staging "SuperDictate.exe"
    if (-not (Test-Path $stagedExe)) { throw "Build output missing: $stagedExe" }

    Write-Host "Running diagnostics on the new build..." -ForegroundColor Cyan
    & $stagedExe --self-test all | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "The new build failed its self-test (exit code $LASTEXITCODE); nothing was installed." }

    Write-Host "Installing..." -ForegroundColor Cyan
    $installArgs = @('--install')
    if ($InstallRoot) { $installArgs += $InstallRoot }
    # SuperDictate.exe is a windowed app: PowerShell waits for it only when its output is piped.
    & $stagedExe @installArgs | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Install failed (exit code $LASTEXITCODE)." }

    # The installer records its folder in the Apps entry; SuperDictate.exe sits at the top of it.
    $entry = Get-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\com.local.superdictate"
    $installedExe = Join-Path $entry.InstallLocation "SuperDictate.exe"
    if (-not (Test-Path $installedExe)) { throw "The installer finished but $installedExe is missing." }

    # Started through WMI so it doesn't belong to this console.
    Write-Host "Starting $installedExe" -ForegroundColor Green
    $null = Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{
        CommandLine      = "`"$installedExe`""
        CurrentDirectory = (Split-Path $installedExe)
    }
}
finally {
    if (Test-Path $staging) { Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue }
}
