<#
.SYNOPSIS
    Builds TrustTunnel GUI and packages the portable and installer versions.

.DESCRIPTION
    Uses only the C# compiler of .NET Framework 4.8 that ships with Windows.
    Output (dist\):
        portable\TrustTunnelGUI-<ver>-portable\      ready-to-run folder
        portable\TrustTunnelGUI-<ver>-portable.zip
        installer\TrustTunnelGUI-<ver>-setup.exe
    The TrustTunnel console client release files must be in client\.

.PARAMETER Dev
    Builds development executables with self-tests and screenshot rendering into dist\dev instead.
#>
param([switch]$Dev)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$fw = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$csc = Join-Path $fw 'csc.exe'
$dist = Join-Path $root 'dist'
$obj = Join-Path $dist 'obj'
$icon = Join-Path $root 'src\res\app_icon.ico'

$version = [regex]::Match((Get-Content (Join-Path $root 'src\App.cs') -Raw), 'AssemblyVersion\("(\d+\.\d+\.\d+)').Groups[1].Value
$clientFiles = 'trusttunnel_client.exe', 'setup_wizard.exe', 'wintun.dll', 'LICENSE.txt', 'WINTUN_LICENSE.txt'

$references = @(
    "$fw\WPF\PresentationFramework.dll", "$fw\WPF\PresentationCore.dll", "$fw\WPF\WindowsBase.dll",
    "$fw\System.Xaml.dll", "$fw\System.Windows.Forms.dll", "$fw\System.Drawing.dll",
    "$fw\System.Web.Extensions.dll", "$fw\System.Security.dll",
    "$fw\System.IO.Compression.dll", "$fw\System.IO.Compression.FileSystem.dll",
    'System.Core.dll', 'System.Xml.dll'
) | ForEach-Object { "/r:$_" }

$appSources = Get-ChildItem (Join-Path $root 'src') -Filter *.cs | ForEach-Object { $_.FullName }
$setupSources = @((Join-Path $root 'installer\Setup.cs'), (Join-Path $root 'src\Theme.cs'))

function Invoke-Csc([string]$Out, [string]$Manifest, [string[]]$Sources, [string[]]$Resources, [string]$Define) {
    New-Item -ItemType Directory -Force (Split-Path $Out) | Out-Null
    $cscArgs = @('/nologo', '/target:winexe', '/platform:anycpu', '/optimize+', '/nowarn:0649', "/out:$Out", "/win32icon:$icon", "/win32manifest:$Manifest") + $references
    if ($Define) { $cscArgs += "/define:$Define" }
    foreach ($r in $Resources) { $cscArgs += "/resource:$r" }
    $cscArgs += $Sources
    & $csc @cscArgs
    if ($LASTEXITCODE -ne 0) { throw "Compilation of $(Split-Path $Out -Leaf) failed" }
    Write-Host "  built $Out"
}

function New-Zip([string]$Source, [string]$Zip, [bool]$IncludeBaseDirectory) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    if (Test-Path $Zip) { Remove-Item $Zip -Force }
    [System.IO.Compression.ZipFile]::CreateFromDirectory($Source, $Zip, [System.IO.Compression.CompressionLevel]::Optimal, $IncludeBaseDirectory)
}

function Reset-Directory([string]$Path) {
    if (Test-Path $Path) { Remove-Item $Path -Recurse -Force }
    New-Item -ItemType Directory -Force $Path | Out-Null
}

$iconResource = "$icon,app_icon.ico"

if ($Dev) {
    Write-Host "Development build $version"
    $devDir = Join-Path $dist 'dev'
    Invoke-Csc (Join-Path $devDir 'TrustTunnelGUI.dev.exe') (Join-Path $root 'src\app.manifest') $appSources @($iconResource) 'DEVTOOLS'
    Invoke-Csc (Join-Path $devDir 'Setup.dev.exe') (Join-Path $root 'src\app.manifest') $setupSources @($iconResource) 'DEVTOOLS'
    return
}

Write-Host "TrustTunnel GUI $version"
$clientDir = Join-Path $root 'client'
foreach ($f in $clientFiles) {
    if (-not (Test-Path (Join-Path $clientDir $f))) {
        throw "client\$f is missing. Put the files of the TrustTunnel client release (trusttunnel_client-*-windows-x86_64) into client\."
    }
}

# 1. Application
$appExe = Join-Path $obj 'TrustTunnelGUI.exe'
Invoke-Csc $appExe (Join-Path $root 'src\app.manifest') $appSources @($iconResource) $null

# 2. Portable: app + client + "portable" marker (data stays in .\data)
$name = "TrustTunnelGUI-$version-portable"
$portableRoot = Join-Path $dist 'portable'
$portableDir = Join-Path $portableRoot $name
Reset-Directory $portableRoot
New-Item -ItemType Directory -Force $portableDir | Out-Null
Copy-Item $appExe $portableDir
foreach ($f in $clientFiles) { Copy-Item (Join-Path $clientDir $f) $portableDir }
Set-Content (Join-Path $portableDir 'portable') 'Settings and logs of this copy are stored in the data folder next to TrustTunnelGUI.exe.' -Encoding ASCII
New-Zip $portableDir (Join-Path $portableRoot "$name.zip") $true
Write-Host "  packed $portableRoot\$name.zip"

# 3. Installer: the payload (app + client) is embedded into the setup executable
$payloadDir = Join-Path $obj 'payload'
Reset-Directory $payloadDir
Copy-Item $appExe $payloadDir
foreach ($f in $clientFiles) { Copy-Item (Join-Path $clientDir $f) $payloadDir }
$payloadZip = Join-Path $obj 'payload.zip'
New-Zip $payloadDir $payloadZip $false

$installerDir = Join-Path $dist 'installer'
Reset-Directory $installerDir
Invoke-Csc (Join-Path $installerDir "TrustTunnelGUI-$version-setup.exe") (Join-Path $root 'installer\setup.manifest') $setupSources @($iconResource, "$payloadZip,payload.zip") $null

Write-Host ""
Get-ChildItem $portableRoot, $installerDir -File | ForEach-Object { '{0,-45} {1,8:N1} MB' -f $_.Name, ($_.Length / 1MB) }
