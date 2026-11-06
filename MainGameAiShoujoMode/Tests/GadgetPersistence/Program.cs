using System;
using System.IO;
using System.Linq;
using BepInEx.Bootstrap;
using MainGameAiShoujoMode;
using UnityEngine;

public class CaptureClient : BepInEx.BaseUnityPlugin
{
    public bool Fail, FailRestore, Allowed = true;
    public string Current = "current placement";
    public Action BeforeRestore;
    public bool HotelGadgetAllowed => Allowed;
    public string CaptureHotelGadget(Transform room) { if (Fail) throw new IOException("capture failed"); return Current; }
    public void RestoreHotelGadget(Transform room, string json) { BeforeRestore?.Invoke(); if (FailRestore) throw new IOException("restore failed"); Current = json; }
    public void SetHotelGadgetVisible(bool visible) { }
}
public sealed class MissingApiClient : BepInEx.BaseUnityPlugin { }
public sealed class AlignedSyringeClient : CaptureClient
{
    public bool Aligned;
    public bool VerifyHotelGadgetPlacement(Transform room,string saved) => Aligned;
}
public sealed class VerifiedStateClient : CaptureClient
{
    public bool StateApplied;
    public bool VerifyHotelGadgetState(Transform room,string saved) => StateApplied;
}
public sealed class SemanticPlacementClient : BepInEx.BaseUnityPlugin
{
    public string Actual;
    public bool PlacementApplied = true, StateApplied = true;
    public bool HotelGadgetAllowed => true;
    public string CaptureHotelGadget(Transform room) => Actual;
    public void RestoreHotelGadget(Transform room, string saved) { }
    public void SetHotelGadgetVisible(bool shown) { }
    public bool VerifyHotelGadgetPlacement(Transform room, string saved) => PlacementApplied;
    public bool VerifyHotelGadgetState(Transform room, string saved) => StateApplied;
}
internal static class Program
{
    private const string Guid = "com.kks.maingame.iphonecamera";
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Main(string[] args)
    {
        var needle = UnityEngine.JsonUtility.FromJson<MainGameSyringe.HotelNeedleSnapshot>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"needle-fixture.json")));
        float expectedY=needle.localPose.position.y;
        string encoded=needle.Encode();
        Require(encoded.Contains("localPoseRecord") && MainGameSyringe.HotelNeedleSnapshot.Decode(encoded).localPose.position.y==expectedY, "Bone-local pose did not survive string-record roundtrip");
        const string oldNeedle="{\"actor\":0,\"bone\":\"cf_j_thigh00_L\",\"spent\":false,\"surface\":{\"x\":0,\"y\":0,\"z\":0}}";
        Require(MainGameSyringe.HotelNeedleSnapshot.Decode(oldNeedle).localPose==null, "Old omitted local pose must use saved room placement");
        string upgraded=encoded.Replace("\"localPose\":null,", "");
        Require(HotelGadgetComparison.Equivalent(oldNeedle, upgraded, (a,b)=>a==b, true), "Legacy syringe snapshot upgrade was rejected");
        Require(!HotelGadgetComparison.Equivalent(oldNeedle, upgraded, (a,b)=>a==b), "Optional syringe compatibility leaked to other providers");
        Require(!HotelGadgetComparison.Equivalent(oldNeedle, upgraded.Replace("cf_j_thigh00_L", "wrong-bone"), (a,b)=>a==b, true), "Legacy compatibility accepted a different bone");
        Require(!HotelGadgetComparison.Equivalent(encoded, encoded.Replace("cf_j_thigh00_L", "wrong-bone"), (a,b)=>a==b, true), "New attachment mismatch accepted");
        string Attached(string pose, string state) => UnityEngine.JsonUtility.ToJson(new { visible=true, poseRecords=new[]{pose}, runtime=state });
        string attachedBefore=Attached("saved room pose", encoded), attachedAfter=Attached("animated room pose", encoded);
        Require(HotelGadgetComparison.Equivalent(attachedBefore, attachedAfter, (a,b)=>a==b, true), "Bone-following room position incorrectly failed a matching local pose");
        Require(!HotelGadgetComparison.Equivalent(attachedBefore, attachedAfter, (a,b)=>a==b), "Bone comparison leaked to another provider");
        Require(!HotelGadgetComparison.Equivalent(attachedBefore, Attached("animated room pose", encoded.Replace("cf_j_thigh00_L", "wrong-bone")), (a,b)=>a==b, true), "Changed attachment was ignored with room pose");
        var changedNeedle=MainGameSyringe.HotelNeedleSnapshot.Decode(encoded);
        changedNeedle.localPose.position.y=-expectedY;
        Require(!HotelGadgetComparison.Equivalent(attachedBefore, Attached("animated room pose", changedNeedle.Encode()), (a,b)=>a==b, true), "Changed bone-local position was ignored");
        Require(HotelGadgetComparison.Equivalent(attachedBefore, Attached("animated room pose", changedNeedle.Encode()), (a,b)=>a==b, true, true), "Confirmed physical needle alignment still rejected its corrected model position");
        Require(!HotelGadgetComparison.Equivalent(attachedBefore, Attached("animated room pose", changedNeedle.Encode().Replace("cf_j_thigh00_L","wrong-bone")), (a,b)=>a==b, true, true), "Physical alignment bypassed attachment identity");
        Require(!HotelGadgetComparison.Equivalent(Attached("saved room pose",oldNeedle), Attached("animated room pose",oldNeedle), (a,b)=>a==b, true), "Legacy records without local pose lost room placement verification");
        const string hiddenSaved="{\"schema\":\"v1\",\"visible\":false,\"collarAttached\":true,\"poseRecords\":[\"dormant last pose\"],\"runtime\":\"dormant last attachment\",\"settings\":\"same\"}";
        const string hiddenActual="{\"schema\":\"v1\",\"visible\":false,\"collarAttached\":false,\"poseRecords\":[],\"runtime\":null,\"settings\":\"same\"}";
        Require(HotelGadgetComparison.Equivalent(hiddenSaved,hiddenActual,(a,b)=>a==b),"Hidden models incorrectly required dormant placement to be active");
        Require(!HotelGadgetComparison.Equivalent(hiddenSaved,hiddenActual.Replace("same","different"),(a,b)=>a==b),"Hidden model settings mismatch was ignored");
        const string shapeSaved="{\"schema\":\"v1\",\"visible\":true,\"poseRecords\":[\"saved shape\"],\"points\":[],\"runtime\":\"saved control\",\"settings\":\"same\"}";
        const string shapeActual="{\"schema\":\"v1\",\"visible\":true,\"poseRecords\":[\"aligned shape\"],\"points\":[],\"runtime\":\"running control\",\"settings\":\"same\"}";
        Require(!HotelGadgetComparison.Equivalent(shapeSaved,shapeActual,(a,b)=>a==b,false,true),"Geometry verification incorrectly accepted unverified control state");
        Require(HotelGadgetComparison.Equivalent(shapeSaved,shapeActual,(a,b)=>a==b,false,true,true),"Verified geometry and state still required stale raw coordinates");
        Require(!HotelGadgetComparison.Equivalent(shapeSaved,shapeActual.Replace("same","different"),(a,b)=>a==b,false,true,true),"Semantic verification bypassed saved settings");
        Require(HotelGadgetComparison.Equivalent("{\"rotation\":{\"x\":0,\"y\":0,\"z\":0,\"w\":1}}","{\"rotation\":{\"x\":0,\"y\":0,\"z\":0,\"w\":-1}}",(a,b)=>a==b),"Equivalent quaternion signs were rejected");
        using (var hipFixture = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "hip-pose-fixture.json"))))
        {
            string hipSaved = hipFixture.RootElement.GetProperty("saved").GetString();
            string hipActual = hipFixture.RootElement.GetProperty("actual").GetString();
            Require(!HotelGadgetComparison.Equivalent(hipSaved, hipActual, (a,b)=>a==b, false, false, true), "Captured Hip root-placement regression must reproduce the old failure");
            const string hipGuid = "com.kks.main.girlbodyikgizmo";
            var semanticHip = new SemanticPlacementClient { Actual = hipActual };
            Chainloader.PluginInfos[hipGuid] = new PluginInfo { Instance = semanticHip };
            HotelRoomRestoreBatch RestoreHip()
            {
                var result = new HotelRoomRestoreBatch(null, null, "hip-fixture");
                _ = new HotelRoomGadgets(new Transform(), new[] { new HotelRoomGadget { guid = hipGuid, payload = hipSaved } }, _=>{}, restoration: result);
                return result;
            }
            Require(RestoreHip().Succeeded, "Verified Hip effectors were rejected because the map owns a different actor root");
            semanticHip.PlacementApplied = false;
            Require(!RestoreHip().Succeeded, "A real Hip placement mismatch was ignored");
            semanticHip.PlacementApplied = true; semanticHip.StateApplied = false;
            Require(!RestoreHip().Succeeded, "A real Hip control-state mismatch was ignored");
            Chainloader.PluginInfos.Clear();
            Console.WriteLine("CAPTURED_HIP_REGRESSION_PASS original=false semantic=true placementMismatch=rejected stateMismatch=rejected");
        }
        if (args.Any())
        {
            using var fixture=System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"needle-fixture.json")));
            double tolerance=fixture.RootElement.GetProperty("tolerance").GetDouble();
            string savedRuntime=File.ReadAllText(args.First()), actualRuntime=File.ReadAllText(args.Last());
            Require(!HotelGadgetComparison.Equivalent(savedRuntime,actualRuntime,(a,b)=>Math.Abs(a-b)<tolerance),"Captured regression no longer reproduces old room-coordinate failure");
            Require(HotelGadgetComparison.Equivalent(savedRuntime,actualRuntime,(a,b)=>Math.Abs(a-b)<tolerance,true),"Actual captured syringe still fails bone-local verification");
            Console.WriteLine("CAPTURED_SYRINGE_REGRESSION_PASS oldComparison=false attachedComparison=true");
        }
        Require(HotelGadgetComparison.Equivalent("{\"x\":1.0,\"running\":true}", "{\"running\":true,\"x\":1.00000001}", (a,b) => (float)a == (float)b), "Float serialization roundoff or object key order rejected a restored snapshot");
        Require(!HotelGadgetComparison.Equivalent("{\"running\":true}", "{\"running\":false}", (a,b) => a==b), "Different playback state was accepted");
        Require(!HotelGadgetComparison.Equivalent("{\"x\":1}", "{\"x\":2}", (a,b) => a==b), "Different placement was accepted");
        var client = new CaptureClient();
        const string syringeGuid="com.kks.maingame.syringe";
        var syringeClient=new AlignedSyringeClient();
        Chainloader.PluginInfos[syringeGuid]=new PluginInfo { Instance=syringeClient };
        var needleBatch=new HotelRoomRestoreBatch(null,null,"fixture");
        _=new HotelRoomGadgets(new Transform(),new[]{new HotelRoomGadget{guid=syringeGuid,payload=attachedBefore}},_=>{},restoration:needleBatch);
        Require(!needleBatch.Succeeded,"Failed physical placement verification was ignored");
        syringeClient.Aligned=true;
        needleBatch=new HotelRoomRestoreBatch(null,null,"fixture");
        _=new HotelRoomGadgets(new Transform(),new[]{new HotelRoomGadget{guid=syringeGuid,payload=attachedBefore}},_=>{},restoration:needleBatch);
        Require(needleBatch.Succeeded,"Aligned syringe failed provider verification");
        Chainloader.PluginInfos.Clear();
        var stateClient=new VerifiedStateClient();
        Chainloader.PluginInfos[Guid]=new PluginInfo{Instance=stateClient};
        var stateBatch=new HotelRoomRestoreBatch(null,null,"fixture");
        _=new HotelRoomGadgets(new Transform(),new[]{new HotelRoomGadget{guid=Guid,payload="state"}},_=>{},restoration:stateBatch);
        Require(!stateBatch.Succeeded,"Provider state verification failure was ignored");
        stateClient.StateApplied=true;
        stateBatch=new HotelRoomRestoreBatch(null,null,"fixture");
        _=new HotelRoomGadgets(new Transform(),new[]{new HotelRoomGadget{guid=Guid,payload="state"}},_=>{},restoration:stateBatch);
        Require(stateBatch.Succeeded,"Applied provider state failed verification");
        Chainloader.PluginInfos.Clear();
        Chainloader.PluginInfos[Guid] = new PluginInfo { Instance = client };
        var saved = new[] { new HotelRoomGadget { guid = Guid, payload = "last good placement" } };
        bool written = false;
        var manager = new HotelRoomGadgets(new Transform(), saved, data => written = true);
        client.Fail = true;
        Require(!manager.Save() && !written, "Capture failure reported success or replaced persisted state");
        Require(manager.Status.Contains("iPhone") && manager.Status.Contains("capture failed"), "Capture error lost its source");
        client.Fail = false;
        Require(manager.Save() && written, "Recovered capture did not save");
        var failedDisk = new HotelRoomGadgets(new Transform(), saved, data => throw new IOException("disk failed"));
        Require(!failedDisk.Save() && failedDisk.Status.Contains("disk failed"), "Storage failure reported success");
        Chainloader.PluginInfos.Clear();
        HotelRoomGadget[] result = saved;
        var unavailable = new HotelRoomGadgets(new Transform(), saved, data => result = data);
        Require(unavailable.Save() && result.Single().payload == saved.Single().payload, "Unavailable client lost saved placement");
        client.FailRestore = true;
        Chainloader.PluginInfos[Guid] = new PluginInfo { Instance = client };
        written = false;
        var failedRestore = new HotelRoomGadgets(new Transform(), saved, data => written = true);
        Require(!failedRestore.Save() && !written, "Failed restore silently overwrote the saved room");
        client.FailRestore = false;
        Chainloader.PluginInfos.Clear();
        const string extra = "future.item.provider";
        Chainloader.PluginInfos[extra] = new PluginInfo { Instance = client };
        result = null;
        var discovered = new HotelRoomGadgets(new Transform(), Array.Empty<HotelRoomGadget>(), data => result = data);
        Require(discovered.Save() && result.Single().guid == extra, "Provider discovery missed an item outside the old four-entry list");
        var broken = new CaptureClient();
        Chainloader.PluginInfos[Guid] = new PluginInfo { Instance = broken };
        var partial = new HotelRoomGadgets(new Transform(), saved, data => { });
        broken.Fail = true;
        var logs = new System.Collections.Generic.List<string>();
        var writes = new System.Collections.Generic.List<string>();
        var batch = new HotelRoomSaveBatch(() => true, logs.Add, "201.json", "test");
        partial.Save(batch, data => writes.Add(data.guid)); batch.Finish();
        Require(!batch.Succeeded && writes.Contains(extra) && !writes.Contains(Guid), "A failed provider prevented later provider persistence");
        Require(logs.Any(x => x.Contains("capture failed") && x.Contains("stage=capture")) && logs.Any(x => x.Contains("provider=" + extra)), "Provider failure/success diagnostics missing");
        Chainloader.PluginInfos[Guid] = new PluginInfo { Instance = new MissingApiClient() };
        bool notice = false;
        var missing = new HotelRoomGadgets(new Transform(), saved, data => { },
            () => new HotelRoomSaveBatch(() => true, logs.Add, "201.json", "gadgets-button"),
            resultBatch => notice = resultBatch.HasNotice && resultBatch.FailureNotice.Contains("保存API"));
        Require(!missing.Save() && notice && logs.Any(x => x.Contains("trigger=gadgets-button") && x.Contains("item=iPhone") && x.Contains("result=failure")), "Installed provider missing API lost notification or standalone diagnostics");
        Chainloader.PluginInfos.Clear();
        bool composerReady = false, futureReady = false;
        var dependentHip = new CaptureClient { BeforeRestore = () => Require(composerReady && futureReady, "HipHijack restored before its follow targets") };
        Chainloader.PluginInfos["com.kks.main.girlbodyikgizmo"] = new PluginInfo { Instance = dependentHip };
        Chainloader.PluginInfos["com.kks.main.gameobjectcomposer"] = new PluginInfo { Instance = new CaptureClient { BeforeRestore = () => composerReady = true } };
        Chainloader.PluginInfos[extra] = new PluginInfo { Instance = new CaptureClient { BeforeRestore = () => futureReady = true } };
        new HotelRoomGadgets(new Transform(), new[] {
            new HotelRoomGadget { guid = "com.kks.main.girlbodyikgizmo", payload = "room-local hip state" },
            new HotelRoomGadget { guid = "com.kks.main.gameobjectcomposer", payload = "composer targets" },
            new HotelRoomGadget { guid = extra, payload = "future targets" }
        }, _ => { }, restoration: new HotelRoomRestoreBatch(() => true, logs.Add, "201.json"));
        Require(dependentHip.Current == "room-local hip state", "Dependent HipHijack restore did not finish after all providers");
        Chainloader.PluginInfos.Clear();
        var restoreGood = new CaptureClient(); var restoreBad = new CaptureClient { FailRestore = true };
        Chainloader.PluginInfos[Guid] = new PluginInfo { Instance = restoreBad };
        Chainloader.PluginInfos[extra] = new PluginInfo { Instance = restoreGood };
        var restoration = new HotelRoomRestoreBatch(() => true, logs.Add, "201.json");
        var restoredManager = new HotelRoomGadgets(new Transform(), new[] { new HotelRoomGadget { guid = Guid, payload = "failed restore" }, new HotelRoomGadget { guid = extra, payload = "successful restore" } }, _ => { }, restoration: restoration);
        Require(!restoration.Finish() && restoration.HasNotice && restoreGood.Current == "successful restore" && restoration.Status.Contains("restore failed"), "Provider restore failure stopped later item or lost notification");
        Console.WriteLine("HOTEL_GADGET_PERSISTENCE_PASS captureFailure=true storageFailure=true recovery=true unavailablePreserved=true");
    }
}
