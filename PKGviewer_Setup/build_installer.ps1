<#
  Builds PKG Explorer's MSI end to end: publish, then package.

    .\build_installer.ps1                 # 1.0.15, Release
    .\build_installer.ps1 -Version 1.0.16

  Needs the WiX tool once per machine:   dotnet tool install --global wix
  (undo with:                            dotnet tool uninstall --global wix)

  Output: ..\PKGviewer_Setup_<version>.msi, beside the old MSIs.

  Framework-dependent on purpose.  A self-contained publish measured 152 MB
  against 19 MB for this one; the trade is that the target machine needs the
  x86 .NET Desktop Runtime, which the MSI checks for and explains rather than
  letting the app fail to start.

  Written 2026-09-13 by session "PKG Explorer codebase review".
#>
[CmdletBinding()]
param(
    [string]$Version       = "1.0.15",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$here    = Split-Path -Parent $MyInvocation.MyCommand.Path
$proj    = Join-Path $here "..\LoadAnything\PKGexplorer.vbproj"
$publish = Join-Path $here "obj\publish"
$msi     = Join-Path $here "..\PKGviewer_Setup_$Version.msi"

# .Source, not the CommandInfo itself - Test-Path on a CommandInfo stringifies
# to the command NAME, not its path, and reports false even when it is there.
$wix = (Get-Command wix -ErrorAction SilentlyContinue).Source
if (-not $wix) { $wix = Join-Path $env:USERPROFILE ".dotnet\tools\wix.exe" }
if (-not (Test-Path $wix)) {
    throw "WiX not found. Install it with:  dotnet tool install --global wix"
}

Write-Host "==> publishing $Configuration win-x86 (framework-dependent)" -ForegroundColor Cyan
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
dotnet publish $proj `
    -c $Configuration -r win-x86 --self-contained false `
    -p:Platform=x86 -p:DebugType=none `
    -o $publish --nologo -v:quiet
if ($LASTEXITCODE -ne 0) { throw "publish failed" }

# The exe has to be there, and nothing that cannot run on .NET 8 should be.
foreach ($must in @("PKGviewer.exe", "PKGviewer.dll", "PKGviewer.runtimeconfig.json")) {
    if (-not (Test-Path (Join-Path $publish $must))) { throw "publish is missing $must" }
}
foreach ($gone in @("FbxSDK.dll", "SlimDX.dll")) {
    if (Test-Path (Join-Path $publish $gone)) {
        throw "$gone is mixed-mode C++/CLI and cannot load on .NET 8 - it must not ship"
    }
}

$n = (Get-ChildItem $publish -Recurse -File | Measure-Object Length -Sum)
Write-Host ("    {0} files, {1:N1} MB" -f $n.Count, ($n.Sum / 1MB))

Write-Host "==> building MSI $Version" -ForegroundColor Cyan
& $wix build (Join-Path $here "PKGExplorer.wxs") `
    -d "PublishDir=$publish" `
    -d "ProductVersion=$Version" `
    -arch x86 `
    -o $msi
if ($LASTEXITCODE -ne 0) { throw "wix build failed" }

$size = (Get-Item $msi).Length / 1MB
Write-Host ("==> {0}  ({1:N1} MB)" -f $msi, $size) -ForegroundColor Green
