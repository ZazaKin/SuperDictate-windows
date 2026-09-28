[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$OutputDir = "$PSScriptRoot\..\publish"
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Resolve dotnet executable with SDK support
$dotnet = $null
$localDotnet = "$env:LOCALAPPDATA\dotnet-sdk8\dotnet.exe"
if (Test-Path $localDotnet) {
    $dotnet = $localDotnet
    $env:DOTNET_ROOT = "$env:LOCALAPPDATA\dotnet-sdk8"
} else {
    $systemDotnet = Get-Command dotnet -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source
    if ($systemDotnet) {
        $sdks = & $systemDotnet --list-sdks 2>$null
        if ($sdks) {
            $dotnet = $systemDotnet
        }
    }
}

if (-not $dotnet) {
    throw ".NET SDK not found. Please install .NET 8 SDK."
}

$projectPath = (Resolve-Path "$PSScriptRoot\..\src\SuperDictate\SuperDictate.csproj").Path
$resolvedOutput = if ([System.IO.Path]::IsPathRooted($OutputDir)) {
    $OutputDir
} else {
    [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $OutputDir))
}

Write-Host "Building SuperDictate for win-x64..." -ForegroundColor Cyan
Write-Host "Project: $projectPath"
Write-Host "Output:  $resolvedOutput"

& $dotnet publish $projectPath `
    -c Release `
    -r win-x64 `
    --self-contained `
    -p:PublishSingleFile=true `
    -o $resolvedOutput

if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE"
}

Write-Host "Build completed successfully at $resolvedOutput" -ForegroundColor Green
