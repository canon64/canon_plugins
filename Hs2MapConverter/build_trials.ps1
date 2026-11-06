$ErrorActionPreference = 'Stop'
$work = 'F:\kks\work\hs2_map_trials'
$editor = 'C:\Program Files\Unity\Hub\Editor\2019.4.9f1\Editor\Unity.exe'
if (!(Test-Path -LiteralPath $editor)) { throw 'Target Unity editor not found' }
if (Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object CommandLine -Like "*$work\UnityProject*") { throw 'This batch project is already open' }
Copy-Item -LiteralPath "$PSScriptRoot\UnityEditor\Hs2MapBuild.cs" -Destination "$work\UnityProject\Assets\Editor\Hs2MapBuild.cs"
New-Item -ItemType Directory -Force -Path "$work\temp", "$work\UnityUser" | Out-Null
$env:TEMP = "$work\temp"
$env:TMP = "$work\temp"
$env:APPDATA = "$work\UnityUser\Roaming"
$env:LOCALAPPDATA = "$work\UnityUser\Local"
$process = Start-Process -FilePath $editor -ArgumentList @('-batchmode','-nographics','-projectPath',"$work\UnityProject",'-executeMethod','Hs2MapBuild.Build','-logFile',"$work\unity_build.log") -WindowStyle Hidden -PassThru
Write-Output "Batch build started: PID=$($process.Id), log=$work\unity_build.log"
