param([switch]$VerifyOnly)
$ErrorActionPreference = 'Stop'
$sourceRoot = Split-Path -Parent $PSScriptRoot
$pluginRoot = 'F:\kks\BepInEx\plugins'
$names = @('MainGameFpsDisplay', 'MainGameBlankMapAdd')
$reportRoot = Join-Path $sourceRoot '_diagnostics\fps-display'

function Assert-GameClosed {
    $running = @(Get-Process -Name 'KoikatsuSunshine','KoikatsuSunshine_VR' -ErrorAction SilentlyContinue)
    if ($running.Count) { throw ('Game is running; no deployment: ' + ($running.Id -join ',')) }
}

Write-Host ('FPS display deployment; verifyOnly=' + $VerifyOnly)
if (-not $VerifyOnly) { Assert-GameClosed }
$items = foreach ($name in $names) {
    $source = Join-Path $sourceRoot "$name\bin\Release\net472\$name.dll"
    $destination = Join-Path $pluginRoot "canon_plugins\$name\$name.dll"
    if (-not (Test-Path -LiteralPath $source)) { throw "Missing build: $source" }
    $duplicates = @(Get-ChildItem -LiteralPath $pluginRoot -Recurse -File -Filter "$name.dll" |
        Where-Object { $_.FullName -ne $destination })
    if ($duplicates.Count) { throw ('Duplicate DLLs: ' + ($duplicates.FullName -join ', ')) }
    [pscustomobject]@{Name=$name; Source=$source; Destination=$destination}
}
if (-not $VerifyOnly) {
    $backup = Join-Path $reportRoot ('backup-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
    New-Item -ItemType Directory -Path $backup -Force | Out-Null
    foreach ($item in $items) {
        Assert-GameClosed
        if (Test-Path -LiteralPath $item.Destination) {
            Copy-Item -LiteralPath $item.Destination -Destination (Join-Path $backup ($item.Name + '.dll'))
        }
        New-Item -ItemType Directory -Path (Split-Path -Parent $item.Destination) -Force | Out-Null
        Copy-Item -LiteralPath $item.Source -Destination $item.Destination -Force
        Write-Host ('Copied ' + $item.Name)
    }
}
$results = foreach ($item in $items) {
    $built = Get-Item -LiteralPath $item.Source
    $installed = Get-Item -LiteralPath $item.Destination
    $hash = (Get-FileHash -LiteralPath $built.FullName -Algorithm SHA256).Hash
    $match = $built.Length -eq $installed.Length -and $built.LastWriteTimeUtc -eq $installed.LastWriteTimeUtc -and
        $hash -eq (Get-FileHash -LiteralPath $installed.FullName -Algorithm SHA256).Hash
    $count = @(Get-ChildItem -LiteralPath $pluginRoot -Recurse -File -Filter ($item.Name + '.dll')).Count
    if (-not $match -or $count -ne 1) { throw ('DLL verification failed: ' + $item.Name) }
    [pscustomobject]@{Plugin=$item.Name; Bytes=$installed.Length; LastWriteTimeUtc=$installed.LastWriteTimeUtc.ToString('o'); SHA256=$hash; SizeTimeHashMatch=$match; LoadableDllCount=$count}
}
New-Item -ItemType Directory -Path $reportRoot -Force | Out-Null
$report = [pscustomobject]@{Time=(Get-Date).ToString('o'); Backup=$backup; Dlls=@($results)}
[System.IO.File]::WriteAllText((Join-Path $reportRoot 'deployment.json'), ($report | ConvertTo-Json -Depth 5), (New-Object System.Text.UTF8Encoding($false)))
$results | Format-Table -AutoSize
Write-Host 'Verified FPS display and PlaybackBar DLLs. In-game acceptance remains pending.'
