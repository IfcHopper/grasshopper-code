# Creates local food4Rhino ZIP and Windows/macOS Yak packages; never authenticates or publishes.
param([string]$YakPath = 'C:\Program Files\Rhino 8\System\Yak.exe')
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $YakPath -PathType Leaf)) { throw 'Set -YakPath to the Rhino 8 Yak executable.' }
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$project = Join-Path $root 'IfcHopper/IfcHopper.csproj'
$version = ([xml](Get-Content -LiteralPath $project)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
& (Join-Path $PSScriptRoot 'Package.ps1')
$artifacts = Join-Path $root 'artifacts'
$zip = Join-Path $artifacts "IfcHopper-$version.zip"
# Unique staging preserves prior packages and unrelated local artifacts.
$stage = Join-Path $artifacts ('yak-stage-' + [guid]::NewGuid().ToString('N'))
Expand-Archive -LiteralPath $zip -DestinationPath $stage
$content = Join-Path $stage 'IfcHopper'
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'manifest.yml') -Destination $content
Copy-Item -LiteralPath (Join-Path $root 'IfcHopper/Resources/IfcHopper.png') -Destination (Join-Path $content 'icon.png')
if (Get-ChildItem -LiteralPath $content -Recurse -File | Where-Object { $_.Name -in @('RhinoCommon.dll', 'Grasshopper.dll', 'GH_IO.dll') }) {
    throw 'Package contains host SDK assemblies; do not distribute.'
}
Push-Location $content
try {
    & $YakPath build --platform any --version $version
    if ($LASTEXITCODE -ne 0) { throw 'Yak build failed.' }
} finally { Pop-Location }
$packages = @(Get-ChildItem -LiteralPath $content -Filter '*.yak')
if ($packages.Count -ne 1) { throw 'Expected one Yak package.' }
Copy-Item -LiteralPath $packages[0].FullName -Destination $artifacts
# Keep staging for inspection; it is ignored with artifacts/.
Get-FileHash -Algorithm SHA256 -LiteralPath $zip, (Join-Path $artifacts $packages[0].Name) |
    Format-Table Path, Hash -AutoSize
