param([switch]$VerifyOnly,
    [string[]]$Names = @('MainGameClothColliderCards', 'MainGameBlankMapAdd', 'MainGameKaleidoscope', 'MainGameDollMode', 'MainGameCircularDisplacement'),
    [switch]$SkipClothConfig, [string]$ReportName = 'deployment.json')
$ErrorActionPreference = 'Stop'
$sourceRoot = Split-Path -Parent $PSScriptRoot
$pluginRoot = 'F:\kks\BepInEx\plugins'
$deployRoot = Join-Path $pluginRoot 'canon_plugins'
$evidence = Join-Path $sourceRoot '_diagnostics\perf-reduction-20261027'
$configPath = 'F:\kks\BepInEx\config\canon.kks.clothcollidercards.cfg'
$utf8 = New-Object System.Text.UTF8Encoding($false)

function Assert-GameClosed {
    $running = @(Get-Process -Name 'KoikatsuSunshine','KoikatsuSunshine_VR' -ErrorAction SilentlyContinue)
    if ($running.Count) { throw ('Game is running; no deployment: ' + ($running.Id -join ',')) }
}

Write-Host ('Performance reduction deployment; verifyOnly=' + $VerifyOnly)
if (-not $VerifyOnly) { Assert-GameClosed }
$items = @()
foreach ($name in $names) {
    # Always use this plugin's own output, never a dependency copy in another bin directory.
    $source = Join-Path $sourceRoot "$name\bin\Release\net472\$name.dll"
    $destination = Join-Path $deployRoot "$name\$name.dll"
    if (-not (Test-Path -LiteralPath $source)) { throw "Missing build: $source" }
    $duplicates = @(Get-ChildItem -LiteralPath $pluginRoot -Recurse -File -Filter "$name.dll" |
        Where-Object { $_.FullName -ne $destination })
    if ($duplicates.Count) { throw ('Duplicate loadable DLLs: ' + ($duplicates.FullName -join ', ')) }
    $items += [pscustomobject]@{ Name=$name; Source=$source; Destination=$destination }
}

if (-not $VerifyOnly) {
    $backup = Join-Path $evidence ('backup-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
    New-Item -ItemType Directory -Path $backup -Force | Out-Null
    foreach ($item in $items) {
        Assert-GameClosed
        if (Test-Path -LiteralPath $item.Destination) {
            Copy-Item -LiteralPath $item.Destination -Destination (Join-Path $backup ($item.Name + '.dll'))
        }
        $parent = Split-Path -Parent $item.Destination
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
        Copy-Item -LiteralPath $item.Source -Destination $item.Destination -Force
        Write-Host ('Copied ' + $item.Name)
    }
    Assert-GameClosed
    if (-not $SkipClothConfig) {
    $config = ''
    if (Test-Path -LiteralPath $configPath) {
        Copy-Item -LiteralPath $configPath -Destination (Join-Path $backup 'canon.kks.clothcollidercards.cfg')
        $config = [System.IO.File]::ReadAllText($configPath, $utf8)
    }
    $section = [regex]::Match($config, '(?ms)^\[General\][^\r\n]*\r?\n(?<body>.*?)(?=^\[|\z)')
    if ($section.Success) {
        $body = $section.Groups['body'].Value
        if ([regex]::IsMatch($body, '(?m)^Enabled\s*=')) {
            $body = [regex]::Replace($body, '(?m)^Enabled\s*=.*$', 'Enabled = false')
        } else { $body += "`r`nEnabled = false`r`n" }
        $replacement = "[General]`r`n" + $body
        $config = $config.Substring(0, $section.Index) + $replacement + $config.Substring($section.Index + $section.Length)
    } else { $config += "`r`n[General]`r`n`r`n# Restart required after changing Enabled.`r`nEnabled = false`r`n" }
    [System.IO.File]::WriteAllText($configPath, $config, $utf8)
    }
}

$results = @()
foreach ($item in $items) {
    $built = Get-Item -LiteralPath $item.Source
    $installed = Get-Item -LiteralPath $item.Destination
    $builtHash = (Get-FileHash -LiteralPath $built.FullName -Algorithm SHA256).Hash
    $installedHash = (Get-FileHash -LiteralPath $installed.FullName -Algorithm SHA256).Hash
    $match = $built.Length -eq $installed.Length -and $built.LastWriteTimeUtc -eq $installed.LastWriteTimeUtc -and $builtHash -eq $installedHash
    $count = @(Get-ChildItem -LiteralPath $pluginRoot -Recurse -File -Filter ($item.Name + '.dll')).Count
    if (-not $match -or $count -ne 1) { throw ('Installed DLL verification failed: ' + $item.Name) }
    $results += [pscustomobject]@{
        Plugin=$item.Name; Source=$built.FullName; Destination=$installed.FullName
        Bytes=$installed.Length; LastWriteTimeUtc=$installed.LastWriteTimeUtc.ToString('o')
        SHA256=$installedHash; SizeTimeHashMatch=$match; LoadableDllCount=$count
    }
}
if (-not $SkipClothConfig) {
    $config = [System.IO.File]::ReadAllText($configPath, $utf8)
    if (-not [regex]::IsMatch($config, '(?ms)^\[General\][^\r\n]*\r?\n(?:(?!^\[).)*?^Enabled\s*=\s*false\s*$')) {
        throw 'Cloth startup Enabled=false verification failed'
    }
}
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
$report = [pscustomobject]@{ Time=(Get-Date).ToString('o'); ClothConfigChecked=(-not $SkipClothConfig); Backup=$backup; Dlls=$results }
[System.IO.File]::WriteAllText((Join-Path $evidence $ReportName), ($report | ConvertTo-Json -Depth 5), $utf8)
$results | Select-Object Plugin,Bytes,SizeTimeHashMatch,LoadableDllCount | Format-Table -AutoSize
Write-Host ('Verified ' + $results.Count + ' DLLs. No game was launched.')
