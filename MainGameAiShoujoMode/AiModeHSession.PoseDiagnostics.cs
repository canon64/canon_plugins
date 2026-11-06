using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace MainGameAiShoujoMode
{
    public sealed partial class AiModeHSession
    {
        internal Func<bool> RoomPoseDiagnostics;
        internal Action<string, Func<string>> RoomPoseTrace;
        internal HotelRoomSelection DiagnosticSavedSelection;
        internal void TraceRoomPoseCatalog(string stage, object supplied = null, bool? allowed = null, bool details = false)
        {
            if (RoomPoseDiagnostics?.Invoke() != true || RoomPoseTrace == null) return;
            HotelPoseCatalogDiagnostics.Write(RoomPoseDiagnostics, RoomPoseTrace, stage, () =>
            {
                var entries = supplied as List<HSceneProc.AnimationListInfo>;
                if (entries != null) return entries.Select(Describe);
                var suppliedLists = supplied as List<HSceneProc.AnimationListInfo>[];
                if (suppliedLists != null) return suppliedLists.Where(list => list != null).SelectMany(list => list).Select(Describe);
                if (supplied != null && !ReferenceEquals(supplied, _proc)) return Array.Empty<HotelRoomSelection>();
                var lists = _proc == null ? null : (List<HSceneProc.AnimationListInfo>[])AccessTools.Field(typeof(HSceneProc), "lstUseAnimInfo").GetValue(_proc);
                return (lists ?? Array.Empty<List<HSceneProc.AnimationListInfo>>()).Where(list => list != null).SelectMany(list => list).Select(Describe);
            }, DiagnosticSavedSelection, () => "scene=" + (_proc == null ? "none" : RuntimeHelpers.GetHashCode(_proc).ToString())
                + " roomReady=" + RoomReady + " supplied=" + (supplied == null ? "current" : RuntimeHelpers.GetHashCode(supplied).ToString()) + " providerAllowed=" + allowed,
                details);
        }
    }
}
