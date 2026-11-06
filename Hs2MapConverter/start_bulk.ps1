$ErrorActionPreference = 'Stop'
$python = 'J:\system_tools\venvs\py310-common\Scripts\python.exe'
$bulk = 'J:\workspaces\hs2_kks_map_conversion'
$script = Join-Path $PSScriptRoot 'run_bulk.py'
$link = 'F:\kks\mods\Local HS2 Bulk Imports'
if (!(Test-Path -LiteralPath "$bulk\installed_index.json")) { throw 'Run index_kks.py before starting the bulk converter' }
if (Get-CimInstance Win32_Process -Filter "Name='python.exe'" | Where-Object CommandLine -Like "*$script*") { throw 'The bulk worker is already running' }
New-Item -ItemType Directory -Force -Path "$bulk\zipmods" | Out-Null
if (!(Test-Path -LiteralPath $link)) { New-Item -ItemType Directory -Path $link | Out-Null }
$actual = Get-Item -LiteralPath $link -Force
if ($actual.LinkType) { throw 'Bulk output must now be a real F drive directory' }
$env:PYTHONIOENCODING = 'utf-8'
if (Test-Path -LiteralPath "$bulk\bulk.log") { Copy-Item -LiteralPath "$bulk\bulk.log" -Destination "$bulk\bulk.previous.log" -Force }
$process = Start-Process -FilePath $python -ArgumentList @('-B','-u', $script,'--retry-failed') -WorkingDirectory $PSScriptRoot -RedirectStandardOutput "$bulk\bulk.log" -RedirectStandardError "$bulk\bulk.stderr.log" -WindowStyle Hidden -PassThru
Write-Output "Bulk converter started: PID=$($process.Id)"
Write-Output "Progress: $bulk\progress.json"
Write-Output "KKS mods: $link"
