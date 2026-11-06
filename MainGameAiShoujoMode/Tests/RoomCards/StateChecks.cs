using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using MainGameAiShoujoMode;

internal static class StateChecks
{
    private static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
    internal static void Run(string directory)
    {
        var legacyAppearance = new HotelRoomAppearance();
        Require(!legacyAppearance.CanRestoreCard("201"), "Legacy 201 snapshot can overwrite registered MOD card");
        Require(legacyAppearance.CanRestoreCard("202"), "201 migration changed another room");
        legacyAppearance.format = "resolved-v2";
        Require(legacyAppearance.CanRestoreCard("201"), "Verified snapshot cannot restore saved outfit");
        HotelRoomState input;
        using (var stream = File.OpenRead(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "state-fixture.json")))
            input = (HotelRoomState)new DataContractJsonSerializer(typeof(HotelRoomState)).ReadObject(stream);
        var store = new HotelRoomStateStore(directory, "201");
        Require(store.Load().selection == null && store.Load().placement == null, "initial state");
        Require(!File.Exists(Path.Combine(directory, "201.json")), "empty load wrote state");
        store.SaveSelection(input.selection); store.SavePlacement(input.placement);
        var after = new HotelRoomStateStore(directory, "201").Load();
        Require(input.selection.Matches(after.selection) && after.placement.x == input.placement.x && after.placement.y == input.placement.y
            && after.placement.z == input.placement.z && after.placement.yaw == input.placement.yaw, "pose/position/direction restart round trip");
        var changed = new HotelRoomSelection { mode = input.selection.mode, id = input.selection.id, name = "動作B", bundle = input.selection.bundle, asset = input.selection.asset };
        Require(!changed.Matches(input.selection), "clone ID collision");
        changed.name = input.selection.name; changed.bundle = "other";
        Require(!changed.Matches(input.selection), "resource identity collision");
        store.SaveSelection(changed);
        Require(store.Load().placement.yaw == input.placement.yaw, "pose save overwrote placement");
        store.SavePlacement(input.placement.Copy());
        Require(store.Load().selection.Matches(changed), "placement save overwrote pose");
        store.SaveGadgets(input.gadgets); store.SaveLighting(input.lighting); store.SaveAppearance(input.appearance);
        store.SaveSelection(changed); store.SavePlacement(input.placement);
        var full = store.Load();
        Require(full.lighting.sources.Single().key == input.lighting.sources.Single().key
            && full.lighting.sources.Single().g == input.lighting.sources.Single().g
            && full.lighting.sources.Single().strength == input.lighting.sources.Single().strength, "Individual light color or intensity lost on restart");
        var lightingDraft = full.lighting.Copy(); lightingDraft.sources.Single().r = default(float);
        Require(full.lighting.sources.Single().r == input.lighting.sources.Single().r, "Light preview mutated the saved snapshot");
        Require(full.gadgets.Single().payload == input.gadgets.Single().payload && full.lighting.emission == input.lighting.emission
            && full.appearance.female == input.appearance.female && full.appearance.preferences.Single().value == input.appearance.preferences.Single().value,
            "other writes lost gadgets, lighting or complete character data");
        Require(new HotelRoomStateStore(directory, "202").Load().selection == null, "room isolation");
        var reception = new HotelRoomStateStore(directory, "reception");
        reception.SaveReceptionistPose("Base Layer.StandD.Stand_00_00");
        Require(new HotelRoomStateStore(directory, "reception").Load().receptionistPose == "Base Layer.StandD.Stand_00_00", "Receptionist pose did not persist");
        reception.SaveReceptionistPlacement(input.receptionistPlacement);
        var reloadedReception = new HotelRoomStateStore(directory, "reception").Load();
        Require(reloadedReception.receptionistPlacement.x == input.receptionistPlacement.x
            && reloadedReception.receptionistPlacement.y == input.receptionistPlacement.y
            && reloadedReception.receptionistPlacement.z == input.receptionistPlacement.z
            && reloadedReception.receptionistPlacement.rx == input.receptionistPlacement.rx
            && reloadedReception.receptionistPlacement.ry == input.receptionistPlacement.ry
            && reloadedReception.receptionistPlacement.rz == input.receptionistPlacement.rz
            && reloadedReception.receptionistPose == "Base Layer.StandD.Stand_00_00", "Receptionist XYZ/rotation XYZ lost or clobbered pose");
        string receptionPath = Path.Combine(directory, "reception.json"), receptionBefore = File.ReadAllText(receptionPath);
        var badReception = input.receptionistPlacement.Copy(); badReception.rz = float.NaN;
        bool badReceptionRejected = false;
        try { reception.SaveReceptionistPlacement(badReception); } catch (InvalidDataException) { badReceptionRejected = true; }
        Require(badReceptionRejected && File.ReadAllText(receptionPath) == receptionBefore, "Invalid receptionist rotation replaced saved data");
        Require(store.Load().receptionistPose == null, "Receptionist pose leaked into guest room");
        var suite=new HotelRoomStateStore(directory,"reception-suite");
        suite.SaveSelection(changed);suite.SavePlacement(input.placement.Copy());suite.SaveLighting(input.lighting.Copy());
        Require(new HotelRoomStateStore(directory,"reception-suite").Load().selection.Matches(changed)
            && store.Load().appearance.female==input.appearance.female
            && new HotelRoomStateStore(directory,"reception").Load().receptionistPose==reloadedReception.receptionistPose,
            "Reception suite clobbered the receptionist or room 201 state");
        string path = Path.Combine(directory, "201.json"), before = File.ReadAllText(path);
        var invalid = input.placement.Copy(); invalid.x = float.NaN;
        bool rejected = false; try { store.SavePlacement(invalid); } catch (InvalidDataException) { rejected = true; }
        Require(rejected && File.ReadAllText(path) == before, "NaN clobbered state");
        invalid.x = float.PositiveInfinity; rejected = false;
        try { store.SavePlacement(invalid); } catch (InvalidDataException) { rejected = true; }
        Require(rejected && File.ReadAllText(path) == before, "infinite position clobbered state");
        File.WriteAllText(path, "{broken"); rejected = false;
        try { store.SaveSelection(changed); } catch { rejected = true; }
        Require(rejected && File.ReadAllText(path) == "{broken", "corrupt state clobbered");
        Console.WriteLine("HOTEL_ROOM_STATE_PASS restart=true independentBlocks=true roomIsolation=true cloneIdentity=true finiteOnly=true corruptPreserved=true gadgetsLightingAppearance=true");
    }
}
