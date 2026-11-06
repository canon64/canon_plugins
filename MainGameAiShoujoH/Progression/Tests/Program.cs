using System;
using System.Linq;
using MainGameAiShoujoH.Progression;

internal static class Program
{
    private static int checks;
    private static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    private static void Main()
    {
        foreach (var pair in new[] {
            ("0:0", "aibu:0"), ("aibu/0", "aibu:0"), ("aibu:0", "aibu:0"),
            ("1:0", "houshi:0"), ("2:0", "sonyu:0"), ("3:10", "tokushu:10"),
            ("4:0", "les:0"), ("5:0", "3P_F2M1:0"), ("3P_F2M1/0", "3P_F2M1:0") })
        {
            string canonical = NativeProcCatalog.CanonicalId(pair.Item1);
            Check(canonical == pair.Item2 && NativeProcCatalog.CanonicalId(canonical) == canonical, "Explicit native category alias and canonical stability: " + pair.Item1);
        }
        foreach (string invalid in new[] { "6:0", "aibu:999999", "unknown:0", "0/0", " aibu:0", "AIBU:0", "aibu:00" })
        {
            bool rejected = false; try { NativeProcCatalog.CanonicalId(invalid); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "Unknown or non-authored aliases must not fall back: " + invalid);
        }
        bool nullRejected = false; try { NativeProcCatalog.CanonicalId(null); } catch (ArgumentNullException) { nullRejected = true; }
        Check(nullRejected, "Null native alias is rejected");
        var a = new NativeProgression(NativeProcCatalog.Get("aibu/0"));
        a.Advance(new DriveSample { Wheel = 1, Voice0 = true }); Check(a.State == "Idle", "Aibu must wait for voice before requesting start");
        var effects = a.Advance(new DriveSample { Wheel = 1 }); Check(effects.Any(e => e.Name == "start-voice" && e.Amount == 2), "Aibu initial start must request timing 2");
        a.Advance(new DriveSample { Voice1 = true }); Check(a.State == "Idle", "Aibu must wait for second participant voice");
        a.Advance(new DriveSample()); Check(a.State == "WLoop", "Aibu start after voice completion");
        a.Advance(new DriveSample { Delta = 10, Hit = false }); Check(a.Female == 0, "Manual Aibu non-hit must not accumulate");
        a.Advance(new DriveSample { Finish = Finish.Before }); Check(a.State == "OLoop" && a.Female == .7f, "Aibu finish-before exact native threshold");
        a.Advance(new DriveSample { Delta = 11, Hit = true }); Check(a.State == "Orgasm" && a.OrgasmCount == 1, "Aibu female finish");
        a.Advance(new DriveSample { NormalizedTime = .99f }); Check(a.State == "Orgasm", "Aibu normalized terminal wait");
        a.Advance(new DriveSample { NormalizedTime = 1 }); Check(a.State == "Orgasm_A", "Aibu aftermath");
        var h = new NativeProgression(NativeProcCatalog.Get("houshi/0"));
        h.Advance(new DriveSample { Wheel = 1 }); h.Advance(new DriveSample()); h.Advance(new DriveSample { Finish = Finish.Before });
        h.Advance(new DriveSample { Delta = 20, Hit = false }); Check(h.State == "OLoop" && h.Male == 1, "Houshi manual male=1 must not auto-finish");
        h.Advance(new DriveSample { Finish = Finish.Outside }); Check(h.State == "Orgasm_OUT", "Houshi explicit outer finish");
        var m = new NativeProgression(NativeProcCatalog.Get("tokushu/1"));
        m.Advance(new DriveSample { Delta = 4.9f }); Check(m.State == "Idle", "Native masturbation start timer is 5 seconds");
        m.Advance(new DriveSample { Delta = .1f }); Check(m.State == "WLoop", "Native masturbation autonomous start");
        m.Advance(new DriveSample { Delta = 9 }); Check(m.State == "MLoop", "Native masturbation first .25 phase");
        m.Advance(new DriveSample()); m.Advance(new DriveSample { Delta = 9 }); Check(m.State == "SLoop", "Native masturbation .5 phase");
        m.Advance(new DriveSample()); m.Advance(new DriveSample { Delta = 6 }); Check(m.State == "SLoop", "Auto masturbation must not use manual .7 threshold");
        m.Advance(new DriveSample { Delta = 2 }); Check(m.State == "OLoop", "Auto masturbation .75 threshold");
        m.Stop(); var before = m.State; m.Advance(new DriveSample { Delta = 100 }); Check(m.State == before, "Stopped generation cannot progress");
        Check(NativeProcCatalog.Get("tokushu/10").Kind == ProcKind.Aibu, "Triangular horse native dispatch is Aibu, not Peeping");
        Check(NativeProcCatalog.Get("les/0").Kind == ProcKind.Les, "Les catalog number differs from Proc index");
        foreach (var key in NativeAutoCurve.Keys) Check(Math.Abs(NativeAutoDriver.Evaluate(key[0]) - key[1]) < 1e-7, "HAuto exact serialized curve key");
        var automatic = new NativeProgression(new ProcDefinition { Kind = ProcKind.Aibu, Initiative = 1 });
        var auto = new NativeAutoDriver((lo, hi) => lo, (lo, hi) => lo);
        Check(Math.Abs(auto.SecondsToBoundary(automatic, .1f, .2f) - 5) < .0001, "HAuto next native start deadline");
        var autoSample = new DriveSample { Delta = 4.9f }; auto.Advance(automatic, autoSample, .1f, .2f);
        Check(!autoSample.AutoStart, "HAuto does not start before native five seconds");
        autoSample.Delta = .1f; auto.Advance(automatic, autoSample, .1f, .2f);
        Check(autoSample.AutoStart, "HAuto signals native start at boundary");
        float f = 1, maleRate = 1;
        NativeSkillRates.Apply(new ProcDefinition { Kind = ProcKind.Aibu, Systems = (1 << 2) | (1 << 1) }, new[] { 0, 6, 3 }, false, id => id == 0 ? 2 : id == 3 ? 3 : 7, ref f, ref maleRate);
        Check(f == 6 && maleRate == 1, "Aibu native skill else-if priority plus independent skill3");
        f = maleRate = 1;
        NativeSkillRates.Apply(new ProcDefinition { Kind = ProcKind.Sonyu, Systems = (1 << 3) | (1 << 1) }, new[] { 15, 1, 3 }, false, id => id == 15 ? 2 : id == 3 ? 3 : 5, ref f, ref maleRate);
        Check(f == 6 && maleRate == 5, "Sonyu native female and male skill paths remain separate");
        f = maleRate = 1;
        NativeSkillRates.Apply(new ProcDefinition { Kind = ProcKind.Houshi }, new[] { 18, 3 }, false, id => 4, ref f, ref maleRate);
        Check(f == 1 && maleRate == 4, "Houshi skill18 affects male only");
        f = maleRate = 1;
        NativeSkillRates.Apply(new ProcDefinition { Kind = ProcKind.Spanking }, new[] { 3 }, true, id => 4, ref f, ref maleRate);
        Check(f == 1 && maleRate == 1, "Merchant native skill bypass");
        var select = new NativeMotionSelectionContext { PlayerSex = 0, Place = 0, HasItem = (mode, id) => true };
        Check(NativeMotionEligibility.Check(NativeProcCatalog.Get("aibu:0"), select, false), "Authored kiss0 native normal selection");
        select.Force = true; Check(!NativeMotionEligibility.Check(NativeProcCatalog.Get("aibu:0"), select, false), "Force rejects authored refusal0 kiss"); select.Force = false;
        select.Place = 17; Check(!NativeMotionEligibility.Check(NativeProcCatalog.Get("aibu:0"), select, false), "Native place17 does not alias floor0"); select.Place = 0;
        select.PlayerSex = 1; Check(!NativeMotionEligibility.Check(NativeProcCatalog.Get("aibu:0"), select, false), "Female player rejects non-Les promiscuity0"); select.PlayerSex = 0;
        Check(!NativeMotionEligibility.Check(NativeProcCatalog.Get("aibu:0"), select, true), "No skill13 cannot expose automatic main control");
        Check(!NativeHItemRequirements.Check(3, 3, id => false) && NativeHItemRequirements.Check(3, 3, id => id == 3), "Native camera motion requires category14 item3");
        Check(NativeHItemRequirements.Check(3, 2, id => false), "Native kind0 dildo is not a carried item requirement");
        var spank = new NativeProgression(new ProcDefinition { Kind = ProcKind.Spanking });
        spank.Advance(new DriveSample { Wheel = 1 });
        Check(Math.Abs(spank.SecondsToBoundary(new DriveSample()) - .3f) < .00001f, "Spanking upFeel has one native .3 second boundary");
        spank.Advance(new DriveSample { Delta = .3f }); float afterSpank = spank.Female;
        spank.Advance(new DriveSample { Delta = 2 }); spank.Advance(new DriveSample());
        Check(Math.Abs(spank.Female - afterSpank) < .000001f, "Spanking action does not add again after upFeel ends");
        Console.WriteLine("NATIVE_PROGRESSION_CHECKS_PASS " + checks);
    }
}
