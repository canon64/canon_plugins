using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MainGameAiShoujoMode;
using MapEnvironmentRuntime;

internal static class HotelWalkChecks
{
    internal static void Run(string path)
    {
        var settings = JsonCodec.FromJson<Parameters>(File.ReadAllText(path));
        var controls = JsonCodec.FromJson<HotelRoomControlSettings>(File.ReadAllText(Path.Combine(Path.GetDirectoryName(path), "room-controls.json")));
        controls.Validate();
        if (controls.neonColor.a <= settings.N("math.zero") || controls.neonColor.r <= settings.N("math.zero")) throw new Exception("Neon color lost during managed JSON read");
        if (controls.receptionistPosition.y < settings.N("math.zero") || string.IsNullOrWhiteSpace(controls.receptionistIdle)) throw new Exception("Receptionist configuration lost during JSON read");
        settings.ValidateDocument();
        foreach (var number in settings.numbers)
            if (float.IsNaN(number.value) || float.IsInfinity(number.value) || number.value < number.min || number.value > number.max) throw new Exception("Invalid parameter bounds: " + number.key);
        if (settings.T("movement.mode") != "walk" || settings.B("movement.navigation.enabled")) throw new Exception("Hotel must use physical walking without island navigation");
        if (settings.N("movement.bodyHeight") - settings.N("movement.bodyRadius") <= settings.N("movement.step")) throw new Exception("Invalid hotel controller geometry");
        foreach (string key in new[] { "hotel.interactDistance", "movement.walkSpeed", "movement.sprint" })
            if (settings.N(key) <= settings.N("math.zero")) throw new Exception("Invalid walking parameter: " + key);
        var copy = JsonCodec.FromJson<Parameters>(JsonCodec.ToJson(settings));
        if (copy.V("spawn") != settings.V("spawn")) throw new Exception("Hotel spawn data lost in JSON");
        var floors = JsonCodec.FromJson<HotelMapManifest>(File.ReadAllText(Path.Combine(Path.GetDirectoryName(path), "floors.json")));
        floors.Validate();
        var floorCopy = JsonCodec.FromJson<HotelMapManifest>(JsonCodec.ToJson(floors)); floorCopy.Validate();
        var guestRoom = Array.Find(floorCopy.floors, f => f.id == "room201");
        if (guestRoom?.room == null || guestRoom.room.id != "201" || guestRoom.room.cardSlot != "201"
            || guestRoom.room.rootObject != "HotelRoomProps__201" || guestRoom.room.bedObject != "Mattress")
            throw new Exception("Room 201 cannot automatically start its registered session");
        var corridor = floorCopy.floors.Single(f => f.id == "second");
        foreach (string room in HotelRoomCards.Rooms.Where(id => id != "reception"))
        {
            var guest = floorCopy.floors.Single(f => f.id == "room" + room);
            if (guest.room.id != room || guest.room.cardSlot != room || guest.bundle != guestRoom.bundle
                || guest.scene != guestRoom.scene || guest.room.rootObject != guestRoom.room.rootObject)
                throw new Exception("Guest room model/card isolation failed: " + room);
            if (!corridor.portals.Any(portal => portal.destination == guest.id && portal.colliderObject.Contains("_" + room + "_")))
                throw new Exception("Guest room has no corridor door: " + room);
        }
        var suite=Array.Find(floorCopy.floors,f=>f.id=="reception-suite");
        if(suite?.room==null||suite.room.cardSlot!="reception"||suite.room.id==guestRoom.room.id)
            throw new Exception("Reception suite cannot reuse its receptionist card independently");
        CheckLayoutPersistence(Path.GetDirectoryName(path),settings);
        var missingRoom = JsonCodec.FromJson<HotelMapManifest>(JsonCodec.ToJson(floors));
        Array.Find(missingRoom.floors, f => f.id == "room201").room = null;
        bool missingRoomRejected = false;
        try { missingRoom.Validate(); } catch (InvalidDataException) { missingRoomRejected = true; }
        if (!missingRoomRejected) throw new Exception("Missing Free H room metadata was silently treated as an empty walking map");
        if (floorCopy.initialEntry.position != floors.initialEntry.position || floorCopy.initialEntry.yaw != floors.initialEntry.yaw)
            throw new Exception("Lobby entrance position lost in managed JSON round trip");
        var initialFloor = Array.Find(floors.floors, f => f.id == floors.initialFloor);
        if (floors.initialEntry.position == initialFloor.arrival) throw new Exception("Hotel entry still uses the elevator return position");
        if (floorCopy.floors.Length != floors.floors.Length || floorCopy.initialFloor != floors.initialFloor) throw new Exception("Floor data lost in JSON");
        foreach (var floor in floorCopy.floors)
            if (!File.Exists(Path.Combine(Path.GetDirectoryName(path),floor.bundle))) throw new Exception("Missing hotel bundle: " + floor.bundle);
        var broken = JsonCodec.FromJson<HotelMapManifest>(JsonCodec.ToJson(floors));
        broken.floors[settings.I("math.zero")].destination = broken.initialFloor;
        bool rejected = false;try { broken.Validate(); } catch (InvalidDataException) { rejected = true; }
        if (!rejected) throw new Exception("Self-linked elevator accepted");
        if (typeof(SandboxFreeCamera).GetMethod("Initialize", new[] { typeof(SandboxSession) }) == null || typeof(SandboxFreeCamera).GetMethod("Initialize", new[] { typeof(IWalkingSession) }) == null)
            throw new Exception("Existing island controller entry or hotel interface was removed");
        var type = typeof(AiModeAudio).Assembly.GetType("MainGameAiShoujoMode.HotelWalkingContext", true);
        var context = (IWalkingSession)Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { settings }, null);
        if (context.WalkingReady) throw new Exception("Hotel input active before loading");
        bool notified = false;context.PanelChanged += shown => notified = shown;
        type.GetMethod("SetPanel", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(context, new object[] { true });
        type.GetProperty("WalkingReady").SetValue(context, true, null);
        if (!notified || !context.Panel) throw new Exception("Hotel help fails to notify walking host");
        context.ToggleCameraMode();
        if (settings.T("movement.mode") != "walk") throw new Exception("Hotel enabled a flying camera");
        Console.WriteLine("HOTEL_WALK_CHECKS_PASS managedParameterBounds=true initialReadyFalse=true helpEvent=true existingIslandEntryPreserved=true flightDisabled=true");
    }
    internal static void CheckLayoutPersistence(string parent,Parameters p)
    {
        var directory=Path.Combine(parent,"layout-checks-"+Guid.NewGuid().ToString("N"));
        try
        {
            var store=new HotelObjectLayoutStore(directory,"reception-suite");
            var value=new HotelObjectLayoutData{room="reception-suite",objects=new[]{new HotelObjectTransform{
                id="EDIT__GuestChair",position=p.V("spawn"),rotation=UnityEngine.Vector3.up*p.N("spawn.yaw"),scale=UnityEngine.Vector3.one}}};
            store.Save(value);
            var restored=new HotelObjectLayoutStore(directory,"reception-suite").Load().objects.Single();
            if(restored.position!=value.objects.Single().position||restored.rotation!=value.objects.Single().rotation||restored.scale!=value.objects.Single().scale)
                throw new Exception("Furniture transform lost on restart");
            if(new HotelObjectLayoutStore(directory,"201").Load().objects.Any())throw new Exception("Suite layout leaked into 201");
            value.objects.Single().position=UnityEngine.Vector3.zero;store.Save(value);
            var file=Path.Combine(directory,"reception-suite.json");var before=File.ReadAllText(file);
            value.objects.Single().scale=UnityEngine.Vector3.zero;
            bool rejected=false;try{store.Save(value);}catch(InvalidDataException){rejected=true;}
            if(!rejected||File.ReadAllText(file)!=before)throw new Exception("Invalid scale damaged furniture layout");
            value.objects.Single().scale=UnityEngine.Vector3.one;File.WriteAllText(file,"{broken");
            rejected=false;try{store.Save(value);}catch{rejected=true;}
            if(!rejected||File.ReadAllText(file)!="{broken")throw new Exception("Broken furniture save overwritten");
            Console.WriteLine("HOTEL_FURNITURE_STORE_PASS restart=true rotationScale=true roomIsolation=true invalidPreserved=true corruptPreserved=true");
        }
        finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
}
