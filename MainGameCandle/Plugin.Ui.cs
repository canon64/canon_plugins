using System.Globalization;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace MainGameCandle
{
    public sealed partial class Plugin
    {
        readonly Dictionary<string,string> _drafts=new Dictionary<string,string>();
        void DrawTab()
        {
            if(!CanAct){GUILayout.Label("許可されたHシーンを開始してください。無効化後の再開は次のHからです。");GUILayout.Label(_status);return;}
            GUILayout.BeginVertical(GUI.skin.box);GUILayout.Label("ロウソクの操作");GUILayout.BeginHorizontal();
            if(GUILayout.Button("呼び出し",GUILayout.Width(100))) { try{Recall();}catch(System.Exception ex){DeleteCandle();_status=ex.Message;} }
            if(GUILayout.Button("ギズモ",GUILayout.Width(100))) { try{ShowGizmo();}catch(System.Exception ex){HideGizmo();_status=ex.Message;} }
            if(GUILayout.Button("ロウソク削除",GUILayout.Width(130)))PlaybackBarDelete();
            GUILayout.EndHorizontal();
            _lit.Value=GUILayout.Toggle(_lit.Value,"点火（炎・灯り・自動滴下）");
            Slider("芯・炎の高さ",_wickHeight,6,40,"mm");
            if(_gizmo!=null&&_gizmo.IsVisible)
            {
                GUILayout.BeginHorizontal();GUILayout.Label("ギズモ",GUILayout.Width(65));
                if(GUILayout.Button("移動",GUILayout.Width(70)))_gizmo.SetMode(MainGameTransformGizmo.GizmoMode.Move);
                if(GUILayout.Button("回転",GUILayout.Width(70)))_gizmo.SetMode(MainGameTransformGizmo.GizmoMode.Rotate);
                GUILayout.EndHorizontal();
            }
            GUILayout.Label("VR：グリップで保持／解放で固定。135度以上で消火、135度未満に戻して保持手のトリガーで再点火。");GUILayout.EndVertical();
            GUILayout.BeginVertical(GUI.skin.box);GUILayout.Label("蝋の量と反応");
            Slider("滴下開始角度",_startAngle,0,85,"度");Slider("滴下頻度",_flowMultiplier,0,5,"倍");
            Slider("一粒の量",_dropVolume,.25f,20,"倍");Slider("ばらつき",_variation,0,100,"%");
            Slider("蝋の厚み",_waxThickness,0,5,"mm");
            GUILayout.Label("厚みは付着済みの蝋にも即反映します。0mmで平面に戻します。");
            GUILayout.Label("初期値は水平で平均約1.5滴/秒、一粒の基準量は旧版の3倍。傾けるほど頻度が上がり、長く溜めるほど大粒になります。135度以上で消火。");
            GUILayout.Label("蝋は顔・体の肌だけに付着します。服や物は通り抜けます。");
            _voice.Value=GUILayout.Toggle(_voice.Value,"肌への付着時に短い喘ぎ声（既存Bridge）");
            _expression.Value=GUILayout.Toggle(_expression.Value,"肌への付着時に短い表情反応");
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("蝋を全消去",GUILayout.Width(130)))ClearWax();
            if(GUILayout.Button("付着対象を再取得",GUILayout.Width(170)))RefreshSurfaces();
            GUILayout.EndHorizontal();
            GUILayout.Label("付着した蝋はロウソク削除後も残ります。H終了で全消去します。");GUILayout.EndVertical();
            GUILayout.Label(_status);
            var e=Event.current;if(e!=null&&(e.rawType==EventType.MouseUp||e.rawType==EventType.KeyUp))SaveSettings();
            // This is a child tab. Both hosts already register the entire viewport, retain
            // pointer capture with Sync, and clear it when closed; do not create a second lease.
        }
        void Slider(string label,ConfigEntry<float> entry,float min,float max,string unit)
        {
            GUILayout.BeginHorizontal();GUILayout.Label(label,GUILayout.Width(110));
            float next=GUILayout.HorizontalSlider(entry.Value,min,max,GUILayout.Width(180));
            if(next!=entry.Value){entry.Value=next;_drafts[entry.Definition.Key]=next.ToString("0.###",CultureInfo.InvariantCulture);}
            string key=entry.Definition.Key;if(!_drafts.TryGetValue(key,out string text))text=entry.Value.ToString("0.###",CultureInfo.InvariantCulture);
            string draft=GUILayout.TextField(text,GUILayout.Width(65));_drafts[key]=draft;
            if(draft!=text&&float.TryParse(draft,NumberStyles.Float,CultureInfo.InvariantCulture,out float n)&&!float.IsNaN(n)&&!float.IsInfinity(n)&&n>=min&&n<=max)entry.Value=n;
            GUILayout.Label(unit,GUILayout.Width(35));GUILayout.EndHorizontal();
        }
    }
}
