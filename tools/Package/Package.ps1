# Builds IfcHopper in Release and packs IfcHopper-<version>.zip (an IfcHopper folder with the plugin, its DLLs,
# LICENSE.txt, INSTALL.txt and THIRD-PARTY-NOTICES.txt) into artifacts/. The version comes from IfcHopper.csproj.
# Usage (repository root): powershell -File tools/Package/Package.ps1
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$project = Join-Path $root 'IfcHopper\IfcHopper.csproj'
$version = ([xml](Get-Content $project)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1

dotnet build $project -c Release -v q -nologo
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

$artifacts = Join-Path $root 'artifacts'
$stage = Join-Path $artifacts 'stage'
$folder = Join-Path $stage 'IfcHopper'
if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
New-Item -ItemType Directory -Force $folder | Out-Null

$output = Join-Path $root 'IfcHopper\bin\Release\net8.0'
Copy-Item (Join-Path $output '*') $folder -Recurse -Exclude '*.pdb'
Copy-Item (Join-Path $root 'LICENSE') (Join-Path $folder 'LICENSE.txt')
Copy-Item (Join-Path $PSScriptRoot 'THIRD-PARTY-NOTICES.txt') $folder
[IO.File]::WriteAllText((Join-Path $folder 'INSTALL.txt'), ([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'INSTALL.txt')) -replace '\{version\}', $version))

# Windows tar writes zip paths with forward slashes, which Compress-Archive in Windows PowerShell does not.
$zip = Join-Path $artifacts "IfcHopper-$version.zip"
if (Test-Path $zip) { Remove-Item $zip }
Push-Location $stage
try { & (Join-Path $env:SystemRoot 'System32\tar.exe') -a -c -f $zip IfcHopper; if ($LASTEXITCODE -ne 0) { throw 'Zip failed.' } } finally { Pop-Location }
Remove-Item -Recurse -Force $stage
Write-Output $zip
