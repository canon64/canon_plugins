# MainGameHandySync

KKS MainGame HScene animation → The Handy (Handy2) device sync plugin.

## Features (MVP)
- Real-time piston phase extraction from HScene (bone-based primary + HFlag fallback)
- Live position commands to The Handy via v2-style API (Connection Key only)
- Configurable stroke range, invert, update rate, speed scale
- Detailed logging of sampled phase for validation
- Test connection helper

## Planned / High Value
- Funscript export (recommended for best results due to cloud latency)
- v3 HAMP / HSSP support

## Setup
1. Build Release → copy `MainGameHandySync.dll` (+ .pdb) to `BepInEx/plugins/canon_plugins/` (or plugins/)
2. In-game BepInEx config:
   - `Enabled` = true
   - `ConnectionKey` = your device key (from handyfeeling.com device page)
   - Adjust `StrokeMin/Max` (e.g. 5–95) to taste
3. Enter HScene (sonyu/insertion) — phase logs appear in `MainGameHandySync.log`
4. With valid key + device online, motion should drive the Handy

## Test Connection
Call `TestHandyConnection()` via in-game console / future UI button (currently internal).

## Notes
- Cloud round-trip latency exists on live path. For perfect sync, record gameplay + use exported Funscript in external player (future feature).
- Bone discovery is automatic at runtime; check log for "Bones found" or partial warnings.

## Credits / References
Extensive use of prior HScene / hip IK / sonyu analysis in `F:/kks/work/analysis/`.
Follows patterns from AstralMode and MainGameCameraControl.

## License
Private / experimental (secret_work scope).
