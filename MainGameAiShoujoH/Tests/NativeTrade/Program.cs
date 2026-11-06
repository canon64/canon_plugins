using System;
using System.IO;
using System.Linq;
using MainGameAiShoujoMode.Life;
using Store = MainGameAiShoujoMode.Life.AiLifeStateStore;
namespace MainGameAiShoujoMode.Life { internal sealed class AiLifeActivity { internal void Validate() { } } }
class Program
{
 static void Check(bool value,string text) { if(!value) throw new Exception(text); }
 static void Main(string[] args) {
  var path=Path.Combine(args[0],Guid.NewGuid().ToString("N"),"world.json");var store=new Store(path,1,99,()=>DateTime.UtcNow);
  store.EnsureInventory("player","world",2);var state=store.EnsureInventory("merchant","world",null);
  Check(state.nativeMerchantSpendTotal==0,"legacy/new native trade total has no invented credit");
  Store.Request Seed(string destination,int item,int count)=>new Store.Request { id=Guid.NewGuid().ToString("N"),action="acquire",destination=destination,category=1,item=item,count=count };
  state=store.Commit(state,Seed("player:world",0,10),out _,out _);var nativeStock=Seed("merchant:world",1,3);
  var trade=new Store.Request { id="trade-fixed",action="merchant-trade",sources=new[]{"player:world"},destination="merchant:world",category=1,item=0,count=2,
   merchantReturnCategory=1,merchantReturnItem=1,merchantReturnCount=1,nativeMerchantSpendDelta=600,nativeMerchantSpendMaximum=99999999 };
  trade.merchantReturnRate=trade.nativeMerchantSpendDelta;
  state=store.EnsureNativeVendor("merchant:world","day:area",new[]{new Store.NativeVendorItem { category=nativeStock.category,item=nativeStock.item,count=nativeStock.count,stock=nativeStock.count,rate=trade.merchantReturnRate }});
  var before=state;state=store.Commit(state,trade,out _,out bool applied);
  Check(applied&&state.nativeMerchantSpendTotal==600&&state.inventories.Single(i=>i.kind=="player").items.Single(i=>i.ID==0).Count==8,"actual exchange and cumulative total publish together");
  state=store.Commit(before,trade,out _,out applied);Check(!applied&&state.nativeMerchantSpendTotal==600,"repeated transaction never adds cumulative credit twice");
  var bytes=File.ReadAllBytes(path);trade.id="short-stock";trade.merchantReturnCount=99;
  try {store.Commit(state,trade,out _,out _);throw new Exception("missing merchant stock accepted");}catch(InvalidOperationException){}
  Check(bytes.SequenceEqual(File.ReadAllBytes(path)),"shortage leaves both inventories and native total unchanged");
  trade.id="short-player";trade.merchantReturnCount=1;trade.count=99;
  try {store.Commit(state,trade,out _,out _);throw new Exception("missing player stock accepted");}catch(InvalidOperationException){}
  Check(bytes.SequenceEqual(File.ReadAllBytes(path)),"player shortage does not persist partial stock or total");
  trade.id="wrong-rate";trade.count=trade.merchantReturnCount;trade.merchantReturnRate+=Math.Sign(trade.count);
  try {store.Commit(state,trade,out _,out _);throw new Exception("wrong native price accepted");}catch(InvalidOperationException){}
  Check(bytes.SequenceEqual(File.ReadAllBytes(path)),"native vendor quote is checked inside atomic commit");
  var repeated=store.EnsureNativeVendor("merchant:world","day:area",Array.Empty<Store.NativeVendorItem>());
  Check(bytes.SequenceEqual(File.ReadAllBytes(path))&&repeated.nativeMerchantVendor.Single().count==nativeStock.count-trade.merchantReturnCount,"opening same day never replenishes spent stock");
  state=store.EnsureNativeVendor("merchant:world","next-day:area",new[]{new Store.NativeVendorItem {category=nativeStock.category,item=nativeStock.item,rate=trade.nativeMerchantSpendDelta,stock=nativeStock.count,count=nativeStock.count}});
  Check(state.nativeMerchantVendor.Single().count==nativeStock.count&&state.inventories.Single(i=>i.kind=="merchant").items.Where(i=>i.ID==trade.item).Sum(i=>i.Count)==before.inventories.Single(i=>i.kind=="player").items.Single().Count-state.inventories.Single(i=>i.kind=="player").items.Single(i=>i.ID==trade.item).Count,"new day replaces unsold stock while retaining received player goods");
  var catalog=NativeMerchantCatalog.Load(Path.Combine(AppContext.BaseDirectory,"native-merchant-catalog.json"));
  foreach(var table in catalog.tables.Where(t=>t.kind=="normal")) {
   var low=catalog.NormalStock(int.Parse(table.area),(min,max)=>min);var high=catalog.NormalStock(int.Parse(table.area),(min,max)=>max-Math.Sign(max));
   Check(low.Length==table.rows.Length&&low.Zip(table.rows,(v,r)=>v.category==r.category&&v.item==r.item&&v.rate==r.Rate&&v.count==r.Stocks.First()).All(v=>v),"every native category/item/rate and low stock bound retained");
   Check(high.Zip(table.rows,(v,r)=>v.count==(r.Stocks.Length==Math.Sign(r.Stocks.Length)?r.Stocks.First():r.Stocks.Last()-Math.Sign(r.Stocks.Length))).All(v=>v),"native stock upper range is exclusive across all rows");
  }
  Console.WriteLine("DEDICATED_H_NATIVE_TRADE_PASS 14");
 }
}
