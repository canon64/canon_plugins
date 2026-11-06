# MainGirlShoulderIkStabilizer 1.1.1

Raises the clavicle-side mapped parent bone from upper-arm elevation for sides with HipHijack hand IK enabled. Bent elbows no longer disable elevation just because the hand is close to the shoulder.

The production correction preserves the solved hand position, wrist orientation, bone lengths and elbow plane. It blends manual shoulder/chain ownership, limits unreachable correction, restores only its own previous rotations, and runs once after the outermost real FinalIK solve. AdvIK shoulder rotation is suppressed only for owned sides without changing card settings.

Supports the shared FinalIK path in KoikatsuSunshine and official VR. Requires MainGirlHipHijack 1.1.1 or newer, MainGameLogRelay and canon_gate. Deploy both updated plugins from each project's own Release output after the game exits.

The opt-in `Logging.ShoulderDiagnosticLog` writes actual bone angles, forward direction, weights and four pose stages to `log/ShoulderPoseDiagnostic.log`, independently of the shared relay switch. Its startup identity includes loaded DLL paths and module IDs.

Run `tools/verify.ps1` for Release builds, headless production-code tests and the existing HipHijack characterization. Headless tests do not prove Unity skin deformation or live Harmony ordering. Visual acceptance remains an in-game check by the user.

See [Japanese usage and settings](README_ja.md), [implementation notes](CODEBASE_STATE.md) and [diagnostics](LOG_MAP.md).
