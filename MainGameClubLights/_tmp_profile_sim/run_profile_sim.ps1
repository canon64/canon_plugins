$ErrorActionPreference = 'Stop'
$base = 'F:\kks\work\plugins\MainGameClubLights\bin\Release\net472'
$testDir = 'F:\kks\work\plugins\MainGameClubLights\_tmp_profile_sim'
New-Item -ItemType Directory -Force -Path $testDir | Out-Null

[Reflection.Assembly]::LoadFrom((Join-Path $base 'UnityEngine.dll')) | Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $base 'UnityEngine.CoreModule.dll')) | Out-Null
$asm = [Reflection.Assembly]::LoadFrom((Join-Path $base 'MainGameClubLights.dll'))

$settingsType = $asm.GetType('MainGameClubLights.ClubLightsSettings', $true)
$lightType = $asm.GetType('MainGameClubLights.LightInstanceSettings', $true)
$storeType = $asm.GetType('MainGameClubLights.SettingsStore', $true)

$saveMethod = $storeType.GetMethod('Save', [Reflection.BindingFlags]'Static, NonPublic')
$loadMethod = $storeType.GetMethod('Load', [Reflection.BindingFlags]'Static, NonPublic')
if ($null -eq $saveMethod -or $null -eq $loadMethod) { throw 'SettingsStore Save/Load reflection failed' }

function New-Settings {
    param([int]$count)
    $s = [Activator]::CreateInstance($settingsType)
    $lightsField = $settingsType.GetField('Lights')
    $lights = $lightsField.GetValue($s)

    for ($i = 1; $i -le $count; $i++) {
        $li = [Activator]::CreateInstance($lightType)
        $lightType.GetField('Id').SetValue($li, ('sim{0:00}' -f $i))
        $lightType.GetField('Name').SetValue($li, ('Sim Light {0}' -f $i))
        $lightType.GetField('Intensity').SetValue($li, [single](1.25 * $i))
        $lightType.GetField('Range').SetValue($li, [single](8 + $i))
        $lightType.GetField('SpotAngle').SetValue($li, [single](150 + $i))
        $lightType.GetField('WorldPosX').SetValue($li, [single](0.1 * $i))
        $lightType.GetField('WorldPosY').SetValue($li, [single](1.2 * $i))
        $lightType.GetField('WorldPosZ').SetValue($li, [single](2.3 * $i))

        $intLoop = $lightType.GetField('IntensityLoop').GetValue($li)
        $intLoop.GetType().GetField('Enabled').SetValue($intLoop, $true)
        $intLoop.GetType().GetField('VideoLink').SetValue($intLoop, ($i % 2 -eq 0))
        $intLoop.GetType().GetField('MinValue').SetValue($intLoop, [single](0.25 * $i))
        $intLoop.GetType().GetField('MaxValue').SetValue($intLoop, [single](2.0 * $i))
        $intLoop.GetType().GetField('SpeedHz').SetValue($intLoop, [single](0.75 * $i))

        $rangeLoop = $lightType.GetField('RangeLoop').GetValue($li)
        $rangeLoop.GetType().GetField('Enabled').SetValue($rangeLoop, $true)
        $rangeLoop.GetType().GetField('VideoLink').SetValue($rangeLoop, ($i % 2 -eq 1))
        $rangeLoop.GetType().GetField('MinValue').SetValue($rangeLoop, [single](1.5 * $i))
        $rangeLoop.GetType().GetField('MaxValue').SetValue($rangeLoop, [single](9.5 * $i))
        $rangeLoop.GetType().GetField('SpeedHz').SetValue($rangeLoop, [single](1.25 * $i))

        $lights.Add($li) | Out-Null
    }

    return $s
}

function Get-LightsCount {
    param($settingsObj)
    $lights = $settingsType.GetField('Lights').GetValue($settingsObj)
    return [int]$lights.Count
}

function Get-FirstLightDigest {
    param($settingsObj)
    $lights = $settingsType.GetField('Lights').GetValue($settingsObj)
    if ($lights.Count -le 0) { return '(none)' }
    $li = $lights[0]
    $id = $lightType.GetField('Id').GetValue($li)
    $name = $lightType.GetField('Name').GetValue($li)
    $intensity = $lightType.GetField('Intensity').GetValue($li)
    $range = $lightType.GetField('Range').GetValue($li)
    $spot = $lightType.GetField('SpotAngle').GetValue($li)

    $intLoop = $lightType.GetField('IntensityLoop').GetValue($li)
    $ilEnabled = $intLoop.GetType().GetField('Enabled').GetValue($intLoop)
    $ilVideo = $intLoop.GetType().GetField('VideoLink').GetValue($intLoop)
    $ilMin = $intLoop.GetType().GetField('MinValue').GetValue($intLoop)
    $ilMax = $intLoop.GetType().GetField('MaxValue').GetValue($intLoop)
    $ilSpeed = $intLoop.GetType().GetField('SpeedHz').GetValue($intLoop)

    return "id=$id name=$name I=$intensity R=$range A=$spot IL(enabled=$ilEnabled video=$ilVideo min=$ilMin max=$ilMax hz=$ilSpeed)"
}

$profilePath = Join-Path $testDir 'sim_profile.json'
if (Test-Path $profilePath) { Remove-Item -LiteralPath $profilePath -Force }

$sA = New-Settings -count 1
$okA = [bool]$saveMethod.Invoke($null, [object[]]@([string]$profilePath, $sA))
$lA = $loadMethod.Invoke($null, [object[]]@([string]$profilePath))
$cA = Get-LightsCount $lA
$dA = Get-FirstLightDigest $lA

$sB = New-Settings -count 0
$okB = [bool]$saveMethod.Invoke($null, [object[]]@([string]$profilePath, $sB))
$lB = $loadMethod.Invoke($null, [object[]]@([string]$profilePath))
$cB = Get-LightsCount $lB
$dB = Get-FirstLightDigest $lB

$sC = New-Settings -count 2
$okC = [bool]$saveMethod.Invoke($null, [object[]]@([string]$profilePath, $sC))
$lC = $loadMethod.Invoke($null, [object[]]@([string]$profilePath))
$cC = Get-LightsCount $lC
$dC = Get-FirstLightDigest $lC

$json = Get-Content -Path $profilePath -Raw -Encoding UTF8

Write-Output "CASE_A save=$okA loadCount=$cA first=$dA"
Write-Output "CASE_B save=$okB loadCount=$cB first=$dB"
Write-Output "CASE_C save=$okC loadCount=$cC first=$dC"
Write-Output ('JSON_HEAD=' + ($json.Substring(0, [Math]::Min(420, $json.Length)).Replace("`r"," ").Replace("`n"," ")))
