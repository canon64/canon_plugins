param([string]$DllPath = (Join-Path $PSScriptRoot '..\bin\Release\net472\MainGameSubCameraDisplayProbe.dll'))
$ErrorActionPreference = 'Stop'
Write-Host 'Checking copied bone-camera persistence against the built assembly.'
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $DllPath).Path)
$linkType = $assembly.GetType('MainGameSubCameraDisplayProbe.CopiedBoneCamera', $true)
$link = [Activator]::CreateInstance($linkType, $true)
$values = @{
    FormatVersion = 2; LookAtWorldY = $false
    FixedCameraPosition = [single[]]@(1.25,2.5,-63.75)
    HasOwnerBinding = $true; OwnerIndependentActor = $false; OwnerFemaleSlot = 1
    BonePath = 'BodyTop/p_cf_anim/cf_j_root/cf_j_spine03'
}
foreach ($name in $values.Keys) { $linkType.GetField($name).SetValue($link, $values[$name]) }
function RoundTrip($Value, [type]$Type) {
    $serializer = [Runtime.Serialization.Json.DataContractJsonSerializer]::new($Type)
    $stream = [IO.MemoryStream]::new()
    try {
        $serializer.WriteObject($stream, $Value)
        $stream.Position = 0
        return $serializer.ReadObject($stream)
    } finally { $stream.Dispose() }
}
foreach ($holderName in @('SubCameraPreset', 'ActorCameraShot', 'CameraPoseOverride')) {
    $holderType = $assembly.GetType("MainGameSubCameraDisplayProbe.$holderName", $true)
    $holder = [Activator]::CreateInstance($holderType, $true)
    $holderType.GetField('UseBoneLink').SetValue($holder, $true)
    $holderType.GetField('BoneName').SetValue($holder, 'cf_j_head')
    $holderType.GetField('BoneCamera').SetValue($holder, $link)
    $restored = RoundTrip $holder $holderType
    if (!$restored.UseBoneLink -or $restored.BoneName -ne 'cf_j_head') { throw "$holderName lost bone binding" }
    foreach ($name in $values.Keys) {
        $actual = $linkType.GetField($name).GetValue($restored.BoneCamera)
        if (($actual -join ',') -ne ($values[$name] -join ',')) { throw "$holderName lost $name" }
    }
    $legacy = [Activator]::CreateInstance($holderType, $true)
    $legacy = RoundTrip $legacy $holderType
    if ($null -ne $legacy.BoneCamera) { throw "$holderName changed legacy defaults" }
    Write-Host "PASS: $holderName retains every bone-camera field and legacy defaults."
}
$presetType = $assembly.GetType('MainGameSubCameraDisplayProbe.SubCameraPreset', $true)
$poseType = $assembly.GetType('MainGameSubCameraDisplayProbe.CameraPoseOverride', $true)
$actorType = $assembly.GetType('MainGameSubCameraDisplayProbe.ActorCameraShot', $true)
$preset = [Activator]::CreateInstance($presetType, $true)
$pose = [Activator]::CreateInstance($poseType, $true)
$actor = [Activator]::CreateInstance($actorType, $true)
$poseType.GetField('BoneCamera').SetValue($pose, $link)
$actorType.GetField('BoneCamera').SetValue($actor, $link)
$poses = [Array]::CreateInstance($poseType, 1)
$poses.SetValue($pose, 0)
$presetType.GetField('PoseOverrides').SetValue($preset, $poses)
$presetType.GetField('ActorCamera').SetValue($preset, $actor)
$restored = RoundTrip $preset $presetType
if ($restored.PoseOverrides[0].BoneCamera.FormatVersion -ne 2 -or
    $restored.ActorCamera.BoneCamera.BonePath -ne 'BodyTop/p_cf_anim/cf_j_root/cf_j_spine03') { throw 'Nested shot lost bone-camera settings' }
Write-Host 'PASS: nested pose overrides and independent actor shot retain bone-camera settings.'
