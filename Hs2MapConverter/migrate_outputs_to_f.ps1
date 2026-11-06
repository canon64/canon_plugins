$ErrorActionPreference = 'Stop'
$source = 'J:\workspaces\hs2_kks_map_conversion\zipmods'
$destination = 'F:\kks\mods\Local HS2 Bulk Imports'
if (Get-CimInstance Win32_Process -Filter "Name='python.exe'" | Where-Object CommandLine -Like '*Hs2MapConverter\run_bulk.py*') { throw 'Stop the bulk worker before moving outputs' }
$junction = Get-Item -LiteralPath $destination -Force
if ($junction.FullName -ne $destination -or $junction.LinkType -ne 'Junction' -or @($junction.Target)[0] -ne $source) { throw 'Unexpected junction; no changes made' }
$junction.Delete()
New-Item -ItemType Directory -Path $destination | Out-Null
Get-ChildItem -LiteralPath $source -File -Filter '*.zipmod' | ForEach-Object {
    $copy = Join-Path $destination $_.Name
    Copy-Item -LiteralPath $_.FullName -Destination $copy
    if ((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $copy -Algorithm SHA256).Hash) { throw 'Output hash mismatch' }
}
Write-Output "Completed zipmods copied and SHA-256 verified: $destination"
