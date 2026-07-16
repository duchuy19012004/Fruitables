[CmdletBinding()]
param(
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$agentsRoot = Join-Path $repoRoot '.agents'
$d2Root = Join-Path $agentsRoot 'tools\d2'
$d2Exe = Join-Path $d2Root 'd2.exe'

Write-Host 'Installing project-local Node tools...'
$env:PUPPETEER_SKIP_DOWNLOAD = '1'
Push-Location $agentsRoot
try {
    & npm ci
    if ($LASTEXITCODE -ne 0) { throw 'npm ci failed for diagram tools.' }
}
finally { Pop-Location }

$bpmnRoot = Join-Path $agentsRoot 'skills\bpmn\engine'
Write-Host 'Installing BPMN engine dependencies...'
Push-Location $bpmnRoot
try {
    & npm ci
    if ($LASTEXITCODE -ne 0) { throw 'npm ci failed for the BPMN engine.' }
}
finally { Pop-Location }

if ($Force -or -not (Test-Path -LiteralPath $d2Exe)) {
    Write-Host 'Installing the official D2 Windows binary locally...'
    $headers = @{ 'User-Agent' = 'Fruitables-Codex-Setup' }
    $d2Version = 'v0.7.1'
    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/terrastruct/d2/releases/tags/$d2Version" -Headers $headers
    $arch = if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq 'Arm64') { 'arm64' } else { 'amd64' }
    $asset = $release.assets | Where-Object { $_.name -eq "d2-$($release.tag_name)-windows-$arch.tar.gz" } | Select-Object -First 1
    if (-not $asset) { throw "No D2 Windows $arch archive was found in release $($release.tag_name)." }

    $tempBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    $tempRoot = Join-Path $tempBase ("fruitables-d2-" + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null
    try {
        $archive = Join-Path $tempRoot $asset.name
        Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $archive -Headers $headers
        if (-not $asset.digest -or -not $asset.digest.StartsWith('sha256:')) {
            throw 'The pinned D2 release asset did not provide a SHA-256 digest.'
        }
        $expectedHash = $asset.digest.Substring(7).ToLowerInvariant()
        $actualHash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actualHash -ne $expectedHash) { throw 'D2 archive SHA-256 checksum mismatch.' }
        & tar -xzf $archive -C $tempRoot
        if ($LASTEXITCODE -ne 0) { throw 'Could not extract the D2 archive.' }
        $downloaded = Get-ChildItem -LiteralPath $tempRoot -Recurse -File -Filter 'd2.exe' | Select-Object -First 1
        if (-not $downloaded) { throw 'The D2 archive did not contain d2.exe.' }
        New-Item -ItemType Directory -Force -Path $d2Root | Out-Null
        Copy-Item -LiteralPath $downloaded.FullName -Destination $d2Exe -Force
        Set-Content -LiteralPath (Join-Path $d2Root 'VERSION') -Value $release.tag_name -Encoding UTF8
    }
    finally {
        $resolvedTemp = [IO.Path]::GetFullPath($tempRoot)
        if ($resolvedTemp.StartsWith($tempBase, [StringComparison]::OrdinalIgnoreCase) -and
            (Split-Path $resolvedTemp -Leaf).StartsWith('fruitables-d2-')) {
            Remove-Item -LiteralPath $resolvedTemp -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

Write-Host 'Diagram tool setup complete.'
