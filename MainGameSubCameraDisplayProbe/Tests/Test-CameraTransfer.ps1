param([ValidateSet('None','RoundCoordinates','BonePositionFollow','DropBoneMode','EarlyFollow','EarlyTransition')][string]$Fault = 'None')
$ErrorActionPreference = 'Stop'
Write-Host 'Testing production main capture -> circle save -> JSON reload -> preset recall -> bone follow with a managed Unity adapter.'
function Read-Member([string]$Source, [string]$Declaration) {
    $start = $Source.IndexOf($Declaration, [StringComparison]::Ordinal)
    if ($start -lt 0) { throw "Missing production member: $Declaration" }
    $open = $Source.IndexOf('{', $start)
    $depth = 1; $end = $open + 1
    while ($depth -gt 0 -and $end -lt $Source.Length) {
        if ($Source[$end] -eq '{') { $depth++ }
        if ($Source[$end] -eq '}') { $depth-- }
        $end++
    }
    return $Source.Substring($start, $end - $start)
}
$root = Split-Path -Parent $PSScriptRoot
$presets = [IO.File]::ReadAllText((Join-Path $root 'Plugin.Presets.cs'))
$members = @(
    'private void LoadPreset(SubCameraPreset preset, string source)',
    'private void ActivateBoneFollow(', 'private void UpdateBoneFollow()', 'private void ClearBoneFollow()',
    'private Transform ResolveActiveBone()', 'private static ChaControl ResolveMainFemale()',
    'private static Transform[] GetFemaleBoneCache(', 'private static Transform ResolveBoneTarget(',
    'private static Transform FindBoneByName(', 'private SubCameraPreset ResolveCameraPresetForCurrentPose(',
    'private CameraPoseOverride FindCameraPoseOverride(', 'private static SubCameraPreset ClonePreset(',
    'private static ActorCameraShot CaptureActorShot(', 'private static SubCameraPreset ResolveActorShot(',
    'private static float[] CloneArray(', 'private static bool HasCameraPosition(', 'private static float[] ToArray('
) | ForEach-Object { Read-Member $presets $_ }
$external = [IO.File]::ReadAllText((Join-Path $root 'Plugin.ExternalActor.cs'))
$members += @('private void LateUpdate()', 'private void ApplyExternalPreset(', 'private void UpdateExternalPreset()', 'private void SetExternalCameraWorldPose(') |
    ForEach-Object { Read-Member $external $_ }
$plugin = [IO.File]::ReadAllText((Join-Path $root 'Plugin.cs'))
if (!$plugin.Contains('[DefaultExecutionOrder(32000)]')) { throw 'Camera no longer runs after animation/IK.' }
$members += Read-Member $plugin 'private void Update()'
$members += Read-Member ([IO.File]::ReadAllText((Join-Path $root 'Plugin.Probe.cs'))) 'private void CaptureTransformsToSettings()'
$main = [IO.File]::ReadAllText((Join-Path $root '..\MainGameCameraControl\Plugin.PlaybackPresets.cs'))
$capture = Read-Member $main 'public object[] CaptureMainCameraForSubCamera()'
$lookAt = Read-Member ([IO.File]::ReadAllText((Join-Path $root '..\MainGameCameraControl\Plugin.cs'))) 'private Vector3 ResolveActiveLookAt('
$units = @([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'CameraTransferHarness.cs')))
$units += 'namespace MainGameCameraControl { internal partial class Plugin {' + $capture + $lookAt + '}}'
$units += 'namespace MainGameSubCameraDisplayProbe { public sealed partial class Plugin {' + ($members -join "`n") + '}}'
foreach ($name in @('Plugin.CopiedBoneCamera.cs','Plugin.CopiedBoneBinding.cs','Plugin.PlaybackPresets.cs','Plugin.Transition.cs','Plugin.Settings.cs')) {
    $units += [IO.File]::ReadAllText((Join-Path $root $name))
}
# Put compilation-unit imports first; do not rewrite any production method bodies.
$imports = $units | ForEach-Object { [regex]::Matches($_, '(?m)^using [^\r\n]+;\r?$') | ForEach-Object { $_.Value.Trim() } } | Sort-Object -Unique
$bodies = $units | ForEach-Object { [regex]::Replace($_, '(?m)^using [^\r\n]+;\r?$', '') }
$code = ($imports -join "`n") + "`n" + ($bodies -join "`n")
switch ($Fault) {
    'EarlyFollow' { $code = $code.Replace('TickVisibilityDiagnostic();', 'if (_actorLease == 0) UpdateBoneFollow(); TickVisibilityDiagnostic();') }
    'EarlyTransition' { $code = $code.Replace('TickVisibilityDiagnostic();', 'if (_actorLease == 0) UpdateCameraTransition(); TickVisibilityDiagnostic();') }
    'RoundCoordinates' { $code = $code.Replace('return new[] { value.x, value.y, value.z };', 'return new[] { (float)Math.Round(value.x,2), (float)Math.Round(value.y,2), (float)Math.Round(value.z,2) };') }
    'DropBoneMode' { $code = $code.Replace('_boneFollowActive = true;', '_boneFollowActive = false;') }
    'BonePositionFollow' {
        $code = $code.Replace('[DataMember] public int FormatVersion;', '[IgnoreDataMember] public bool TestInitialized; [IgnoreDataMember] public Vector3 TestBonePosition; [DataMember] public int FormatVersion;')
        $line = 'position = shot != null ? ToVector3(shot.FixedCameraPosition, legacyPosition) : legacyPosition;'
        $code = $code.Replace($line, $line + ' if(shot!=null) { if(!shot.TestInitialized) {shot.TestInitialized=true;shot.TestBonePosition=bone.position;} position+=bone.position-shot.TestBonePosition; }')
    }
}
Add-Type -TypeDefinition $code -IgnoreWarnings -WarningAction SilentlyContinue
$evidenceRoot = Join-Path $root '..\_diagnostics\bone-shot-v2-tests'
[IO.Directory]::CreateDirectory($evidenceRoot) | Out-Null
$rejected = $false
try { $result = [MainGameSubCameraDisplayProbe.Plugin]::RunTransferChecks([IO.Path]::GetFullPath($evidenceRoot)) }
catch {
    if ($Fault -eq 'None') { throw }
    $rejected = $true
    Write-Host "PASS: regression checks rejected fault $Fault : $($_.Exception.InnerException.Message)"
}
if ($Fault -ne 'None' -and !$rejected) { throw "Regression checks failed to detect $Fault" }
if ($Fault -eq 'None') { Write-Host $result }
