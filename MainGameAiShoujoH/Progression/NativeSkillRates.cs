using System;
using System.Linq;

namespace MainGameAiShoujoH.Progression
{
    public static class NativeSkillRates
    {
        // Source: each Proc's SkilChangeSpeed branches, not a shared all-skills product.
        public static void Apply(ProcDefinition d, int[] skills, bool merchant, Func<int, float> value, ref float female, ref float male)
        {
            if (merchant) return;
            bool Has(int id) => skills != null && skills.Contains(id);
            bool System(int id) => (d.Systems & (1 << id)) != 0;
            var family = d.Family;
            if (family == ProcKind.Houshi) { if (Has(18)) male *= value(18); return; }
            if (family == ProcKind.Sonyu)
            { if (Has(1) && System(1)) male *= value(1); if (Has(15) && System(3)) female *= value(15); }
            else if (family == ProcKind.Aibu)
            {
                if (Has(0) && System(2)) female *= value(0);
                else if (d.Kind != ProcKind.MultiPlay && Has(6) && System(1)) female *= value(6);
                else if (Has(15) && System(3)) female *= value(15);
            }
            else if (family == ProcKind.Les && Has(0) && System(2)) female *= value(0);
            if (family != ProcKind.Peeping && Has(3)) female *= value(3);
        }
    }
}
