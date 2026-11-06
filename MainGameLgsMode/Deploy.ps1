param([switch]$VerifyOnly)
$ErrorActionPreference = 'Stop'
$pluginName = 'MainGameLgsMode'
$source = Join-Path $PSScriptRoot 'bin\Release\net472\MainGameLgsMode.dll'
$pluginRoot = 'F:\kks\BepInEx\plugins'
$destination = Join-Path $pluginRoot 'canon_plugins\MainGameLgsMode'
$dll = Join-Path $destination 'MainGameLgsMode.dll'
function Assert-Closed {
    if (Get-Process -Name 'KoikatsuSunshine','KoikatsuSunshine_VR' -ErrorAction SilentlyContinue) {
        throw 'Close KKS before deploying DLLs. No files have been deployed.'
    }
}
Write-Host ('MainGameLgsMode deployment; verifyOnly=' + $VerifyOnly)
if (-not $VerifyOnly) { Assert-Closed }
if (-not (Test-Path -LiteralPath $source)) { throw 'Build MainGameLgsMode first' }
$duplicates = @(Get-ChildItem -LiteralPath $pluginRoot -Recurse -Filter 'MainGameLgsMode.dll' -File | Where-Object { $_.FullName -ne $dll })
if ($duplicates.Count) { throw ('Duplicate DLLs: ' + ($duplicates.FullName -join ', ')) }
if (-not $VerifyOnly) {
    Assert-Closed
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    if (Test-Path -LiteralPath $dll) {
        $backup = Join-Path $PSScriptRoot ('runtime\backups\' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
        New-Item -ItemType Directory -Path $backup -Force | Out-Null
        Copy-Item -LiteralPath $dll -Destination $backup
    }
    Copy-Item -LiteralPath $source -Destination $dll -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LgsReceiver.lua') -Destination $destination -Force
    if (-not (Test-Path -LiteralPath (Join-Path $destination 'settings.json'))) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'settings.example.json') -Destination (Join-Path $destination 'settings.json')
    }
    $luaPath = $destination.Replace('\', '/')
    $script = "dofile([[$luaPath/LgsReceiver.lua]])`nOnEvent = CreateKksMKeyReceiver([[$luaPath/runtime/command.lua]])`n"
    [IO.File]::WriteAllText((Join-Path $destination 'LgsProfileScript.lua'), $script, [Text.UTF8Encoding]::new($false))
}
$built = Get-Item -LiteralPath $source
$installed = Get-Item -LiteralPath $dll
$hash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
if ($built.Length -ne $installed.Length -or $built.LastWriteTimeUtc -ne $installed.LastWriteTimeUtc -or
    $hash -ne (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash) { throw 'DLL size/time/hash verification failed' }
[pscustomobject]@{Plugin=$pluginName; Bytes=$installed.Length; SHA256=$hash; Installed=$dll; SizeTimeHashMatch=$true} | Format-List
Write-Host 'KKS files verified. LGS profile setup and in-game operation require separate verification.'
