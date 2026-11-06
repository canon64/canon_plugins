using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace MainGameAiShoujoMode.Life
{
    #pragma warning disable CS0649 // Assigned by the source-backed JSON deserializer.
    [DataContract] internal sealed class NativeMerchantCatalog
    {
        [DataContract] internal sealed class Table
        {
            [DataMember] public string kind, area;
            [DataMember] public Row[] rows;
        }
        [DataContract] internal sealed class Row
        {
            [DataMember] public int category, item, Group, Rate, Percent, Stock;
            [DataMember] public int[] Stocks;
        }
        [DataMember] public string itemCatalogSha256;
        [DataMember] public Table[] tables;
        internal static NativeMerchantCatalog Load(string path)
        {
            NativeMerchantCatalog value;
            using (var stream = File.OpenRead(path)) value = (NativeMerchantCatalog)new DataContractJsonSerializer(typeof(NativeMerchantCatalog)).ReadObject(stream);
            if (value?.tables == null || string.IsNullOrWhiteSpace(value.itemCatalogSha256) || value.tables.Any(t => t.rows == null)) throw new InvalidDataException("Native merchant catalog is missing.");
            return value;
        }
        internal AiLifeStateStore.NativeVendorItem[] NormalStock(int area, Func<int,int,int> random)
        {
            var table = tables.Single(t => t.kind == "normal" && t.area == area.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return table.rows.Select(row => {
                if (row.Stocks == null || !row.Stocks.Any() || row.Rate <= default(int)) throw new InvalidDataException("Native vendor count/rate is missing.");
                int count = row.Stocks.Length == Math.Sign(row.Stocks.Length) ? row.Stocks.First() : random(row.Stocks.First(), row.Stocks.Last());
                if (count < row.Stocks.First() || (row.Stocks.Length > Math.Sign(row.Stocks.Length) && count >= row.Stocks.Last())) throw new InvalidDataException("Native random stock is outside its original exclusive range.");
                return new AiLifeStateStore.NativeVendorItem { category=row.category, item=row.item, rate=row.Rate, group=row.Group, count=count, stock=count };
            }).ToArray();
        }
    }
}
