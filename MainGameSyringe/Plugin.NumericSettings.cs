using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using UnityEngine;
namespace MainGameSyringe
{
 public sealed partial class Plugin
 {
  ConfigEntry<float> _stickDistance,_insertionDepth,_pullDistance,_probeMove,_probeAngle,_armRadius,_thighRadius,_neckRadius;
  ConfigEntry<float> _pusherStroke,_injectionSeconds,_remainingLiquid;
  ConfigEntry<Vector3> _spawnOffset;
  readonly Dictionary<string,string> _numericText=new Dictionary<string,string>();
  Vector2 _tabScroll;bool _showNumeric;
  void BindInsertionSettings()
  {
   _stickDistance=NumberSetting("Insertion","SkinDistance",.025f,.001f,.2f,"針先が肌へ吸着する距離（m）");
   _insertionDepth=NumberSetting("Insertion","Depth",.008f,0,.1f,"肌メッシュ内部へ刺す深さ（m）");
   _pullDistance=NumberSetting("Insertion","PullReleaseDistance",.12f,.01f,1f,"刺さった注射器の握り位置から手がこの距離以上離れると、投与せず手元へ戻る（m）。範囲内で再度トリガーを押すと投与。");
   _probeMove=NumberSetting("Insertion","SurfaceSampleMovement",.005f,.001f,.05f,"手の移動で肌メッシュを再計算する移動量（m）");
   _probeAngle=NumberSetting("Insertion","SurfaceSampleAngle",2f,.1f,45f,"肌メッシュを再計算する角度変化（度）");
   _armRadius=NumberSetting("Insertion","UpperArmRadius",.035f,.01f,.3f,"腕（上腕・前腕）の候補判定半径（m）");
   _thighRadius=NumberSetting("Insertion","ThighRadius",.065f,.01f,.4f,"太ももの候補判定半径（m）");
   _neckRadius=NumberSetting("Insertion","NeckRadius",.06f,.01f,.15f,"首（首骨から頭骨まで）の候補判定半径（m）");
   _pusherStroke=NumberSetting("Model","PusherStroke",.04f,0,.2f,"旧モデル互換用。新モデルの可動距離は40.5mmです。");
   _injectionSeconds=NumberSetting("Model","InjectionSeconds",3f,.1f,30f,"旧設定互換用。押し子の動作は注射・補充とも3秒に固定です。");
   var timingVersion=Config.Bind("Model","InjectionTimingVersion",0,"旧既定の投与時間を3秒へ移行する版数。");
   if(timingVersion.Value<2){if(Math.Abs(_injectionSeconds.Value-.45f)<.0001f||Math.Abs(_injectionSeconds.Value-2f)<.0001f)_injectionSeconds.Value=3f;timingVersion.Value=2;}
   _remainingLiquid=NumberSetting("Model","RemainingLiquid",.02f,0,1,"注射後の薬液の残量（0～1）");
   _spawnOffset=Config.Bind("Model","SpawnOffset",new Vector3(.18f,-.14f,.5f),"手元へ配置する視点からの位置（m）");
  }
  ConfigEntry<float> NumberSetting(string group,string key,float value,float min,float max,string description)
   =>Config.Bind(group,key,value,new ConfigDescription(description,new AcceptableValueRange<float>(min,max)));
  void DrawNumericSettings()
  {
   _showNumeric=GUILayout.Toggle(_showNumeric,"数値設定を開く（入力後、各行の適用を押す）");if(!_showNumeric)return;
   EditNumber("肌へ近づく距離 m",_stickDistance);EditNumber("刺す深さ m",_insertionDepth);EditNumber("引き抜く距離 m",_pullDistance);
   EditNumber("握れる距離 m",_gripRadius);
   EditNumber("腕の判定半径 m",_armRadius);EditNumber("太ももの判定半径 m",_thighRadius);
   EditNumber("肌の再計算移動量 m",_probeMove);EditNumber("肌の再計算角度 度",_probeAngle);
   EditNumber("螺旋が濃くなる秒数",_fadeIn);EditNumber("螺旋が消える秒数",_fadeOut);
   GUILayout.Label("押し子：注射・補充とも3秒 / モデルの移動距離40.5mm");EditNumber("注射後の薬液残量",_remainingLiquid);
   EditNumber("注射器の倍率",_scale);EditNumber("ギズモの倍率",_gizmoSize);
   EditVector("握り位置 m",_gripPosition);EditVector("握り角度 度",_gripAngles);EditVector("手元への配置 m",_spawnOffset);
  }
  bool NumberInput(string key,float value,out float result)
  {
   if(!_numericText.TryGetValue(key,out var text))text=value.ToString("0.###",CultureInfo.InvariantCulture);
   _numericText[key]=GUILayout.TextField(text,GUILayout.Width(80));
   return float.TryParse(_numericText[key],NumberStyles.Float,CultureInfo.InvariantCulture,out result)&&!float.IsNaN(result)&&!float.IsInfinity(result);
  }
  void EditNumber(string label,ConfigEntry<float> entry)
  {
   GUILayout.BeginHorizontal();GUILayout.Label(label,GUILayout.Width(150));bool valid=NumberInput(entry.Definition.Section+entry.Definition.Key,entry.Value,out float value);
   bool enabled=GUI.enabled;GUI.enabled=enabled&&valid;if(GUILayout.Button("適用",GUILayout.Width(45)))entry.Value=value;
   GUI.enabled=enabled;GUILayout.EndHorizontal();
  }
  void EditVector(string label,ConfigEntry<Vector3> entry)
  {
   GUILayout.Label(label+"（X / Y / Z）");GUILayout.BeginHorizontal();
   bool x=NumberInput(label+"x",entry.Value.x,out float vx),y=NumberInput(label+"y",entry.Value.y,out float vy),z=NumberInput(label+"z",entry.Value.z,out float vz);
   bool enabled=GUI.enabled;GUI.enabled=enabled&&x&&y&&z;if(GUILayout.Button("適用",GUILayout.Width(45)))entry.Value=new Vector3(vx,vy,vz);
   GUI.enabled=enabled;GUILayout.EndHorizontal();
  }
 }
}
