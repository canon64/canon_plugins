using System;
using MainGameAiShoujoH.Contracts;

namespace MainGameAiShoujoH.Integration
{
    public sealed class EndingConditions
    {
        public NativeHEvent Event;
        public bool Merchant, Weakness, Force, SleepStart, SecondFemaleIsAgent, HadControl, PainSelected, ConstraintSelected, PainAction, ConstraintAction;
        public int InvitePattern = -1, Phase, FemaleOrgasm, MaleFinish, FemalePlayerFinish;
        public float DirtySkill, YobaiBareRate;
    }
    public sealed class EndingSelection
    {
        public string Scenario, JumpTag = "";
        public int ConditionMode, FemaleOrgasm;
        public bool Weakness, PainAction, ConstraintAction, CloseWithoutScenario;
        // Exact EndProcADV branch order from saved HScene:5085-5475.
        public static EndingSelection Select(EndingConditions c, int percentRoll)
        {
            if (c == null || percentRoll < 0 || percentRoll >= 100) throw new ArgumentException("Invalid ending conditions.");
            var result = new EndingSelection { Weakness = c.Weakness };
            string Weak(string normal) => c.Weakness ? "1" : normal;
            int Condition() => c.Force ? 2 : c.Phase >= 2 ? 1 : 0;
            if (c.Merchant) { result.Scenario = "0"; result.JumpTag = "OUT"; return result; }
            switch (c.Event)
            {
                case NativeHEvent.Yobai:
                    if (percentRoll < c.YobaiBareRate) result.Scenario = "11";
                    else result.CloseWithoutScenario = true;
                    return result;
                case NativeHEvent.GyakuYobai:
                    result.Scenario = c.SleepStart ? c.Weakness ? "14" : "13" : Weak("7"); return result;
                case NativeHEvent.Bath: case NativeHEvent.Toilet1: case NativeHEvent.Toilet2: case NativeHEvent.ShagmiBare:
                case NativeHEvent.Back: case NativeHEvent.Kitchen: case NativeHEvent.Tachi: case NativeHEvent.Stairs:
                case NativeHEvent.StairsBare: case NativeHEvent.MapBath: case NativeHEvent.KabeanaBack:
                case NativeHEvent.KabeanaFront: case NativeHEvent.Neonani: case NativeHEvent.TsukueBare:
                    result.Scenario = Weak("9"); return result;
            }
            if (c.Event != NativeHEvent.FromFemale || c.InvitePattern != -1)
            {
                if (c.SecondFemaleIsAgent) { result.Scenario = Weak("10"); return result; }
                if ((c.PainSelected || c.ConstraintSelected) && (c.Force || c.DirtySkill < 170))
                {
                    result.Scenario = Weak(c.PainSelected ? "5" : "12"); result.ConditionMode = 2;
                    if (!c.Weakness) { result.PainAction = c.PainSelected && c.PainAction; result.ConstraintAction = !c.PainSelected && c.ConstraintAction; }
                    return result;
                }
                result.ConditionMode = Condition();
                bool noResults = c.FemaleOrgasm == 0 && c.MaleFinish == 0 && c.FemalePlayerFinish == 0;
                if (!c.HadControl || noResults) result.Scenario = "4";
                else if (c.MaleFinish > 0 || c.FemalePlayerFinish > 0)
                {
                    result.Scenario = Weak(c.FemaleOrgasm == 0 ? "3" : "0");
                    if (!c.Weakness && c.FemaleOrgasm > 0) result.FemaleOrgasm = c.FemaleOrgasm;
                }
                else result.Scenario = Weak("2");
                return result;
            }
            if (c.Event == NativeHEvent.FromFemale || c.InvitePattern == 0) result.Scenario = Weak("6");
            else if (c.InvitePattern == 1) result.Scenario = Weak("8");
            else throw new InvalidOperationException("Native ending branch has no selected scenario.");
            result.ConditionMode = Condition(); return result;
        }
    }
}
