using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using MainGameAiShoujoMode.Furniture;

internal static class Program
{
    private static int _checks;
    private static void Check(bool condition,string label){if(!condition)throw new Exception(label);_checks++;}
    private static void Rejected(Action action,string label){try{action();}catch(Exception){_checks++;return;}throw new Exception(label);}
    private static void Main(string[] args)
    {
        var root=Path.GetFullPath(args.Single());Directory.CreateDirectory(root);
        var file=Path.Combine(root,"layout.json");var store=new FurnitureStore(file);
        Check(!store.Read().items.Any(),"first start empty");
        using var fixture=JsonDocument.Parse(File.ReadAllText(@"F:\kks\work\tools\KksMapSandbox\parameters.json"));
        float N(string key)=>fixture.RootElement.GetProperty("numbers").EnumerateArray().Single(v=>v.GetProperty("key").GetString()==key).GetProperty("value").GetSingle();
        var one=N("math.one");var two=N("math.two");
        var catalog=FurnitureCatalog.Load(Path.Combine(Environment.CurrentDirectory,"MainGameAiShoujoMode/Package/Content/Furniture/catalog.json"));
        Check(catalog.items.Any(i=>i.Available)&&catalog.categories.Any(c=>!string.IsNullOrWhiteSpace(c.name)),"production catalog and native category names deserialize");
        Check(catalog.items.Where(i=>!i.Available).All(i=>!string.IsNullOrWhiteSpace(i.error)),"unavailable furniture always explains failure");
        var tileWidth=N("furniture.tileWidth");var tileHeight=N("furniture.tileHeight");
        var columns=(int)(one+two);var rows=(int)two;
        var grid=FurnitureGrid.Fit(tileWidth*columns,tileHeight*rows,tileWidth,tileHeight,(int)one);
        Check(grid.Columns==columns&&grid.Rows==rows&&grid.PageSize==columns*rows,"catalog occupies both axes of the available space");
        var bigger=FurnitureGrid.Fit(tileWidth*columns*two,tileHeight*rows*two,tileWidth,tileHeight,(int)one);
        Check(bigger.Columns==grid.Columns*two&&bigger.Rows==grid.Rows*two,"more window space increases visible furniture instead of leaving a narrow list");
        var seen=Enumerable.Range(default(int),(catalog.items.Length+grid.PageSize-(int)one)/grid.PageSize).SelectMany(page=>catalog.items.Skip(page*grid.PageSize).Take(grid.PageSize)).Select(i=>i.id).ToArray();
        Check(seen.SequenceEqual(catalog.items.Select(i=>i.id)),"paging retains catalog order and every item exactly once");
        Check(catalog.items.All(i=>i.thumbnail=="Thumbnails/"+i.id+".png"&&File.Exists(Path.Combine(Environment.CurrentDirectory,"MainGameAiShoujoMode/Package/Content/Furniture",i.thumbnail))),"every catalog item has its own extracted thumbnail");
        var item=new FurnitureInstance{id=Guid.NewGuid().ToString("N"),item=default(int),
            position=new FurnitureVector{x=one,y=two,z=one+two},rotation=new FurnitureVector{y=N("math.degreesHalfTurn")*N("math.half")}};
        var layout=new FurnitureLayout{items=new[]{item}};store.Write(layout);var loaded=store.Read();
        Check(loaded.items.Single().id==item.id&&loaded.items.Single().rotation.y==item.rotation.y,"stable ID and pose persist");
        var before=File.ReadAllBytes(file);var draft=FurnitureJson.Copy(loaded);draft.items.Single().position.x+=one;
        Check(File.ReadAllBytes(file).SequenceEqual(before),"preview/cancel does not write store");
        var invalid=FurnitureJson.Copy(loaded);invalid.items.Single().position.y=float.NaN;
        Rejected(()=>store.Write(invalid),"nonfinite accepted");Check(File.ReadAllBytes(file).SequenceEqual(before),"invalid transform preserved file");
        invalid=FurnitureJson.Copy(loaded);invalid.items=new[]{invalid.items.Single(),invalid.items.Single()};
        Rejected(()=>store.Write(invalid),"duplicate identity accepted");
        invalid=FurnitureJson.Copy(loaded);invalid.world="other-island";Rejected(()=>store.Write(invalid),"wrong world accepted");
        File.WriteAllText(file,"{broken");var corrupt=File.ReadAllBytes(file);Rejected(()=>store.Write(layout),"corrupt save overwritten");
        Check(File.ReadAllBytes(file).SequenceEqual(corrupt),"corrupt bytes retained");
        var locks=new FurnitureReservations();var first=locks.Acquire(item.id,"slot","request-one");
        Check(locks.Busy(item.id),"reservation visible during travel");Rejected(()=>locks.Acquire(item.id,"another-slot","request-two"),"occupied bed reassigned");
        first.Dispose();first.Dispose();Check(!locks.Busy(item.id),"cancel and duplicate release safe");
        var second=locks.Acquire(item.id,"slot","request-two");first.Dispose();Check(locks.Busy(item.id),"old release cannot clear new owner");
        second.Dispose();Check(!locks.Busy(item.id),"new owner completed");
        Console.WriteLine("AI_FURNITURE_DATA_PASS checks="+_checks);
    }
}
