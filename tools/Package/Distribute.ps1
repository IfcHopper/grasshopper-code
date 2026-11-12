# Creates local food4Rhino ZIP and Windows/macOS Yak packages; never authenticates or publishes.
param(
    [string]$YakPath = 'C:\Program Files\Rhino 8\System\Yak.exe',
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release'
)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $YakPath -PathType Leaf)) { throw 'Set -YakPath to the Rhino 8 Yak executable.' }
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$project = Join-Path $root 'IfcHopper/IfcHopper.csproj'
$version = ([xml](Get-Content -LiteralPath $project)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
& (Join-Path $PSScriptRoot 'Package.ps1') -Configuration $Configuration
$artifacts = Join-Path $root 'artifacts'
if ($Configuration -eq 'Debug') { $artifacts = Join-Path $artifacts 'debug' }
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
if ($packages[0].Name -like '*-any-any.yak') {
    # Yak 0.15.5 can exit successfully after Windows Application Control blocks assembly inspection.
    # In that case use the referenced SDK's minimum host tag, as normal inspection would, rather
    # than offering an unrestricted package. Keep this fallback until Yak reports inspection failure
    # with a failing exit code. This does not establish runtime compatibility or bypass assembly policy.
    $sdk = @(([xml](Get-Content -LiteralPath $project)).Project.ItemGroup.PackageReference |
        Where-Object { $_.Include -eq 'Grasshopper' })
    if ($sdk.Count -ne 1 -or $sdk[0].Version -notmatch '^(\d+)\.(\d+)\.') {
        throw 'Cannot determine a conservative Rhino tag from the Grasshopper SDK version.'
    }
    $taggedName = $packages[0].Name -replace '-any-any\.yak$', "-rh$($Matches[1])_$($Matches[2])-any.yak"
    $taggedPath = Join-Path $content $taggedName
    Move-Item -LiteralPath $packages[0].FullName -Destination $taggedPath
    Write-Warning "Yak could not infer a Rhino version; tagged from the referenced SDK: $taggedName. Host loading remains unverified."
    $packages = @(Get-Item -LiteralPath $taggedPath)
}
Copy-Item -LiteralPath $packages[0].FullName -Destination $artifacts
# Keep staging for inspection; it is ignored with artifacts/.
Get-FileHash -Algorithm SHA256 -LiteralPath $zip, (Join-Path $artifacts $packages[0].Name) |
    Format-Table Path, Hash -AutoSize
