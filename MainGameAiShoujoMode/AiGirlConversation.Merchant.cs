using System;
using System.Collections.Generic;
using System.Linq;
using MainGameAiShoujoMode.Conversation;
using MainGameAiShoujoMode.Life;

namespace MainGameAiShoujoMode
{
    public sealed partial class AiGirlConversation
    {
        internal Func<bool> CanTradeNativeMerchant;
        internal Action<int> OpenNativeMerchant;
        internal Func<string,int,int,int,int,int,int,bool> TradeNativeMerchant;
        private AiItemCatalog.Item _merchantGive;
        private AiLifeStateStore.NativeVendorItem _merchantTake;
        private int _merchantGiveCount, _merchantTakeCount;
        private void ShowNativeMerchant()
        {
            if (_disposed || !Active) return;
            try {
                if (CanTradeNativeMerchant?.Invoke() != true) throw new InvalidOperationException("この住人とは取引できません。");
                OpenNativeMerchant(Life.Store.Read().nativeMerchantArea);
                if (_merchantGiveCount <= default(int)) _merchantGiveCount = _catalog.Runtime.one;
                if (_merchantTakeCount <= default(int)) _merchantTakeCount = _catalog.Runtime.one;
                var snapshot = Life.Store.Read();
                int availableGive = _merchantGive == null ? default(int) : snapshot.inventories.Single(i => i.Key == Life.PlayerInventoryKey).items.Where(i => i.CategoryID == _merchantGive.category && i.ID == _merchantGive.id).Sum(i => i.Count);
                int availableTake = _merchantTake == null ? default(int) : snapshot.nativeMerchantVendor.Single(i => i.category == _merchantTake.category && i.item == _merchantTake.item).count;
                var menu = new List<KksConversationView.Choice> {
                    new KksConversationView.Choice("渡す品物: " + (_merchantGive?.name ?? "未選択"), () => ChooseMerchantItem(true, default(int))),
                    new KksConversationView.Choice("受け取る品物: " + (_merchantTake == null ? "未選択" : Life.Items.Find(_merchantTake.category,_merchantTake.item).name), () => ChooseMerchantItem(false, default(int))),
                    new KksConversationView.Choice("渡す個数 −", () => { _merchantGiveCount -= _catalog.Runtime.one; ShowNativeMerchant(); }, _merchantGiveCount > _catalog.Runtime.one),
                    new KksConversationView.Choice("渡す個数 ＋", () => { _merchantGiveCount = checked(_merchantGiveCount + _catalog.Runtime.one); ShowNativeMerchant(); }, _merchantGiveCount < availableGive),
                    new KksConversationView.Choice("受取個数 −", () => { _merchantTakeCount -= _catalog.Runtime.one; ShowNativeMerchant(); }, _merchantTakeCount > _catalog.Runtime.one),
                    new KksConversationView.Choice("受取個数 ＋", () => { _merchantTakeCount = checked(_merchantTakeCount + _catalog.Runtime.one); ShowNativeMerchant(); }, _merchantTakeCount < availableTake)
                };
                string offer = _merchantGive == null ? "未選択" : _merchantGive.name + " ×" + _merchantGiveCount;
                string take = _merchantTake == null ? "未選択" : Life.Items.Find(_merchantTake.category,_merchantTake.item).name + " ×" + _merchantTakeCount;
                Present("渡す: " + offer + "\n受取: " + take + "\n交換実績: " + Life.Store.Read().nativeMerchantSpendTotal);
                var owner = Resident; var operation = Guid.NewGuid().ToString("N");
                menu.Add(new KksConversationView.Choice("この内容で交換する", () => {
                    if (_disposed || !Active || !ReferenceEquals(owner,Resident)) return;
                    try {
                        TradeNativeMerchant(operation,_merchantGive.category,_merchantGive.id,_merchantGiveCount,_merchantTake.category,_merchantTake.item,_merchantTakeCount);
                        ShowNativeMerchant(); Present("交換し、在庫と実績を保存しました。\n渡した品物: " + offer + "\n受け取った品物: " + take);
                    } catch (Exception error) { Present(error.GetBaseException().Message); }
                }, _merchantGive != null && _merchantTake != null));
                menu.Add(new KksConversationView.Choice("会話へ戻る",ShowMenu)); _view.SetChoices(menu);
            } catch(Exception error) { Present(error.GetBaseException().Message); _view.SetChoices(new[]{new KksConversationView.Choice("会話へ戻る",ShowMenu)}); }
        }
        private void ChooseMerchantItem(bool giving,int page)
        {
            if (_disposed || !Active) return;
            var state=Life.Store.Read(); var choices=new List<KksConversationView.Choice>();
            var items = giving ? state.inventories.Single(i=>i.Key==Life.PlayerInventoryKey).items
                .GroupBy(i=>new { i.CategoryID,i.ID }).Select(g=>new AiLifeStateStore.NativeVendorItem { category=g.Key.CategoryID,item=g.Key.ID,count=g.Sum(i=>i.Count),rate=Life.Items.Find(g.Key.CategoryID,g.Key.ID)?.rate ?? default(int) }).Where(i=>i.rate>default(int)).ToArray()
                : (state.nativeMerchantVendor ?? new List<AiLifeStateStore.NativeVendorItem>()).Where(i=>i.count>default(int)).ToArray();
            int size=Life.ConnectionCatalog.outfitPageSize;
            foreach(var row in items.Skip(page*size).Take(size)) {
                var item=Life.Items.Find(row.category,row.item);
                choices.Add(new KksConversationView.Choice(item.name+" / 所持"+row.count+" / 交換値"+row.rate,()=>{
                    if(giving) _merchantGive=item; else _merchantTake=row; ShowNativeMerchant();
                }));
            }
            choices.Add(new KksConversationView.Choice("前へ",()=>ChooseMerchantItem(giving,page-_catalog.Runtime.one),page>default(int)));
            choices.Add(new KksConversationView.Choice("次へ",()=>ChooseMerchantItem(giving,page+_catalog.Runtime.one),(page+_catalog.Runtime.one)*size<items.Length));
            choices.Add(new KksConversationView.Choice("交換内容へ戻る",ShowNativeMerchant)); _view.SetChoices(choices);
        }
    }
}
