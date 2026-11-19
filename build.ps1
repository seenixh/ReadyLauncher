# Builds ReadyLauncher.exe with the C# compiler that ships with Windows (.NET Framework 4.8).
# No SDK or Visual Studio needed.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$fw   = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$wpf  = Join-Path $fw 'WPF'
$out  = Join-Path $root 'bin'
New-Item -ItemType Directory -Force $out | Out-Null

$ico = Join-Path $root 'app.ico'
if (-not (Test-Path $ico)) { & (Join-Path $root 'make-icon.ps1') -Path $ico }

$refs = @(
    "$wpf\PresentationFramework.dll", "$wpf\PresentationCore.dll", "$wpf\WindowsBase.dll",
    "$fw\System.Xaml.dll", "$fw\System.Windows.Forms.dll", "$fw\System.Drawing.dll",
    "$fw\System.Xml.dll", "$fw\System.dll", "$fw\System.Core.dll",
    "$fw\System.Management.dll", "$fw\System.Web.Extensions.dll", "$fw\System.ServiceProcess.dll"
) | ForEach-Object { "/r:$_" }

$src = Get-ChildItem (Join-Path $root 'src') -Filter *.cs | ForEach-Object { $_.FullName }

& (Join-Path $fw 'csc.exe') /nologo /target:winexe /optimize+ /platform:anycpu `
    "/out:$out\ReadyLauncher.exe" "/win32icon:$ico" "/win32manifest:$root\app.manifest" `
    "/resource:$ico,ReadyLauncher.app.ico" $refs $src
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
Write-Host "Built $out\ReadyLauncher.exe"
