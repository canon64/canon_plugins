$ErrorActionPreference = 'Stop'
$bulk = 'J:\workspaces\hs2_kks_map_conversion'
$script = 'F:\kks\work\plugins\canon_plugins\Hs2MapConverter\run_bulk.py'
Set-Content -LiteralPath "$bulk\STOP" -Value 'Stopped at user request. Do not resume automatically.' -Encoding utf8
$snapshot = @(Get-CimInstance Win32_Process)
$roots = @($snapshot | Where-Object { $_.Name -eq 'python.exe' -and $_.CommandLine -like "*$script*" })
$ids = [System.Collections.Generic.HashSet[int]]::new()
foreach ($process in $roots) { [void]$ids.Add([int]$process.ProcessId) }
do {
    $added = $false
    foreach ($process in $snapshot) {
        if ($ids.Contains([int]$process.ParentProcessId) -and $process.Name -notlike '*Licensing*' -and $ids.Add([int]$process.ProcessId)) { $added = $true }
    }
} while ($added)
foreach ($process in $roots) { Stop-Process -Id $process.ProcessId -Force -ErrorAction SilentlyContinue }
foreach ($processIdToStop in $ids) { Stop-Process -Id $processIdToStop -Force -ErrorAction SilentlyContinue }
$remaining = @(Get-CimInstance Win32_Process | Where-Object { $ids.Contains([int]$_.ProcessId) })
if ($remaining.Count) { throw "Conversion processes still present: $($remaining.ProcessId -join ', ')" }
Write-Output "Stopped converter process tree: $($ids -join ', ')"
