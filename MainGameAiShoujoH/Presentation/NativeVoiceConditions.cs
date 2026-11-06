using System;
using System.Linq;

namespace MainGameAiShoujoH.Presentation
{
    public sealed class NativeVoiceContext
    {
        public int Personality, PersonalPhase, Slot, Bust, Initiative, Mode, MotionId, OldFinish, FinishCount;
        public float Mood, StartMoodLower, Skill2, HouchiTime, HouchiLimit = 20;
        public bool Force, Faint, Sleeping, Fast, Urine, Merchant, FemaleInitiative;
        public bool MapH, Yobai, NormalStartEvent, EndTaskMasturbation;
        public bool GaugeHit;
        public int Place;
        public int[] Skills = Array.Empty<int>();
        public string Evidence;
        public bool Confirmed;
        public void Validate()
        {
            if (!Confirmed || string.IsNullOrWhiteSpace(Evidence)) throw new InvalidOperationException("Native AI voice character context has no confirmed source.");
            if (Slot < 0 || Slot > 1 || (Personality < 0 || Personality > 5) && !(Personality == -90 && Merchant) || PersonalPhase < 0 || Bust < 0 || Bust > 2
                || !Finite(Mood) || Mood < 0 || Mood > 100 || !Finite(StartMoodLower) || StartMoodLower < 0 || StartMoodLower > 100
                || !Finite(Skill2) || Skill2 < 0 || Skill2 > 99999) throw new ArgumentException("Invalid native AI voice context.");
        }
        private static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
    }
    public static class NativeVoiceConditions
    {
        public static bool Motion(int[] ids, int motion, VoiceKind kind)
        {
            if (ids == null || ids.Length == 0) return kind == VoiceKind.Breath || kind == VoiceKind.ShortBreath;
            return ids.Contains(-1) || ids.Contains(motion);
        }
        public static bool Breath(int[] conditions, NativeVoiceContext c)
        {
            return conditions.All(n => n == 0 ? c.PersonalPhase <= 1 : n == 1 ? c.PersonalPhase >= 2 : n == 2 ? c.Skill2 < 170 : n == 3 ? c.Skill2 >= 170 : n == 4 ? !c.Sleeping : n == 5 ? c.Sleeping : n == 6 ? !c.Fast : n == 7 ? c.Fast : n == 8 ? !c.Urine : n == 9 ? c.Urine : true);
        }
        public static bool Short(int condition, NativeVoiceContext c)
        {
            switch (condition)
            {
                case 0: return !c.Force && !c.FemaleInitiative && !c.Faint && c.PersonalPhase <= 1;
                case 1: return !c.Force && !c.FemaleInitiative && !c.Faint && c.PersonalPhase >= 2;
                case 2: return c.Force && !c.Faint;
                case 3: return c.FemaleInitiative && !c.Faint;
                case 4: return c.Faint && !c.Sleeping;
                case 5: return c.Faint && c.Sleeping;
                case 6: return c.Skill2 < 170;
                case 7: return c.Skill2 >= 170;
                default: return true;
            }
        }
        public static bool VoiceCharacter(int condition, NativeVoiceContext c, bool accurate = false)
        {
            if (accurate)
            {
                switch (condition)
                {
                    case 0: return c.PersonalPhase < 2 && !c.Force && !c.Faint;
                    case 1: return c.PersonalPhase >= 2 && !c.Force && !c.Faint;
                    case 2: return c.Force && !c.Faint;
                    case 3: return !c.Faint;
                    case 4: return c.Faint;
                    default: return true;
                }
            }
            if (c.Faint) return condition == 4;
            return condition == 0 ? c.PersonalPhase < 2 : condition == 1 ? c.PersonalPhase >= 2 : condition != 4;
        }
        public static bool VoiceSpecific(int[] conditions, NativeVoiceContext c)
        {
            return conditions.All(n => n == 0 ? c.HouchiTime >= c.HouchiLimit : n == 1 ? c.Bust == 0 : n == 2 ? c.Bust == 2 : n == 3 ? c.Bust == 1 : n == 4 || n == 5 ? c.FinishCount >= 1 : n == 6 ? c.Skill2 >= 170 : n == 7 ? c.Skill2 < 170 : n == 8 ? c.Slot == 0 : n == 9 ? c.Slot == 1 : n == 10 ? c.OldFinish == 0 : n == 11 ? c.OldFinish == 2 : n == 12 ? c.OldFinish == 3 : n == 13 ? c.Initiative != 0 : n == 14 ? c.OldFinish == 1 : true);
        }
        public static bool StartSpecific(int[] conditions, NativeVoiceContext c)
        {
            return conditions.All(n => n == 0 ? c.Bust == 0 : n == 1 ? c.Bust == 2 : n == 2 ? c.Bust == 1 : n == 3 ? c.Skill2 >= 170 : n == 4 ? c.Skill2 < 170 : n == 5 ? c.Slot == 0 : n == 6 ? c.Slot == 1 : n == 7 ? c.Initiative != 0 : true);
        }
    }
}
