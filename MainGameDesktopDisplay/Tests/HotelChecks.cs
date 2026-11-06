using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using BepInEx.Configuration;
using CanonGate;
using CanonPlugins.Shared;
using UnityEngine;

// Serialization/config boundaries only; all monitor selection, native-producer replacement,
// crop/mesh application, paused state and provider verification below run production methods.
namespace UnityEngine
{
    static class JsonUtility
    {
        static readonly JsonSerializerOptions Options=new(){IncludeFields=true,IgnoreReadOnlyProperties=true};
        internal static string ToJson(object o)=>JsonSerializer.Serialize(o,o.GetType(),Options);
        internal static T FromJson<T>(string s)=>JsonSerializer.Deserialize<T>(s,Options);
    }
}
namespace CanonPlugins.Shared
{
    sealed class GadgetRoomPose
    {
        public Vector3 position;
        internal void Apply(Transform room,Transform target){target.position=room.TransformPoint(position);}
    }
    sealed class GadgetRoomSnapshot
    {
        public bool visible;public string settings,runtime;public GadgetRoomPose[] poses;
        internal static GadgetRoomSnapshot Capture(Transform room,params Transform[] roots)=>new(){visible=roots.All(t=>t!=null),poses=roots.Where(t=>t!=null).Select(t=>new GadgetRoomPose{position=room.InverseTransformPoint(t.position)}).ToArray()};
        internal string Encode()=>JsonUtility.ToJson(this);
        internal static GadgetRoomSnapshot Decode(string s)=>JsonUtility.FromJson<GadgetRoomSnapshot>(s);
    }
    static class GadgetConfigSnapshot
    {
        internal static string Capture(ConfigFile config)=>JsonUtility.ToJson(config.values.ToDictionary(p=>p.Key,p=>Convert.ToString(p.Value.GetType().GetProperty("Value").GetValue(p.Value),CultureInfo.InvariantCulture)));
        internal static void Apply(ConfigFile config,string json)
        {
            if(string.IsNullOrEmpty(json))return;
            var values=JsonUtility.FromJson<Dictionary<string,string>>(json);
            foreach(var pair in values)
                if(config.values.TryGetValue(pair.Key,out var entry))
                {
                    var property=entry.GetType().GetProperty("Value");
                    property.SetValue(entry,Convert.ChangeType(pair.Value,property.PropertyType,CultureInfo.InvariantCulture));
                }
        }
    }
}
namespace MainGameDesktopDisplay
{
    public sealed partial class Plugin
    {
        static void CheckHotelRestore()
        {
            GateApi.Current=new(){Kind=GateApi.SessionKind.H,Id=800};GateApi.Allowed=true;GateApi.Generation++;
            var p=new Plugin();p.Awake();p.StartSession();p.Recall();var room=new Transform();
            p._monitorDevice.Value="secondary";p.RefreshMonitors(false,true);
            p._width.Value=1.7f;p._resolution.Value=640;
            p._clipDraft[0]=13;p._clipDraft[1]=24;p._clipDraft[2]=7;p._clipDraft[3]=16;p._clipDirty=true;p.CommitClipDraft();
            p._paused=true;p._driver.enabled=false;p._root.transform.position=new Vector3(4,5,6);
            string saved=p.CaptureHotelGadget(room);
            p._monitorDevice.Value="primary";p.RefreshMonitors(false,true);p._width.Value=3.1f;p._resolution.Value=1280;p.StartCapture();
            var root=p._root;var source=p._capture;
            p.RestoreHotelGadget(room,saved);
            Check(p._root==root&&p._capture!=source&&p._monitor==1&&p._capture.Source.Left<0,"hotel restore updates an existing display's actual selected monitor and producer");
            Check(p._width.Value==1.7f&&p._capture.Width==640&&p._clipDraft[0]==13&&p._clipDraft[1]==24,"hotel restores selected capture resolution width and crop");
            Check(p._paused&&!p._driver.enabled&&p._root.transform.position.x==4&&p.VerifyHotelGadgetState(room,saved),"hotel restores pause placement and rendered panel dimensions");
            p._panelTransform.localScale=new Vector3(99,99,1);Check(!p.VerifyHotelGadgetState(room,saved),"hotel verification rejects a panel that did not apply saved geometry");
            p.RestoreHotelGadget(room,saved);source=p._capture;var bad=GadgetRoomSnapshot.Decode(saved);
            var settings=JsonUtility.FromJson<Dictionary<string,string>>(bad.settings);settings["Desktop/Monitor"]="unplugged";bad.settings=JsonUtility.ToJson(settings);
            try {p.RestoreHotelGadget(room,bad.Encode());throw new Exception("missing monitor accepted");}catch(InvalidOperationException){}
            Check(p._capture==source&&p._monitorDevice.Value=="secondary"&&p.VerifyHotelGadgetState(room,saved),"missing saved monitor rejects before replacing good capture and preserves old settings");
            p.PlaybackBarDelete();p.RestoreHotelGadget(room,saved);Check(p.VerifyHotelGadgetState(room,saved),"hotel creates a missing display with saved capture and paused state");
            p.StopSession();p.OnDestroy();
        }
    }
}
