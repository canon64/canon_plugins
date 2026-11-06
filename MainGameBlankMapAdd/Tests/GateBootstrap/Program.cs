using System;
using System.Reflection;
using BepInEx.Bootstrap;
using CanonGate;
using CanonPlugins.Shared;
using MainGameBlankMapAdd;
using KKAPI.MainGame;
internal static class Program
{
    private static void Check(bool ok,string message) { if(!ok) throw new Exception(message); Console.WriteLine("PASS "+message); }
    private static void Main()
    {
        var provider=new MainGameBlankMapAdd.Plugin();
        int starts=0,stops=0;
        const string guid="com.kks.maingameblankmapadd";
        PluginSessionGate.Bind(provider,guid,()=>++starts,()=>++stops);
        Chainloader.PluginInfos.Add(guid,new BepInEx.PluginInfo { Instance=provider });
        Check(provider.enabled&&provider.isActiveAndEnabled&&!PluginSessionGate.Allowed,"room provider bootstrap stays enabled before gate grant");
        var gate=new CanonGate.Plugin();
        typeof(CanonGate.Plugin).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(gate,null);
        BlankRoomState.Inside=true;
        GameAPI.Begin();
        Check(BlankRoomState.Reads==1&&starts==1&&PluginSessionGate.Allowed,"first AllPoseRoom H reads once and starts provider runtime");
        for(int i=0;i<100000;i++) { _=PluginSessionGate.Allowed; GameAPI.Begin(); }
        Check(BlankRoomState.Reads==1&&starts==1,"100000 queries and duplicate starts do not inspect or restart");
        GameAPI.End(); GameAPI.End();
        Check(stops==1&&provider.isActiveAndEnabled&&!PluginSessionGate.Allowed,"end stops runtime once but preserves next-H room bootstrap");
        GameAPI.Begin();
        Check(BlankRoomState.Reads==2&&starts==2&&PluginSessionGate.Allowed,"second AllPoseRoom H starts without a disabled-provider deadlock");
        GameAPI.End();
        BlankRoomState.Inside=false; GameAPI.Begin();
        Check(BlankRoomState.Reads==3&&starts==2&&!PluginSessionGate.Allowed,"ordinary H remains denied");
        GameAPI.End();
        provider.enabled=false; BlankRoomState.Inside=true; GameAPI.Begin();
        Check(BlankRoomState.Reads==3&&starts==2&&!PluginSessionGate.Allowed,"manually disabled provider still fails closed");
        GameAPI.End(); provider.enabled=true;
        PluginSessionGate.Dispose();
        typeof(CanonGate.Plugin).GetMethod("OnDestroy",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(gate,null);
        Console.WriteLine("PASS real gate entry plus real BlankMapAdd adapter; Unity boundary models enabled/isActiveAndEnabled coupling.");
    }
}
