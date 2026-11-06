using SessionGate = CanonPlugins.Local.SessionGate;
using UnityEngine;

namespace AstralMode
{
    public sealed partial class Plugin
    {
        private bool CanPlaceAstralCharacters => SessionGate.Allowed &&
            (_state == AstralState.Warping || _state == AstralState.WhiteHold ||
             _state == AstralState.FadeIn || _state == AstralState.Astral);

        // Run only after a pose change, scenery placement, or a placement setting change.
        // KKS may move both roots to a native H point before ChangeAnimator runs.
        private void PlaceAstralCharacters(string reason)
        {
            if (!CanPlaceAstralCharacters || _charaSnapshots.Count == 0) return;

            Vector3 position = _astralCharaCenter;
            Quaternion rotation = _charaSnapshots[0].WorldRot;
            string pose;
            if (IsBenchPose(out pose))
            {
                GameObject bench;
                if (_astralFurnitureByName.TryGetValue("Bench", out bench) && bench != null &&
                    _astralHillRoot != null && _astralHillRoot.activeSelf)
                {
                    position = bench.transform.position;
                    rotation = bench.transform.rotation;
                }
                else
                {
                    // The entry pose is selected before the scenery exists. Use exactly
                    // the same hierarchy and ground-height formula as BuildFurniture.
                    Vector3 hillPosition = ReadVector(_cfgAstralHillPosX, _cfgAstralHillPosY, _cfgAstralHillPosZ);
                    Quaternion hillRotation = Quaternion.Euler(ReadVector(_cfgAstralHillRotX, _cfgAstralHillRotY, _cfgAstralHillRotZ));
                    float hillScale = ReadScale(_cfgAstralHillScale);
                    Vector3 local = ReadVector(_cfgAstralBenchPosX, _cfgAstralBenchPosY, _cfgAstralBenchPosZ);
                    local.y += HillHeight(local.x, local.z);
                    position += hillPosition + hillRotation * (local * hillScale);
                    rotation = hillRotation * Quaternion.Euler(ReadVector(_cfgAstralBenchRotX, _cfgAstralBenchRotY, _cfgAstralBenchRotZ));
                }

            }

            foreach (var snapshot in _charaSnapshots)
            {
                if (snapshot.Target == null) continue;
                snapshot.Target.SetPositionAndRotation(position, rotation);
            }
            SyncCameraBaseToCharacters();
            TraceCoordinates("placement:" + reason);
        }

        private void SyncCameraBaseToCharacters()
        {
            if (_hFlag == null || _hFlag.ctrlCamera == null) return;
            var actors = CollectCurrentHSceneCharas();
            // Same primary female used by native HSceneProc.SetLocalPosition.
            foreach (var actor in actors)
            {
                if (actor == null || actor.objTop == null) continue;
                _hFlag.ctrlCamera.SetWorldBase(actor.objTop.transform);
                // SetWorldBase copies the transform; it does not update the camera yet.
                _hFlag.ctrlCamera.SetCameraData(_hFlag.ctrlCamera.GetCameraData());
                return;
            }
        }
    }
}
