# Makes the files for a GitHub release in dist\<version>\ (needs Inno Setup 6 for the installer):
#   ReadyLauncher-<version>-Setup.exe     the installer (for this Windows account only, no administrator needed)
#   ReadyLauncher-<version>-Portable.zip  ReadyLauncher.exe with portable.txt: unzip anywhere and run it
#   ReadyLauncher.exe                     the program on its own: what Settings > Check for updates downloads
# -SkipBuild uses bin\ReadyLauncher.exe as it is; -TestInstaller also makes a test installer (another name, ID and
# startup key) for trying the installer out without touching a real install.
param([switch]$SkipBuild, [switch]$TestInstaller)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
if (-not $SkipBuild) { & (Join-Path $root 'build.ps1') | Out-Host }
$exe = Join-Path $root 'bin\ReadyLauncher.exe'
$v = [Version](Get-Item $exe).VersionInfo.FileVersion
$version = '{0}.{1}.{2}' -f $v.Major, $v.Minor, [Math]::Max(0, $v.Build)

$iscc = @("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe") |
  Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6 is needed for the installer: https://jrsoftware.org/isinfo.php' }

$out = Join-Path $root "dist\$version"
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory $out | Out-Null
$iss = Join-Path $root 'installer\ReadyLauncher.iss'

# The installer
& $iscc /Qp "/DAppVersion=$version" "/DSourceExe=$exe" "/O$out" "/FReadyLauncher-$version-Setup" $iss | Out-Host
if ($LASTEXITCODE -ne 0) { throw "Inno Setup stopped with code $LASTEXITCODE" }
if ($TestInstaller) {
  & $iscc /Qp "/DAppVersion=$version" "/DSourceExe=$exe" /DTestBuild "/O$(Join-Path $root 'dist\test')" "/FReadyLauncher-$version-Setup-TEST" $iss | Out-Host
  if ($LASTEXITCODE -ne 0) { throw "Inno Setup stopped with code $LASTEXITCODE (test installer)" }
}

# The portable zip: the exe and portable.txt (with Windows line endings, for Notepad)
$stage = Join-Path $out 'portable'
New-Item -ItemType Directory $stage | Out-Null
Copy-Item $exe $stage
$note = [IO.File]::ReadAllText((Join-Path $root 'installer\portable.txt')) -replace "`r?`n", "`r`n"
[IO.File]::WriteAllText((Join-Path $stage 'portable.txt'), $note, (New-Object Text.UTF8Encoding $false))
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($stage, (Join-Path $out "ReadyLauncher-$version-Portable.zip"), [IO.Compression.CompressionLevel]::Optimal, $false)
Remove-Item $stage -Recurse -Force

# The program on its own, for Check for updates
Copy-Item $exe $out

Get-ChildItem $out | ForEach-Object { '{0,-36} {1,10:N0} bytes  SHA256 {2}' -f $_.Name, $_.Length, (Get-FileHash $_.FullName).Hash }
