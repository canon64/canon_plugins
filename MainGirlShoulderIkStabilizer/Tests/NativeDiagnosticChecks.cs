using MainGirlShoulderIkStabilizer;
using U = UnityEngine;

static partial class Program
{
    static void NativeDiagnostics()
    {
        var expression=new ExpressionBone();bool enabled=false;
        foreach(ExpressionBone.BoneName name in Enum.GetValues<ExpressionBone.BoneName>())
            expression.Bones[(int)name]=new U.Transform{localPosition=new U.Vector3(.1f,.2f,.3f)};
        var lines=new List<string>();
        var observer=new NativeShoulderDiagnostic(expression,()=>enabled,lines.Add,.2f);
        Frame();long token=observer.Begin("expression_before");observer.Capture("expression_after",1);
        Check(token==0&&lines.Count==0&&expression.DictionaryReads==0,"OFF native observer does not read bones or write logs");
        enabled=true;token=observer.Begin("expression_before");
        Check(token>0&&expression.DictionaryReads>0,"ON samples real expression inputs");
        string output=lines.Last();
        foreach(string bone in new[]{"cf_d_shoulder02_L","cf_d_shoulder02_R","cf_d_arm01_L","cf_d_arm02_R","cf_d_arm03_L","n_goal_forearm_R"})
            Check(output.Contains("bone="+bone+" parent="),"native auxiliary bone observed: "+bone);
        Check(output.Contains("localQ=")&&output.Contains("localScale=")&&output.Contains("worldQ="),"position rotation scale and world pose recorded");
        Check(output.Contains("NATIVE_INPUT")&&output.Contains("nativeShoulderBendDeg="),"actual native shoulder formula inputs recorded");
        Check(observer.Begin("ik_completed")==0,"same interval does not start another native sample");
        foreach(var stage in new[]{"auto_before","auto_after","expression_after","shape_after"})observer.Capture(stage,token);
        Check(lines.Count==5&&lines.All(s=>s.Contains("nativePass="+token)),"one sample connects actual native/IK stage order");
        int reads=expression.DictionaryReads,count=lines.Count;enabled=false;
        observer.Capture("shape_after",token);Frame();observer.Begin("expression_before");
        Check(expression.DictionaryReads==reads&&lines.Count==count,"OFF immediately stops pending native sample and collection");
        enabled=true;U.Time.unscaledTime+=1;expression.DisableShoulder();
        expression.Bones.Remove((int)ExpressionBone.BoneName.cf_d_shoulder02_L);
        observer.Begin("expression_before");
        Check(lines.Last().Contains("enableShoulderL=False")&&lines.Last().Contains("bone=cf_d_shoulder02_L missing=true"),"disabled native shoulder and missing helper explicitly logged");
        Check(observer.Matches(expression)&&!observer.Matches(new ExpressionBone()),"other character expression is not matched");
        var plugin=new ShoulderIkStabilizerPlugin();var actor=new ChaControl{expression=expression};
        plugin.Configure(false,actor);Check(HarmonyLib.Harmony.ActivePatches==0,"OFF installs no native diagnostic hooks");
        plugin.Configure(true,actor);Check(HarmonyLib.Harmony.ActivePatches==3,"ON installs expression, initialization and shape observers");
        var failure=new InvalidOperationException("native exception");
        Check(ReferenceEquals(plugin.SimulateNative(expression,failure),failure),"native diagnostic finalizer preserves original exceptions");
        reads=expression.DictionaryReads;plugin.Configure(false,actor);count=plugin.Records.Count;
        plugin.SimulateNative(expression);
        Check(HarmonyLib.Harmony.ActivePatches==0&&plugin.Records.Count==count&&expression.DictionaryReads==reads,"OFF removes hooks and stale callbacks collect/write nothing");
        plugin.Configure(true,actor);plugin.Configure(true,actor);
        Check(HarmonyLib.Harmony.ActivePatches==3,"reconfiguration does not duplicate hooks");
        CanonPlugins.Local.SessionGate.Allowed=false;reads=expression.DictionaryReads;count=plugin.Records.Count;
        plugin.SimulateNative(expression);
        Check(expression.DictionaryReads==reads&&plugin.Records.Count==count,"revoked permit prevents native diagnostics");
        plugin.Configure(true,null,false);
        Check(HarmonyLib.Harmony.ActivePatches==0,"session end removes native diagnostic hooks");
        CanonPlugins.Local.SessionGate.Allowed=true;
        var noExpressionActor=new ChaControl();
        noExpressionActor.objBodyBone.Components.Add(new U.Transform{name="cf_s_arm01_L"});
        noExpressionActor.objBodyBone.Components.Add(new U.Transform{name="cf_j_arm00_L"});
        plugin.Configure(true,noExpressionActor);
        Frame();plugin.ObserveIk("auto_before");
        Check(plugin.Records.Last().Contains("expressionPresent=False")&&plugin.Records.Last().Contains("SKIN_AUX")&&
            plugin.Records.Last().Contains("bone=cf_s_arm01_L"),"missing ExpressionBone still records actual skin bones at IK boundaries");
        int recordCount=plugin.Records.Count;
        plugin.Configure(false,noExpressionActor);plugin.ObserveIk("auto_after");
        Check(plugin.Records.Count==recordCount&&HarmonyLib.Harmony.ActivePatches==0,"missing-expression path also stops all diagnostics on OFF");
        noExpressionActor.ChildExpression=expression;plugin.Configure(true,noExpressionActor);
        Check(plugin.Records.Any(s=>s.Contains("expressionSource=component")),"component fallback is reported when actor expression field is null");
        plugin.Configure(false,null,false);
    }
}
