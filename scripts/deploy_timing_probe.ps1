param([switch]$ArmOnce)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot 'deploy_perf_reductions.ps1') -Names @('MainGameFrameTimingProbe') -SkipClothConfig -ReportName 'timing-probe-deployment.json'
if ($ArmOnce) {
    if (@(Get-Process -Name KoikatsuSunshine,KoikatsuSunshine_VR -ErrorAction SilentlyContinue).Count) { throw 'Game is running; config was not modified.' }
    $path = 'F:\kks\BepInEx\config\com.kks.maingame.frametimingprobe.cfg'
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    $text = ''
    if (Test-Path -LiteralPath $path) {
        $text = [IO.File]::ReadAllText($path, $utf8)
        Copy-Item -LiteralPath $path -Destination (Join-Path $root ('_diagnostics\perf-reduction-20261027\probe-config-' + (Get-Date -Format yyyyMMdd-HHmmss-fff) + '.cfg'))
    }
    $section = [regex]::Match($text, '(?ms)^\[Capture\][^\r\n]*\r?\n(?<body>.*?)(?=^\[|\z)')
    $body = if ($section.Success) { $section.Groups['body'].Value } else { '' }
    $settings = [ordered]@{ Remaining='1'; HSceneOnly='true'; WarmupSeconds='10'; BaselineSeconds='5'; DurationSeconds='15' }
    foreach ($key in $settings.Keys) {
        $pattern = '(?m)^' + [regex]::Escape($key) + '\s*=.*$'
        $line = $key + ' = ' + $settings[$key]
        if ([regex]::IsMatch($body, $pattern)) { $body = [regex]::Replace($body, $pattern, $line) }
        else { $body += $line + "`r`n" }
    }
    $replacement = "[Capture]`r`n" + $body
    if ($section.Success) { $text = $text.Substring(0, $section.Index) + $replacement + $text.Substring($section.Index + $section.Length) }
    else { $text += "`r`n" + $replacement }
    [IO.File]::WriteAllText($path, $text, $utf8)
    Write-Host 'Armed once: active H scene -> warmup 10s -> baseline 5s -> instrumented 15s -> Remaining=0. Game was not launched.'
}
