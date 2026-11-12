# Builds IfcHopper and packs IfcHopper-<version>.zip (an IfcHopper folder with the plugin, its DLLs,
# LICENSE.txt, INSTALL.txt and THIRD-PARTY-NOTICES.txt) into artifacts/. The version comes from IfcHopper.csproj.
# Usage (repository root): powershell -File tools/Package/Package.ps1
param([ValidateSet('Debug', 'Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$project = Join-Path $root 'IfcHopper\IfcHopper.csproj'
$version = ([xml](Get-Content $project)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1

dotnet build $project -c $Configuration -v q -nologo
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

$artifacts = Join-Path $root 'artifacts'
if ($Configuration -eq 'Debug') { $artifacts = Join-Path $artifacts 'debug' }
$stage = Join-Path $artifacts ('zip-stage-' + [guid]::NewGuid().ToString('N'))
$folder = Join-Path $stage 'IfcHopper'
New-Item -ItemType Directory -Force $folder | Out-Null

$output = Join-Path $root "IfcHopper\bin\$Configuration\net8.0"
Copy-Item (Join-Path $output '*') $folder -Recurse -Exclude '*.pdb'
Copy-Item (Join-Path $root 'LICENSE') (Join-Path $folder 'LICENSE.txt')
Copy-Item (Join-Path $PSScriptRoot 'THIRD-PARTY-NOTICES.txt') $folder
[IO.File]::WriteAllText((Join-Path $folder 'INSTALL.txt'), ([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'INSTALL.txt')) -replace '\{version\}', $version))

# Windows tar writes zip paths with forward slashes, which Compress-Archive in Windows PowerShell does not.
$zip = Join-Path $artifacts "IfcHopper-$version.zip"
if (Test-Path $zip) { Remove-Item $zip }
Push-Location $stage
try { & (Join-Path $env:SystemRoot 'System32\tar.exe') -a -c -f $zip IfcHopper; if ($LASTEXITCODE -ne 0) { throw 'Zip failed.' } } finally { Pop-Location }
# Only remove this invocation's staging folder inside the selected artifacts directory.
$resolvedStage = (Resolve-Path -LiteralPath $stage).Path
$resolvedArtifacts = (Resolve-Path -LiteralPath $artifacts).Path
if (-not $resolvedStage.StartsWith($resolvedArtifacts + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Staging folder is outside artifacts.'
}
Remove-Item -LiteralPath $resolvedStage -Recurse -Force
Write-Output $zip
