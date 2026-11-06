$ErrorActionPreference = 'Stop'
Write-Host '[shoulder] diagnostic deployment starting'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$game = 'F:\kks'
$plugins = Join-Path $game 'BepInEx\plugins'
$names = @('MainGirlHipHijack', 'MainGirlShoulderIkStabilizer')
function Assert-GameStopped {
    $running = @(Get-Process -Name 'KoikatsuSunshine','KoikatsuSunshine_VR' -ErrorAction SilentlyContinue)
    if ($running.Count -ne 0) { throw ('Close KKS before deployment. Running PID: ' + ($running.Id -join ',')) }
}
Assert-GameStopped
$cecil = Join-Path $game 'BepInEx\core\Mono.Cecil.dll'
Add-Type -Path $cecil
$hipPath = Join-Path $repo 'MainGirlHipHijack\bin\Release\net472\MainGirlHipHijack.dll'
$shoulderPath = Join-Path $repo 'MainGirlShoulderIkStabilizer\bin\Release\net472\MainGirlShoulderIkStabilizer.dll'
$hipAssembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($hipPath)
$shoulderAssembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($shoulderPath)
try {
    $methods = @($hipAssembly.MainModule.GetTypes() | ForEach-Object { $_.Methods } | ForEach-Object FullName)
    $refs = @($shoulderAssembly.MainModule.GetMemberReferences() | Where-Object {
        $_ -is [Mono.Cecil.MethodReference] -and $_.DeclaringType.Scope.Name -eq 'MainGirlHipHijack'
    })
    foreach ($ref in $refs) {
        if ($methods -notcontains $ref.FullName) { throw ('Missing HipHijack API: ' + $ref.FullName) }
    }
    Write-Host "[shoulder] built HipHijack API references verified: $($refs.Count)"
} finally { $hipAssembly.Dispose(); $shoulderAssembly.Dispose() }
$backup = Join-Path $repo ('output\shoulder-verification\deploy-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$jobs = foreach ($name in $names) {
    $source = Join-Path $repo "$name\bin\Release\net472\$name.dll"
    $target = Join-Path $plugins "canon_plugins\$name\$name.dll"
    if (!(Test-Path -LiteralPath $source)) { throw "Missing own build: $source" }
    $duplicates = @(rg --files --hidden --no-ignore $plugins -g "$name.dll")
    if ($duplicates.Count -gt 1 -or ($duplicates.Count -eq 1 -and [IO.Path]::GetFullPath($duplicates[0]) -ne $target)) {
        throw "Unexpected plugin copy: $($duplicates -join ', ')"
    }
    [pscustomobject]@{Name=$name; Source=$source; Target=$target}
}
New-Item -ItemType Directory -Force -Path $backup | Out-Null
$manifest = foreach ($job in $jobs) {
    Assert-GameStopped
    if (Test-Path -LiteralPath $job.Target) { Copy-Item -LiteralPath $job.Target -Destination (Join-Path $backup ($job.Name + '.dll')) }
    Copy-Item -LiteralPath $job.Source -Destination $job.Target -Force
    $pdb = [IO.Path]::ChangeExtension($job.Source, '.pdb')
    if (Test-Path -LiteralPath $pdb) { Copy-Item -LiteralPath $pdb -Destination ([IO.Path]::ChangeExtension($job.Target,'.pdb')) -Force }
    $src = Get-Item -LiteralPath $job.Source
    $dst = Get-Item -LiteralPath $job.Target
    $srcHash = (Get-FileHash -LiteralPath $job.Source -Algorithm SHA256).Hash
    $dstHash = (Get-FileHash -LiteralPath $job.Target -Algorithm SHA256).Hash
    if ($srcHash -ne $dstHash -or $src.Length -ne $dst.Length -or $src.LastWriteTimeUtc -ne $dst.LastWriteTimeUtc) {
        throw "Deployed file mismatch: $($job.Name)"
    }
    Write-Host "[shoulder] verified $($job.Name): $($dst.Length) bytes SHA256=$dstHash"
    [pscustomobject]@{Plugin=$job.Name; Source=$job.Source; Target=$job.Target; Bytes=$dst.Length; ModifiedUtc=$dst.LastWriteTimeUtc.ToString('o'); SHA256=$dstHash}
}
Assert-GameStopped
$cfg = Join-Path $game 'BepInEx\config\com.kks.main.girlshoulderikstabilizer.cfg'
Copy-Item -LiteralPath $cfg -Destination (Join-Path $backup 'shoulder.cfg')
$text = [IO.File]::ReadAllText($cfg)
$key = [regex]'(?m)^ShoulderDiagnosticLog\s*=.*$'
if ($key.Matches($text).Count -eq 1) { $text = $key.Replace($text,'ShoulderDiagnosticLog = true') }
elseif ($key.Matches($text).Count -eq 0 -and $text.Contains('[Logging]')) { $text = $text.Replace('[Logging]', "[Logging]`r`nShoulderDiagnosticLog = true") }
else { throw 'Unexpected Logging configuration; diagnostic enable was not written' }
[IO.File]::WriteAllText($cfg, $text, [Text.UTF8Encoding]::new($false))
$manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $backup 'manifest.json') -Encoding utf8
Write-Host "[shoulder] diagnostics enabled; backup and manifest: $backup"
Write-Host '[shoulder] game was not started; next user launch will create log/ShoulderPoseDiagnostic.log'
