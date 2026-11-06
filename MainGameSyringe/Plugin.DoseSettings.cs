using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
namespace MainGameSyringe
{
 public sealed partial class Plugin
 {
  readonly Dictionary<ChaControl,DoseLedger> _doses=new Dictionary<ChaControl,DoseLedger>();
  ConfigEntry<int> _maximumDoses;ConfigEntry<float> _maximumOrbit,_orbitSpeed;
  void BindDoseSettings()
  {
   _maximumDoses=Config.Bind("PupilOrbit","MaximumDoses",5,new ConfigDescription("半径が最大に達する注射回数",new AcceptableValueRange<int>(1,100)));
   _maximumOrbit=NumberSetting("PupilOrbit","MaximumRadiusPercent",10,0,50,"最大半径（瞳画像幅の％）");
   _orbitSpeed=NumberSetting("PupilOrbit","CyclesPerSecond",.5f,0,5,"瞳の円運動の速度（周／秒）");
  }
  bool ApplyDose(ChaControl actor,DoseLedger ledger,out double next)
  {
   ledger.Evaluate(Time.realtimeSinceStartup,_maximumDoses.Value,_maximumOrbit.Value/100,out int opacity,out float radius,out next);
   return MainGameSpiralEyes.PublicApi.SetOverlayOpacity(Guid,actor,opacity)&&MainGameSpiralEyes.PublicApi.SetPupilOrbit(Guid,actor,radius,_orbitSpeed.Value);
  }
  void RefreshDoseSettings()
  {foreach(var pair in new List<KeyValuePair<ChaControl,DoseLedger>>(_doses))if(pair.Key!=null)ApplyDose(pair.Key,pair.Value,out var unused);}
  void DrawDoseSettings()
  {
   UiLabel("注射ごとに効果時間を管理 / 左右逆回転・片目が上ならもう片目は下");
   GUILayout.BeginHorizontal();GUILayout.Label("最大到達回数",GUILayout.Width(145));
   bool valid=NumberInput("maxdoses",_maximumDoses.Value,out float count);bool enabled=GUI.enabled;GUI.enabled=enabled&&valid&&count>=1&&count<=100&&count==(int)count;
   if(GUILayout.Button("適用",GUILayout.Width(45)))_maximumDoses.Value=(int)count;GUI.enabled=enabled;GUILayout.EndHorizontal();
   ContactNumber("最大半径（画像幅％）",_maximumOrbit,1,0,50);ContactNumber("回転速度 周／秒",_orbitSpeed,1,0,5);
  }
 }
}
