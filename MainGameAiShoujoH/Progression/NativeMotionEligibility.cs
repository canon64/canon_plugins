using System;
using System.Linq;
namespace MainGameAiShoujoH.Progression
{
    public sealed class NativeMotionSelectionContext
    {
        public int PlayerSex, Place, Initiative;
        public bool Futanari, SecondFemale, Merchant, Force, Yobai, Faint, Unlock0, Unlock1;
        public int[] Skills = Array.Empty<int>();
        public Func<int, int, bool> HasItem;
    }
    public static class NativeMotionEligibility
    {
        // HSceneSprite.CheckMotionLimit1703 and CheckAutoMotionLimit1842.
        public static bool Check(ProcDefinition d, NativeMotionSelectionContext c, bool automatic)
        {
            if (c.PlayerSex == 0 || c.PlayerSex == 1 && c.Futanari)
            {
                if (c.SecondFemale ? d.Promiscuity != 1 : d.Promiscuity == 1) return false;
                if (d.Promiscuity == 2) return false;
                if (d.ActionMode == 3 && d.MotionId != 3 && d.MotionId != 13 && d.MotionId != 14 && !d.HasMaleFile) return false;
            }
            else if (c.PlayerSex == 1 && d.Promiscuity < 2) return false;
            if (automatic && ((!c.Merchant && (c.Yobai || c.Force)) || c.Faint || !c.Skills.Contains(13))) return false;
            if (!c.Merchant)
            { if (d.Hentai == 1 && !c.Unlock0 || d.Hentai == 2 && !c.Unlock1) return false; }
            else if (!d.MerchantMotion || !automatic && d.RefusalAction == 2) return false;
            if (!automatic && !c.Merchant)
            {
                if (c.Yobai) { if (!d.Sleep) return false; }
                else if (c.Force ? d.RefusalAction == 0 : d.RefusalAction == 2) return false;
            }
            if (c.Place == -1 ? !d.Positions.Contains(0) && !d.Positions.Contains(1) : !d.Positions.Contains(c.Place)) return false;
            if (d.NeedItem && (c.HasItem == null || !c.HasItem(d.ActionMode, d.MotionId))) return false;
            if (c.Faint && !d.CanDown || d.FaintnessLimit == 1 && (automatic || !c.Faint)) return false;
            if (automatic) return c.Initiative == 2 ? d.FemaleInitiative == 2 : d.FemaleInitiative != 0;
            return c.Initiative == 0 ? d.FemaleInitiative == 0 : c.Initiative == 1 ? d.FemaleInitiative != 0 : c.Initiative != 2 || d.FemaleInitiative == 2;
        }
    }
}
