# Fetch unchanged public examples; tests never download files themselves.
$ErrorActionPreference = 'Stop'
$fixtureRoot = Join-Path $PSScriptRoot '../IfcHopper.Tests/Fixtures'
$manifest = Get-Content -LiteralPath (Join-Path $fixtureRoot 'manifest.json') -Raw | ConvertFrom-Json
$cache = Join-Path $fixtureRoot 'cache'
New-Item -ItemType Directory -Force -Path $cache | Out-Null
foreach ($file in $manifest.files) {
    $destination = Join-Path $cache ([IO.Path]::GetFileName($file.path))
    if ((Test-Path -LiteralPath $destination) -and
        (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -eq $file.sha256) { continue }
    $temporary = "$destination.download"
    try {
        $url = "https://raw.githubusercontent.com/$($manifest.repository)/$($manifest.commit)/$($file.path)"
        Invoke-WebRequest -UseBasicParsing -Uri $url -OutFile $temporary -TimeoutSec 60
        if ((Get-FileHash -LiteralPath $temporary -Algorithm SHA256).Hash -ne $file.sha256) {
            throw "Checksum mismatch: $($file.path)"
        }
        Move-Item -LiteralPath $temporary -Destination $destination -Force
    }
    finally {
        if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary }
    }
}
Write-Output "Public fixtures verified at $($manifest.commit)."
