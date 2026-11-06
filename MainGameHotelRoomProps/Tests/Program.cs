using System;
using System.IO;
using MainGameHotelRoomProps;
internal static class Program
{
    private static int _checks;
    private static void Check(bool ok,string name){if(!ok)throw new Exception(name);_checks++;}
    private static void Reject(Action call,string name){try{call();}catch{_checks++;return;}throw new Exception("accepted "+name);}
    private static void Main(string[] args)
    {
        string directory=args[0];Directory.CreateDirectory(directory);
        var store=new LayoutStore(directory,"test201");if(File.Exists(store.Path))File.Delete(store.Path);
        Check(store.Load()==null,"first visit has no override");
        var pose=new PropPose{id="teddy-left",position=new[]{.2f,.8f,1.1f},rotation=new[]{0f,0f,0f,1f}};
        var data=new RoomLayout{roomId="test201",props=new[]{pose}};store.Save(data);
        var loaded=store.Load();Check(loaded.roomId=="test201" && loaded.props[0].id==pose.id,"stable identity round trip");
        Check(loaded.props[0].position[2]==1.1f && loaded.props[0].rotation[3]==1f,"full position and rotation retained");
        pose.position[0]=.7f;store.Save(data);Check(File.Exists(store.Path+".bak"),"previous layout kept as backup");
        Check(store.Load().props[0].position[0]==.7f,"new release replaces layout");
        Check(!File.Exists(store.Path+".tmp"),"no partial write remains");
        Reject(()=>new LayoutStore(directory,"../201"),"path traversal");
        data.roomId="202";Reject(()=>store.Save(data),"wrong room");data.roomId="test201";
        data.props=new[]{pose,pose};Reject(()=>store.Save(data),"duplicate IDs");data.props=new[]{pose};
        pose.position[0]=float.NaN;Reject(()=>store.Save(data),"nonfinite position");pose.position[0]=.7f;
        pose.rotation=new float[4];Reject(()=>store.Save(data),"zero rotation");pose.rotation=new[]{0f,0f,0f,1f};
        Check(store.Load().props[0].position[0]==.7f,"rejected save cannot overwrite good layout");
        File.WriteAllText(store.Path,"broken");Reject(()=>store.Load(),"damaged JSON");Check(File.ReadAllText(store.Path)=="broken","damaged original preserved");
        Console.WriteLine("ROOM_LAYOUT_TESTS_PASS checks="+_checks);
    }
}
