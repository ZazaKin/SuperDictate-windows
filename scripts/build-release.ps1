<#
Builds what customers download: dist\SuperDictate-Setup-<version>.exe,
dist\SHA256SUMS.txt and dist\update.json. Double-click build-release.cmd in the
project root to run it; the window stays open to show the result.

Answers come from release-settings.ini (project root). They are checked before
anything is built, then filled in where they belong: the version into
SuperDictate.csproj, the repository address into the docs, the certificate
thumbprint into release-signing.sha1. The app reads the same file (embedded)
for its Support page links and donation options.

The setup exe is the app itself. Run from anywhere but the install folder, it
installs per user; the installed copy runs the app and walks the customer
through the one-time speech runtime and model downloads.

Signing: the exe is signed with exactly the pinned certificate
(release-signing.sha1), never another one: SmartScreen reputation is attached
to it. Without a thumbprint the build is unsigned and Windows shows an
"unrecognized app" warning to customers.
#>
[CmdletBinding()]
param(
    # Default: dist in the project root. (Windows PowerShell leaves $PSScriptRoot empty in defaults.)
    [string]$OutputDir
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
trap {
    Write-Host ""
    Write-Host "Release not built." -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}

$rootDir = (Resolve-Path "$PSScriptRoot\..").Path
$settingsFile = Join-Path $rootDir "release-settings.ini"
$project = Join-Path $rootDir "src\SuperDictate\SuperDictate.csproj"
$pin = Join-Path $rootDir "release-signing.sha1"

# Writes only when the text changed, keeping an existing file's encoding (with or without BOM).
function Set-FileText([string]$Path, [string]$Text) {
    $bom = $false
    if (Test-Path $Path) {
        if ([System.IO.File]::ReadAllText($Path) -ceq $Text) { return }
        $bytes = [System.IO.File]::ReadAllBytes($Path)
        $bom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    }
    [System.IO.Directory]::CreateDirectory((Split-Path $Path)) | Out-Null
    [System.IO.File]::WriteAllText($Path, $Text, (New-Object System.Text.UTF8Encoding $bom))
    Write-Host "  filled in $($Path.Substring($rootDir.Length + 1))"
}

# --- release-settings.ini: read and check every answer before anything is built ---
if (-not (Test-Path $settingsFile)) { throw "release-settings.ini is missing from $rootDir." }
$settings = @{}
$donations = @()
$problems = @()
$section = ''
foreach ($raw in Get-Content $settingsFile -Encoding UTF8) {
    $line = $raw.Trim()
    if (-not $line -or $line.StartsWith(';')) { continue }
    if ($line.StartsWith('[')) { $section = $line.Trim('[', ']').Trim().ToLowerInvariant(); continue }
    $equals = $line.IndexOf('=')
    if ($equals -lt 1) { throw "release-settings.ini: can't read the line '$line'. Expected: name = value" }
    # Name | note = value: the answer always goes last.
    $parts = $line.Substring(0, $equals) -split '\|', 2
    $key = $parts[0].Trim()
    $note = if ($parts.Count -gt 1) { $parts[1].Trim() } else { '' }
    $value = $line.Substring($equals + 1).Trim()
    if ($section -eq 'donations' -and "$key $note" -match '://') { $problems += "${key}: the link is before '='. Put it at the end of the line: Name | note = link" }
    if ($section -eq 'release') { $settings[$key.ToLowerInvariant()] = $value }
    elseif ($section -eq 'donations' -and $value) { $donations += [pscustomobject]@{ Name = $key; Value = $value; Note = $note } }
}

$version = "$($settings['version'])"
if ($version -notmatch '^\d+\.\d+\.\d+$') { $problems += "version = '$version' must be three numbers, like 1.0.0" }
elseif (-not (Test-Path (Join-Path $rootDir "docs\releases\$version.md"))) { $problems += "version = $version has no release notes: create docs\releases\$version.md" }

$repository = "$($settings['repository'])".TrimEnd('/')
if ($repository -notmatch '^https://github\.com/[\w.-]+/[\w.-]+$') { $problems += "repository = '$repository' must look like https://github.com/<user>/<repository>" }

# Thumbprints copied from the certificate dialog carry spaces and an invisible mark.
$thumbprint = ("$($settings['signing_thumbprint'])" -replace '[\s\u200e\u200f]', '').ToUpperInvariant()
$pinned = if (Test-Path $pin) { (Get-Content $pin -Raw).Trim().ToUpperInvariant() } else { '' }
if ($thumbprint -and $thumbprint -notmatch '^[0-9A-F]{40}$') { $problems += "signing_thumbprint must be 40 characters 0-9 and A-F" }
elseif ($pinned -and $thumbprint -ne $pinned) {
    $problems += "signing_thumbprint: releases are signed with certificate $pinned (release-signing.sha1). A new certificate starts SmartScreen reputation from zero. If you really replaced it, delete release-signing.sha1 and run again."
}
elseif ($thumbprint -and -not (Get-ChildItem "Cert:\CurrentUser\My\$thumbprint", "Cert:\LocalMachine\My\$thumbprint" -ErrorAction SilentlyContinue)) {
    $problems += "signing_thumbprint: no certificate $thumbprint in your certificate store (certmgr.msc > Personal > Certificates)"
}

foreach ($donation in $donations) {
    if ($donation.Value.Contains('|')) { $problems += "$($donation.Name): the note goes before '=' and the link or address last: Name | note = link"; continue }
    if ($donation.Value -match '^\w+://') {
        if ($donation.Value -notmatch '^https://[^/\s]+/\S+$') { $problems += "$($donation.Name): '$($donation.Value)' must be a full https:// link to your page" }
    }
    elseif ($donation.Value -notmatch '^[A-Za-z0-9_-]{25,100}$') {
        $problems += "$($donation.Name): '$($donation.Value)' doesn't look like a wallet address (letters and digits only, no spaces)"
    }
}

if ($problems.Count -gt 0) {
    throw "Fix these answers in release-settings.ini:`n" + (($problems | ForEach-Object { "  - $_" }) -join "`n")
}
if ($donations.Count -eq 0) { Write-Warning "No donation options filled in: the Support page won't ask for donations." }

# --- fill the answers in where they belong ---
Write-Host "Filling in release-settings.ini answers..." -ForegroundColor Cyan
Set-FileText $project ([regex]::Replace([System.IO.File]::ReadAllText($project), '<Version>[^<]*</Version>', "<Version>$version</Version>"))

$readme = [System.IO.File]::ReadAllText((Join-Path $rootDir "README.md"))
$previous = ([regex]'https://github\.com/([\w.-]+/[\w.-]+)/releases/latest').Match($readme).Groups[1].Value
$current = $repository.Substring('https://github.com/'.Length)
if ($previous -and $previous -ne $current) {
    Get-ChildItem $rootDir -Recurse -Filter *.md -File |
        Where-Object { $_.FullName -notmatch '\\(bin|obj|dist)\\' } |
        ForEach-Object { Set-FileText $_.FullName ([System.IO.File]::ReadAllText($_.FullName).Replace("github.com/$previous", "github.com/$current")) }
}

# The README's closing section is the page with every way to donate; GitHub's Sponsor
# button (.github\FUNDING.yml) opens Ko-fi, Buy Me a Coffee and that section.
$readmePath = Join-Path $rootDir "README.md"
$list = ($donations | ForEach-Object {
    $note = if ($_.Note) { " - $($_.Note)" } else { '' }
    if ($_.Value -match '^https://') { "- **$($_.Name)**: [$($_.Value -replace '^https://(www\.)?', '')]($($_.Value))$note" }
    else { "- **$($_.Name)**: ``$($_.Value)``$note" }
}) -join "`n"
$marked = [regex]'(?s)(<!-- donations:.*?-->\r?\n).*?(?=<!-- /donations -->)'
if (-not $marked.IsMatch([System.IO.File]::ReadAllText($readmePath))) { throw "README.md lost its <!-- donations: --> ... <!-- /donations --> markers; restore them around the donation list." }
# Every translation of the README (README.ru.md, ...) that keeps the markers gets the same list.
foreach ($file in Get-ChildItem $rootDir -Filter 'README*.md' -File) {
    $text = [System.IO.File]::ReadAllText($file.FullName)
    if ($marked.IsMatch($text)) { Set-FileText $file.FullName $marked.Replace($text, { param($m) $m.Groups[1].Value + $(if ($list) { "$list`n" } else { '' }) }, 1) }
}

$funding = Join-Path $rootDir ".github\FUNDING.yml"
$yaml = @("# Written by scripts/build-release.ps1 from release-settings.ini; edit that file instead.")
$custom = @()
foreach ($donation in $donations) {
    if ($donation.Value -match '^https://(?:www\.)?ko-fi\.com/([\w-]+)/?$') { $yaml += "ko_fi: $($Matches[1])" }
    elseif ($donation.Value -match '^https://(?:www\.)?buymeacoffee\.com/([\w-]+)/?$') { $yaml += "buy_me_a_coffee: $($Matches[1])" }
    elseif ($donation.Value -match '^https://') { $custom += $donation.Value }
}
if ($donations | Where-Object { $_.Value -notmatch '^https://' }) { $custom = @("$repository#donate") + $custom }
if ($custom) { $yaml += "custom: [" + (($custom | Select-Object -First 4 | ForEach-Object { "`"$_`"" }) -join ', ') + "]" }
if ($donations) { Set-FileText $funding (($yaml -join "`n") + "`n") }
elseif (Test-Path $funding) { Remove-Item $funding; Write-Host "  removed .github\FUNDING.yml (no donation options)" }

if ($thumbprint -and -not $pinned) {
    [System.IO.File]::WriteAllText($pin, "$thumbprint`r`n")
    Write-Host "  pinned the signing certificate in release-signing.sha1"
}

# --- build, test, sign ---
$dist = if ($OutputDir) { [System.IO.Path]::GetFullPath($OutputDir) } else { Join-Path $rootDir "dist" }
$publish = Join-Path $dist "publish"
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }

Write-Host "Building SuperDictate $version..." -ForegroundColor Cyan
& "$PSScriptRoot\build-app.ps1" -OutputDir $publish
$exe = Join-Path $publish "SuperDictate.exe"

Write-Host "Self-test..." -ForegroundColor Cyan
$report = & $exe --self-test all | Out-String
Write-Host $report
if ($LASTEXITCODE -ne 0) { throw "Self-test failed (FAIL lines above); not releasing." }
# A release must have transcribed real speech, so the engine checks can't be skipped here.
if ($report -match '(?m)^\s+SKIP ') {
    throw "The self-test skipped the speech engine (SKIP line above). Install this version (scripts\install-local.ps1 or the setup), let it set up the speech runtime and a model in Settings > Speech model, then run again."
}

if ($thumbprint) {
    $signtool = Get-Command signtool -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source
    if (-not $signtool) {
        $signtool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe" -ErrorAction SilentlyContinue |
            Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
    }
    if (-not $signtool) { throw "signtool.exe was not found. Install the Windows SDK (Signing Tools for Desktop Apps)." }

    Write-Host "Signing with pinned certificate $thumbprint..." -ForegroundColor Cyan
    & $signtool sign /sha1 $thumbprint /fd sha256 /tr http://timestamp.digicert.com /td sha256 $exe
    if ($LASTEXITCODE -ne 0) { throw "Signing failed (signtool output above)." }
    $signer = (Get-AuthenticodeSignature $exe).SignerCertificate
    if (-not $signer -or $signer.Thumbprint -ne $thumbprint) { throw "The exe is not signed by the pinned certificate." }
}
else {
    Write-Warning "Unsigned build: signing_thumbprint is empty in release-settings.ini. Customers will see a SmartScreen warning."
}

$setup = Join-Path $dist "SuperDictate-Setup-$version.exe"
Copy-Item $exe $setup -Force
Remove-Item $publish -Recurse -Force

$hash = (Get-FileHash -Algorithm SHA256 $setup).Hash.ToLowerInvariant()
"$hash  $(Split-Path $setup -Leaf)" | Set-Content -Encoding ascii (Join-Path $dist "SHA256SUMS.txt")

# What "Check for updates" reads. Publish it by committing it to the repository root
# on main after the GitHub release (tag v<version>) with the exe is live.
[ordered]@{
    version = $version
    url     = "$repository/releases/download/v$version/$(Split-Path $setup -Leaf)"
    sha256  = $hash
    notes   = ""
} | ConvertTo-Json | ForEach-Object { [System.IO.File]::WriteAllText((Join-Path $dist "update.json"), $_) }  # UTF-8 without BOM

Write-Host ""
Write-Host "Release ready:" -ForegroundColor Green
Write-Host "  $setup ($([math]::Round((Get-Item $setup).Length / 1MB)) MB)"
Write-Host "  SHA-256 $hash"
Write-Host "  Donation options on the Support page: $(if ($donations) { ($donations.Name) -join ', ' } else { 'none' })"
Write-Host "  update.json for the repository root (commit it after the GitHub release is published)"
