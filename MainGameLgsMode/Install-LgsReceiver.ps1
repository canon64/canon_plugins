param(
    [Parameter(Mandatory)][string]$LgsRoot,
    [Parameter(Mandatory)][string]$LgsExecutable,
    [Parameter(Mandatory)][string]$ProfileId,
    [Parameter(Mandatory)][string]$InstalledPluginRoot
)
$ErrorActionPreference = 'Stop'
Write-Host 'Installing the LGS receiver with verified backups before modification'
$LgsRoot = (Resolve-Path -LiteralPath $LgsRoot).Path
$LgsExecutable = (Resolve-Path -LiteralPath $LgsExecutable).Path
$InstalledPluginRoot = (Resolve-Path -LiteralPath $InstalledPluginRoot).Path
$profile = Join-Path $LgsRoot ('profiles\' + $ProfileId + '.xml')
$loader = Join-Path $InstalledPluginRoot 'LgsProfileScript.lua'
foreach ($path in @($profile, $loader, (Join-Path $InstalledPluginRoot 'LgsReceiver.lua'))) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw ('Missing file: ' + $path) }
}
$script = [IO.File]::ReadAllText($loader, [Text.Encoding]::UTF8)
function Read-Xml([string]$text) {
    $document = [Xml.XmlDocument]::new()
    $document.PreserveWhitespace = $true
    $document.LoadXml($text)
    return ,$document
}
function Copy-VerifiedSnapshot([string]$destination) {
    Copy-Item -LiteralPath $LgsRoot -Destination $destination -Recurse
    $records = foreach ($file in Get-ChildItem -LiteralPath $LgsRoot -Recurse -File) {
        $relative = $file.FullName.Substring($LgsRoot.Length).TrimStart('\')
        $copy = Join-Path $destination $relative
        $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        if ($hash -ne (Get-FileHash -LiteralPath $copy -Algorithm SHA256).Hash) { throw ('Backup mismatch: ' + $relative) }
        [pscustomobject]@{Path=$relative; SHA256=$hash; Bytes=$file.Length}
    }
    return @($records)
}
$text = [IO.File]::ReadAllText($profile, [Text.Encoding]::UTF8)
$originalXml = Read-Xml $text
$node = $originalXml.SelectSingleNode('/*[local-name()="profiles"]/*[local-name()="profile"]')
if ($node.GetAttribute('guid') -ne $ProfileId -or $node.GetAttribute('name') -ne 'KSPlugin') { throw 'Unexpected profile identity' }
$scriptNode = $node.SelectSingleNode('*[local-name()="script"]')
if ($null -eq $scriptNode) { throw 'Profile has no script node' }
$defaultLines = @($scriptNode.InnerText -split '\r?\n' | ForEach-Object { $_.Trim() } | Where-Object { $_ -and -not $_.StartsWith('--') })
if ($scriptNode.InnerText.Trim() -ne $script.Trim() -and ($defaultLines -join '|') -ne 'function OnEvent(event, arg)|end') {
    throw 'Existing custom Lua script detected; refusing to overwrite it'
}
$running = @(Get-CimInstance Win32_Process -Filter "Name='LCore.exe'" | Where-Object { $_.ExecutablePath -eq $LgsExecutable })
if ($running.Count -gt 1) { throw 'More than one matching LGS process is running' }
$backupRoot = Join-Path $PSScriptRoot ('runtime\lgs-backups\' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
$before = Copy-VerifiedSnapshot (Join-Path $backupRoot 'before-stop')
Write-Host ('Backup verified: ' + $backupRoot)
$stopped = $false
$written = $false
try {
    if ($running.Count) {
        Stop-Process -Id $running[0].ProcessId -ErrorAction Stop
        Wait-Process -Id $running[0].ProcessId -Timeout 10 -ErrorAction SilentlyContinue
        $stopped = $true
    }
    $stable = Copy-VerifiedSnapshot (Join-Path $backupRoot 'stopped')
    $text = [IO.File]::ReadAllText($profile, [Text.Encoding]::UTF8)
    $originalXml = Read-Xml $text
    $pattern = '(?s)(<script>)(.*?)(</script>)'
    if ([regex]::Matches($text, $pattern).Count -ne 1) { throw 'Expected exactly one script block' }
    $escaped = [Security.SecurityElement]::Escape($script)
    $updated = [regex]::Replace($text, $pattern, [Text.RegularExpressions.MatchEvaluator]{ param($match) $match.Groups[1].Value + $escaped + $match.Groups[3].Value })
    $newXml = Read-Xml $updated
    $oldScript = $originalXml.SelectSingleNode('//*[local-name()="script"]')
    $newScript = $newXml.SelectSingleNode('//*[local-name()="script"]')
    if ($newScript.InnerText -ne $script) { throw 'Script content validation failed' }
    $oldScript.InnerText = $newScript.InnerText
    if ($originalXml.OuterXml -ne $newXml.OuterXml) { throw 'A setting outside the script would change' }
    $bytes = [IO.File]::ReadAllBytes($profile)
    $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 239 -and $bytes[1] -eq 187 -and $bytes[2] -eq 191
    [IO.File]::WriteAllText($profile, $updated, [Text.UTF8Encoding]::new($hasBom))
    $written = $true
    if ([IO.File]::ReadAllText($profile, [Text.Encoding]::UTF8) -ne $updated) { throw 'Profile read-back mismatch' }
    foreach ($record in $stable) {
        $current = Join-Path $LgsRoot $record.Path
        if ($current -ne $profile -and (Get-FileHash -LiteralPath $current -Algorithm SHA256).Hash -ne $record.SHA256) {
            throw ('Unrelated setting changed: ' + $record.Path)
        }
    }
    $report = [pscustomobject]@{
        Time=(Get-Date).ToString('o'); Profile=$profile; Backup=$backupRoot
        BeforeStop=$before; BeforeModification=$stable
        ProfileSHA256=(Get-FileHash -LiteralPath $profile -Algorithm SHA256).Hash
        OnlyScriptChanged=$true; AssignedKeysPreserved=$true; RestartRequired=$stopped
    }
    [IO.File]::WriteAllText((Join-Path $backupRoot 'installation.json'), ($report | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
    Write-Host 'Receiver installed; verified that assignments and other settings are unchanged'
}
catch {
    if ($written) { Copy-Item -LiteralPath (Join-Path $backupRoot ('stopped\profiles\' + $ProfileId + '.xml')) -Destination $profile -Force }
    throw
}
finally {
    if ($stopped) {
        $restarted = Start-Process -FilePath $LgsExecutable -ArgumentList '/minimized' -WindowStyle Hidden -PassThru
        $null = $restarted.WaitForInputIdle(10000)
        if ($restarted.HasExited) { throw 'LGS exited after restart; configuration backup is available' }
        Write-Host ('LGS restarted; PID=' + $restarted.Id)
    }
}
Write-Host ('Verified backup and report: ' + $backupRoot)
