using System;
using System.Collections;
using ActionGame;
using HarmonyLib;
using UnityEngine;

namespace MainGameAiShoujoMode
{
    public sealed partial class AiModeHSession
    {
        private HSessionBorrowedMapScope _borrowedMap;
        private IEnumerator Deadline()
        {
            yield return new WaitForSecondsRealtime(_p.N("hSession.timeoutSeconds"));
            _deadline = null;
            if (Busy && !RoomReady) Fail(new TimeoutException("H開始処理が完了しませんでした。"));
        }

        private static void BeforeHStart(HSceneProc __instance)
        {
            if (_active == null || !_active.Busy || (_active._proc != null && _active._proc != __instance)) return;
            _active._proc = __instance;
            var walking = (_active._host as HotelModeSession)?.NativeWalkingAction;
            if (walking != null && _active._map == null)
            {
                _active._borrowedMap = new HSessionBorrowedMapScope(walking.Map, _active._island, _active._p.I("hSession.mapNo"));
                _active._map = walking.Map;
                _active._log?.Invoke("ホテル歩行用Mapを客室Hへ接続しました。");
            }
            if (CanonGate.GateApi.HotelMode?.Owner == _active._host) CanonGate.GateApi.HotelMode.Bind(_active._host, __instance);
        }
        private static void AfterMapAwake(ActionMap __instance)
        {
            var owner = _active;
            if (owner == null || !owner.Busy) return;
            var proc = __instance.GetComponent<HSceneProc>();
            if (proc == null || (owner._proc != null && owner._proc != proc)) return;
            // ActionMap.Awake precedes HSceneProc.Start. Capture this owned HProc now
            // so its native map cannot load HotelMyroom before the Start hook runs.
            owner._proc = proc;
            owner._map = __instance;
            __instance.isLock = true;
            Set(__instance, typeof(BaseMap), "mapRoot", owner._island);
            Set(__instance, typeof(BaseMap), "no", (int)owner._p.N("hSession.mapNo"));
            Set(__instance, typeof(BaseMap), "infoDic", BaseMap.LoadMapInfo());
            Set(__instance, typeof(ActionMap), "mapObjects", Array.Empty<ActionGame.MapObject.Kind>());
            Set(__instance, typeof(ActionMap), "mapObjectGroup", owner._island.transform);
        }
        private static bool BeforeMapStart(ActionMap __instance) => _active == null || _active._map != __instance;
        private static bool BeforeMapChange(BaseMap __instance)
        {
            if (_active == null || _active._map != __instance) return true;
            _active._log?.Invoke("H用標準マップの読込を抑止し、現在の島を維持しました。");
            return false;
        }
    }
}
