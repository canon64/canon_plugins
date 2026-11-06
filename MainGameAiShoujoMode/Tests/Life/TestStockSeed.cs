using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text.Json;
using MainGameAiShoujoMode.Life;

internal static class TestStockSeed
{
    internal static void Run(string content,int quantity)
    {
        Console.WriteLine("AI_TEST_STOCK_SEED_START");
        if (quantity <= default(int)) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (Process.GetProcessesByName("KoikatsuSunshine").Any() || Process.GetProcessesByName("KoikatsuSunshine_VR").Any())
            throw new InvalidOperationException("KKS is running; live save was not changed.");
        var settings=AiLifeRuntimeSettings.LoadContent(content);var livePath=settings.savePath;
        if (!File.Exists(livePath)) throw new FileNotFoundException("Existing AI-mode save is missing",livePath);
        var run=Guid.NewGuid().ToString("N");
        var folder=Path.Combine("F:/kks/work/plugins/canon_plugins/analysis/ai-conversation-system/test-stock-seed",run);Directory.CreateDirectory(folder);
        var staged=Path.Combine(folder,"life-state.json");var backup=Path.Combine(folder,"life-state.before.json");
        int supplied=default;int capacity=default;
        using (var guard=new FileStream(livePath+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))
        {
            var original=File.ReadAllBytes(livePath);File.WriteAllBytes(backup,original);
            var codec=new DataContractJsonSerializer(typeof(AiLifeStateStore.Snapshot));
            AiLifeStateStore.Snapshot world;
            using(var input=new MemoryStream(original)) world=(AiLifeStateStore.Snapshot)codec.ReadObject(input);
            var catalog=AiItemCatalog.Load(Path.Combine(content,settings.itemCatalogFile),settings.sourceProfileVersion);
            var definitions=catalog.items.Where(v=>!v.system).ToArray();
            var player=world.inventories.Single(v=>v.kind=="player"&&v.owner==settings.worldKey);
            var required=player.items.Count+definitions.Count(d=>!player.items.Any(i=>i.CategoryID==d.category&&i.ID==d.id));
            player.capacity=Math.Max(player.capacity??required,required);capacity=player.capacity.Value;world.revision=Guid.NewGuid().ToString("N");
            using(var output=File.Create(staged)) codec.WriteObject(output,world);
            settings.savePath=staged;
            var runtime=new AiLifeRuntime(settings,()=>DateTime.Now,null);runtime.LoadItems(content);
            foreach(var definition in definitions)
            {
                var current=runtime.Store.Read();var inventory=current.inventories.Single(v=>v.Key==runtime.PlayerInventoryKey);
                var count=inventory.items.Where(v=>v.CategoryID==definition.category&&v.ID==definition.id).Sum(v=>v.Count);
                if(count>=quantity)continue;
                runtime.Store.Commit(current,new AiLifeStateStore.Request { id="test-stock:"+run+":"+definition.category+":"+definition.id,
                    action="acquire",destination=runtime.PlayerInventoryKey,category=definition.category,item=definition.id,count=quantity-count },out _,out _);
                supplied++;
            }
            var final=runtime.Store.Read();var stock=final.inventories.Single(v=>v.Key==runtime.PlayerInventoryKey);
            if(definitions.Any(d=>stock.items.Where(v=>v.CategoryID==d.category&&v.ID==d.id).Sum(v=>v.Count)<quantity)) throw new Exception("Stock validation failed; live save retained.");
            if(!File.ReadAllBytes(livePath).SequenceEqual(original)) throw new Exception("Live save changed while preparing stock.");
            File.Replace(staged,livePath,null);
        }
        settings.savePath=livePath;
        var reopened=new AiLifeRuntime(settings,()=>DateTime.Now,null);reopened.LoadItems(content);
        var saved=reopened.Store.Read().inventories.Single(v=>v.Key==reopened.PlayerInventoryKey);
        var rows=saved.items.Select(v=>new { category=v.CategoryID,item=v.ID,count=v.Count,definition=reopened.Items.Find(v.CategoryID,v.ID) }).ToArray();
        if(rows.Any(v=>v.definition==null||v.count<quantity))throw new Exception("Published inventory validation failed.");
        var result=new { savePath=livePath,backup,normalItems=rows.Length,supplied,quantity,capacity,
            foodKinds=rows.Count(v=>v.definition.food),drinkKinds=rows.Count(v=>v.definition.drink),
            items=rows.Select(v=>new { v.category,v.item,name=v.definition.name,v.count }).ToArray(),gameVerified=false };
        File.WriteAllText(Path.Combine(folder,"receipt.json"),JsonSerializer.Serialize(result,new JsonSerializerOptions { WriteIndented=true,Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        Console.WriteLine("AI_TEST_STOCK_SEED_PASS kinds="+rows.Length+" each="+quantity+" capacity="+capacity+" foodKinds="+rows.Count(v=>v.definition.food)+" drinkKinds="+rows.Count(v=>v.definition.drink)+" backup="+backup);
    }
}
