using System;
using BepInEx.Configuration;
using UnityEngine;
using MainGameTransformGizmo;
namespace MainGameSyringe
{
 public sealed partial class Plugin
 {
  ConfigEntry<float> _needleWidth;ConfigEntry<bool> _autoStick,_nearSurface;
  void BindContactSettings()
  {
   _needleWidth=NumberSetting("Insertion","NeedleWidth",0.004f,0,.02f,"針先接触判定の幅（直径、m）");
   _autoStick=Config.Bind("Desktop","StickAfterGizmoDrag",true,"ギズモ操作終了時に一度だけ刺入を判定する。");
   _nearSurface=Config.Bind("Insertion","NearestSkinContact",true,"針先から許容距離内にある腕・太もも・首の肌へ現在の角度で固定する。OFFでは針軸と判定幅で接触を要求する。");
  }
  void GizmoDragChanged(bool dragging)
  {if(!dragging&&Allowed&&InteractionFocused&&!_vr&&_autoStick.Value&&_model!=null&&_insertedBone==null)TryInsert(true);}
  void DrawContactSettings()
  {
   GUILayout.Label("刺入調整");
   bool near=GUILayout.Toggle(_nearSurface.Value,"近くの肌へ固定する（角度を保持）");if(near!=_nearSurface.Value)_nearSurface.Value=near;
   UiLabel(_nearSurface.Value?"針先から肌までが設定距離内なら固定します。刺入と投与は別操作です。":"針軸の延長線と肌の接触が必要です。近くても針が横を向くと失敗します。");
   if(GUILayout.Button("調整用の値を入れる（距離5cm・幅4mm・腕8cm・太もも15cm）"))
   {_nearSurface.Value=true;_stickDistance.Value=.05f;_needleWidth.Value=.004f;_armRadius.Value=.08f;_thighRadius.Value=.15f;}
   ContactNumber("肌に届く距離 cm",_stickDistance,100,.1f,20);
   ContactNumber("針先の判定幅 mm",_needleWidth,1000,0,20);
   ContactNumber("腕の判定範囲 cm",_armRadius,100,1,30);
   ContactNumber("太ももの判定範囲 cm",_thighRadius,100,1,40);
   ContactNumber("首の判定範囲 cm",_neckRadius,100,1,15);
   ContactNumber("刺す深さ cm",_insertionDepth,100,0,10);
   UiLabel("距離＝針先から肌まで。判定範囲＝骨の周囲で腕・太もも・首として認める範囲。深さは刺入後の見た目です。");
   GUILayout.Label("最終判定（操作時に更新）");UiLabel(_probeReport);
   if(_vr)return;
   bool auto=GUILayout.Toggle(_autoStick.Value,"ギズモ操作終了時に自動で刺す");if(auto!=_autoStick.Value)_autoStick.Value=auto;
   bool enabled=GUI.enabled;GUI.enabled=enabled&&_model!=null&&_insertedBone==null;
   GUILayout.BeginHorizontal();
   if(GUILayout.Button("判定だけ確認"))TryInsert(true,false);
   if(GUILayout.Button("今の位置で刺す"))TryInsert(true);
   GUI.enabled=enabled&&_insertedBone!=null;
   if(GUILayout.Button("抜く"))WithdrawNeedle();
   GUILayout.EndHorizontal();GUI.enabled=enabled;
   bool show=GUILayout.Toggle(_showGizmo.Value,"ギズモ表示（OFFで消す）");if(show!=_showGizmo.Value)_showGizmo.Value=show;
   ContactNumber("ギズモの倍率",_gizmoSize,1,.2f,1);
   GUI.enabled=enabled&&_model!=null&&_insertedBone==null;
   GUILayout.BeginHorizontal();
   if(GUILayout.Button("移動")){_showGizmo.Value=true;SyncGizmoVisibility();_gizmo?.SetMode(GizmoMode.Move);}
   if(GUILayout.Button("回転")){_showGizmo.Value=true;SyncGizmoVisibility();_gizmo?.SetMode(GizmoMode.Rotate);}
   GUILayout.EndHorizontal();GUI.enabled=enabled;
  }
  void ContactNumber(string label,ConfigEntry<float> entry,float units,float min,float max)
  {
   GUILayout.BeginHorizontal();GUILayout.Label(label,GUILayout.Width(145));
   float old=entry.Value*units;float value=GUILayout.HorizontalSlider(old,min,max,GUILayout.Width(220));
   if(Math.Abs(value-old)>.00001f)entry.Value=(float)Math.Round(value/units,6);
   bool valid=NumberInput("display."+entry.Definition.Section+entry.Definition.Key,entry.Value*units,out float typed);
   bool enabled=GUI.enabled;GUI.enabled=enabled&&valid&&typed>=min&&typed<=max;
   if(GUILayout.Button("適用",GUILayout.Width(45)))entry.Value=typed/units;
   GUI.enabled=enabled;GUILayout.EndHorizontal();
  }
 }
}
