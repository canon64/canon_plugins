using System;
using System.Runtime.Serialization;

namespace MainGameAiShoujoH.Integration
{
    [DataContract]
    public sealed class NativeHSettings
    {
        [DataMember] public int Schema = 1;
        [DataMember] public bool StopFemale, StopMale, Drug5, Constraint, SleepStart, Force, Futanari;
        [DataMember] public bool HentaiUnlock0, HentaiUnlock1;
        // Saved native ConfigScene.HSystem fields and Init both use true.
        [DataMember] public bool PlayerClothesEnabled = true, PlayerAccessoriesEnabled = true;
        [DataMember] public float FemaleSkillMultiplier = 1, MaleSkillMultiplier = 1, YobaiBareRate = 50;
        [DataMember] public int InvitePattern = -1;
        [DataMember] public string Origin = "専用H新規設定: 未所持技能/未投薬、非固定・非強制・非拘束。YobaiBareRate=原本HSceneFlagCtrl初期50、Invite=-1/SleepStart=falseはHScene終了初期化値";
        [OnDeserialized] void Loaded(StreamingContext context)
        { if (Schema == 0) { PlayerClothesEnabled = PlayerAccessoriesEnabled = true; Schema = 1; } }
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Origin) || !Finite(FemaleSkillMultiplier) || !Finite(MaleSkillMultiplier)
                || FemaleSkillMultiplier <= 0 || MaleSkillMultiplier <= 0 || !Finite(YobaiBareRate) || YobaiBareRate < 0 || YobaiBareRate > 100
                || InvitePattern < -1 || InvitePattern > 1) throw new ArgumentException("専用H開始設定が不正です。");
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
