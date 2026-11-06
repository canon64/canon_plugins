param([ValidateSet('MainGameMagicPaint','MainGameVoiceFaceEventBridge','MainGameCandle','MainGameBlankMapAdd')][string[]]$Names = @('MainGameMagicPaint','MainGameVoiceFaceEventBridge','MainGameCandle','MainGameBlankMapAdd'))
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$pluginRoot = 'F:\kks\BepInEx\plugins'
Write-Host '[candle] deployment started'
function Assert-GameStopped {
    $running = @(Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -match '^(KoikatsuSunshine(?:_VR)?|CharaStudio)$' })
    if ($running.Count) { throw "Deployment stopped: game is running ($($running.ProcessName -join ', '))." }
}
Assert-GameStopped
$jobs = foreach ($name in $Names) {
    $source = Join-Path $repo "$name\bin\Release\net472\$name.dll"
    $destination = Join-Path $pluginRoot "canon_plugins\$name\$name.dll"
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "Missing canonical build: $source" }
    if ([Reflection.AssemblyName]::GetAssemblyName($source).Name -ne $name) { throw "Wrong assembly: $source" }
    $duplicates = @(Get-ChildItem -LiteralPath $pluginRoot -Filter "$name.dll" -Recurse -File | Where-Object { $_.FullName -ne $destination })
    if ($duplicates.Count) { throw "Duplicate DLL: $($duplicates.FullName -join ', ')" }
    [pscustomobject]@{ Name=$name; Source=$source; Destination=$destination; SourceHash=(Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash }
}
$reportDir = Join-Path $repo ('reports\candle-deploy-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
$results = foreach ($job in $jobs) {
    Assert-GameStopped
    $oldHash = $null
    if (Test-Path -LiteralPath $job.Destination) {
        $oldHash = (Get-FileHash -LiteralPath $job.Destination -Algorithm SHA256).Hash
        Copy-Item -LiteralPath $job.Destination -Destination (Join-Path $reportDir ($job.Name + '.previous.dll'))
    }
    if ((Get-FileHash -LiteralPath $job.Source -Algorithm SHA256).Hash -ne $job.SourceHash) { throw "Build changed during deployment: $($job.Source)" }
    New-Item -ItemType Directory -Path (Split-Path $job.Destination -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $job.Source -Destination $job.Destination -Force
    $sourceItem = Get-Item -LiteralPath $job.Source
    $installed = Get-Item -LiteralPath $job.Destination
    $hash = (Get-FileHash -LiteralPath $job.Destination -Algorithm SHA256).Hash
    if ($hash -ne $job.SourceHash -or $sourceItem.Length -ne $installed.Length -or $sourceItem.LastWriteTimeUtc -ne $installed.LastWriteTimeUtc) { throw "Installed artifact does not match: $($job.Destination)" }
    $copies = @(Get-ChildItem -LiteralPath $pluginRoot -Filter ($job.Name + '.dll') -Recurse -File)
    if ($copies.Count -ne 1) { throw "Unexpected duplicate after deployment: $($job.Name)" }
    [pscustomobject]@{Name=$job.Name;Source=$job.Source;Destination=$job.Destination;Length=$installed.Length;LastWriteTimeUtc=$installed.LastWriteTimeUtc.ToString('o');SHA256=$hash;PreviousSHA256=$oldHash;Copies=$copies.Count}
}
$report = Join-Path $reportDir 'deployment.json'
$results | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $report -Encoding UTF8
$results | Format-Table Name,Length,Copies,SHA256 -AutoSize
Write-Host "[candle] deployment verified: $report"
