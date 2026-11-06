using HarmonyLib;
using UnityEngine;

namespace MainGameVoiceFaceEventBridge
{
    internal sealed partial class Plugin
    {
        private Component _aiModeFace;
        private void BeginAiModeFace()
        {
            if (SessionGate.Actor == null) return;
            var actor = SessionGate.Actor;
            _aiModeFace = gameObject.AddComponent(typeof(FaceListCtrl));
            ((Behaviour)_aiModeFace).enabled = false;
            AccessTools.Method(typeof(FaceListCtrl), "Start").Invoke(_aiModeFace, null);
            var heroine = new SaveData.Heroine(actor.chaFile, false);
            var loader = AccessTools.Method(typeof(FaceListCtrl), "LoadTextD");
            loader.Invoke(_aiModeFace, new object[] { heroine.ChaName });
        }
        private void EndAiModeFace()
        {
            if (_aiModeFace != null) Destroy(_aiModeFace);
            _aiModeFace = null;
        }
    }
}
