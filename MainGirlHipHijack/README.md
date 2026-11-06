# MainGirlHipHijack

A BepInEx plugin for KoikatsuSunshine H-scenes that provides female full-body IK control, gizmo editing, pose presets, and VR-assisted manipulation.

Japanese version: [README_ja.md](README_ja.md)

## Status

- Beta
- Female workflow is the primary supported path
- Male-control UI is currently hidden (temporarily sealed), but female bone-follow can still target male bones and HMD as follow candidates
- Targets both `KoikatsuSunshine` and `KoikatsuSunshine_VR`
- `Enabled`, `UI > Visible`, and `Logging > EnableLogs` can be switched from ConfigManager
- Hotel rooms persist all 13 effector states, weights, room-relative poses, bone/Composer bindings, and head/hand settings, restoring them after actor placement and Composer targets.
- Re-entry keeps the room pose authoritative until the posture changes; global presets containing another map's world coordinates do not overwrite it, and legacy room files without HipHijack data keep the native pose.

## Main Features

- Female BodyIK control for 13 effectors:
  - Left/Right Hand
  - Left/Right Foot
  - Left/Right Shoulder
  - Left/Right Thigh
  - Left/Right Elbow
  - Left/Right Knee
  - Body (hip center)
- Per-effector:
  - Enable/disable
  - Weight (0..1)
  - Gizmo visibility
  - Reset to current animation pose
- Bone follow:
  - `Nearest Follow` snaps to nearest valid follow target
  - Follow target candidates include:
    - female body bones (filtered)
    - male body bones (filtered, minimal set rules)
    - HMD (when VR pose is available)
- VR operations:
  - VR grab mode for IK proxies
  - VR grab mode starts enabled for each permitted VR H session. It can be turned off manually for that session; desktop sessions do not enable it automatically.
  - Wrists can be gripped with IK off: grip within the configured range to enable that wrist at full IK weight, preserving its current position and rotation. Release to keep the pose, or press B on the holding controller to clear follow and return that hand to animation.
  - Trigger while holding a wrist sets or clears nearest-bone follow; follow is optional. B suppresses the hip shortcut until grip release. Automatic wrist changes are session-only and do not save settings.
  - Hold A while grabbing a wrist to aim the elbow target along the wrist-to-elbow ray, initially 20 cm beyond the elbow. Rotation pivots around the wrist; releasing A leaves the elbow target in world space. Adjust the extension with the VR section's slider and numeric field (0–100 cm). B also resets the elbow controlled by this gesture.
  - Release the wrist before attaching handcuffs. Cuffed wrists belong exclusively to the cuff controller and cannot be grabbed or reset with the ordinary wrist controls. Removing cuffs restores the previous wrist IK and follow state.
  - Female head grab with additive rotation behavior
  - Main workflow: while holding the trigger in VR, press `B` to enable female hip IK, parent it to the left controller, and map real controller movement into in-game motion
- Pose presets (female):
  - Save/load with screenshot
  - Auto-apply by matching posture
  - Transition easing (Linear / SmoothStep / EaseOut)
  - Includes female head additive rotation state in save/load
- H-scene speed/hip linkage tools:
  - Body-to-controller link
  - Speed gauge hijack
  - Female animation speed cut option
  - Optional auto-insert flow from hip movement while in idle state
- Logging and diagnostics:
  - `Detail log` for input-capture and runtime-state tracing
  - `BodyIK diagnostic log` for before/after IK application checks
  - Output is routed through both `MainGameLogRelay` and the dedicated plugin log
- `MainGameBlankMapAdd` integration (VideoAllposeRoom):
  - If `MainGameBlankMapAdd` is installed, HipHijack UI visibility can be toggled from the VideoAllposeRoom playback bar
  - Use the `HipUI` checkbox on the second playback-bar row (to the right of `説明`)

## Runtime Notes

- The plugin forces its UI closed on startup
- When auto-pose has zero matching candidates for the current posture, BodyIK is turned off and nothing is auto-applied for that posture
- When auto-pose has only one matching candidate, that preset can still be applied, but loop-based rotation does not occur

## Requirements

- KoikatsuSunshine
- BepInEx 5.x

## Dependencies

These are hard dependencies declared in the plugin:

- `MainGameTransformGizmo`
- `MainGameUiInputCapture`
- `MainGameLogRelay`

## Installation

Place built DLLs under:

`BepInEx/plugins/canon_plugins/MainGirlHipHijack/`

Minimum required set:

- `MainGirlHipHijack.dll`
- `MainGameTransformGizmo.dll`
- `MainGameUiInputCapture.dll`
- `MainGameLogRelay.dll`

## Configuration

Runtime settings file:

`BepInEx/plugins/canon_plugins/MainGirlHipHijack/FullIkGizmoSettings.json`

Common saved items include:

- `AutoEnableAllOnResolve`
- `AutoPoseEnabled`
- `AutoPoseSwitchAnimationLoops`
- `PoseTransitionSeconds`
- `PoseTransitionEasing`
- `AutoInsertOnMoveEnabled`
- `DetailLogEnabled`
- `BodyIkDiagnosticLog`

Notes:

- The file is generated automatically on first launch
- Values are normalized/clamped on load/save
- Volatile per-session IK on/off states are reset on startup

## Known Issues

See:

- [KNOWN_ISSUES.md](KNOWN_ISSUES.md)

## Build (source)

Target framework: `net472`

Build:

`dotnet build MainGirlHipHijack.csproj -c Release`

Output:

`bin/Release/net472/MainGirlHipHijack.dll`

## Plugin Info

- GUID: `com.kks.main.girlbodyikgizmo`
- Name: `MainGirlHipHijack`
- Version: `1.0.0`
- Process: `KoikatsuSunshine`, `KoikatsuSunshine_VR`
