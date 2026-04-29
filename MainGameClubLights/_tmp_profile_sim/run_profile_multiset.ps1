$ErrorActionPreference = 'Stop'
$base = 'F:\kks\work\plugins\MainGameClubLights\bin\Release\net472'
$root = 'F:\kks\work\plugins\MainGameClubLights\_tmp_profile_sim\profileset'
New-Item -ItemType Directory -Force -Path $root | Out-Null

[Reflection.Assembly]::LoadFrom((Join-Path $base 'UnityEngine.dll')) | Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $base 'UnityEngine.CoreModule.dll')) | Out-Null
$asm = [Reflection.Assembly]::LoadFrom((Join-Path $base 'MainGameClubLights.dll'))
$settingsType = $asm.GetType('MainGameClubLights.ClubLightsSettings', $true)
$lightType = $asm.GetType('MainGameClubLights.LightInstanceSettings', $true)
$storeType = $asm.GetType('MainGameClubLights.SettingsStore', $true)
$save = $storeType.GetMethod('Save', [Reflection.BindingFlags]'Static, NonPublic')
$load = $storeType.GetMethod('Load', [Reflection.BindingFlags]'Static, NonPublic')

function Make([int]$n){
  $s=[Activator]::CreateInstance($settingsType)
  $lights=$settingsType.GetField('Lights').GetValue($s)
  for($i=1;$i -le $n;$i++){
    $li=[Activator]::CreateInstance($lightType)
    $lightType.GetField('Id').SetValue($li, "p$n-$i")
    $lightType.GetField('Name').SetValue($li, "Profile$n Light$i")
    $lightType.GetField('Intensity').SetValue($li, [single](0.5*$n+$i))
    $loop=$lightType.GetField('IntensityLoop').GetValue($li)
    $loop.GetType().GetField('Enabled').SetValue($loop, $true)
    $loop.GetType().GetField('VideoLink').SetValue($loop, $true)
    $lights.Add($li) | Out-Null
  }
  return $s
}

function Count($obj){
  $lights = $settingsType.GetField('Lights').GetValue($obj)
  return [int]$lights.Count
}

$pa=Join-Path $root 'A.json'; $pb=Join-Path $root 'B.json'; $pc=Join-Path $root 'C.json'
[void]$save.Invoke($null,[object[]]@([string]$pa, (Make 1)))
[void]$save.Invoke($null,[object[]]@([string]$pb, (Make 2)))
[void]$save.Invoke($null,[object[]]@([string]$pc, (Make 0)))

$ca=Count($load.Invoke($null,[object[]]@([string]$pa)))
$cb=Count($load.Invoke($null,[object[]]@([string]$pb)))
$cc=Count($load.Invoke($null,[object[]]@([string]$pc)))

Write-Output "PROFILE_A lights=$ca"
Write-Output "PROFILE_B lights=$cb"
Write-Output "PROFILE_C lights=$cc"
