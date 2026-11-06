using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace MainGameAiShoujoMode.Furniture
{
#pragma warning disable CS0649
    [DataContract] internal sealed class FurnitureVector
    {
        [DataMember] public float x,y,z;
        internal void Validate() { if (new[] {x,y,z}.Any(v => float.IsNaN(v)||float.IsInfinity(v))) throw new InvalidDataException("家具の位置・角度が不正です。"); }
    }
    [DataContract] internal sealed class FurnitureSlot
    {
        [DataMember] public string id, path, pivot, recovery, action, sourcePoint, status, error;
        [DataMember] public FurnitureVector approach, position, rotation, exit;
    }
    [DataContract] internal sealed class FurnitureItem
    {
        [DataMember] public int id, category;
        [DataMember] public string name, sourceBundle, sourceAsset, status, error, bundle, asset;
        [DataMember(EmitDefaultValue=false)] public string thumbnail;
        [DataMember] public FurnitureVector center, size;
        [DataMember] public FurnitureSlot[] slots;
        internal bool Available => status=="converted" && !string.IsNullOrWhiteSpace(bundle) && Path.GetFileName(bundle)==bundle && !string.IsNullOrWhiteSpace(asset);
    }
    [DataContract] internal sealed class FurnitureCategory
    {
        [DataMember] public int id;
        [DataMember] public string name;
    }
    [DataContract] internal sealed class FurnitureCatalog
    {
        [DataMember] public FurnitureItem[] items;
        [DataMember] public FurnitureCategory[] categories;
        internal static FurnitureCatalog Load(string path)
        {
            var catalog = FurnitureJson.Read<FurnitureCatalog>(File.ReadAllText(path));
            if(catalog?.items==null || catalog.items.Any(i=>i==null || string.IsNullOrWhiteSpace(i.name)) || catalog.items.Select(i=>i.id).Distinct().Count()!=catalog.items.Length)
                throw new InvalidDataException("家具一覧が不正です。");
            foreach(var item in catalog.items.Where(i=>i.Available))
            {
                if(item.center==null||item.size==null)throw new InvalidDataException("家具の範囲がありません。");
                item.center.Validate();item.size.Validate();
                if(new[]{item.size.x,item.size.y,item.size.z}.Any(v=>v<=default(float)))throw new InvalidDataException("家具の大きさが不正です。");
                foreach(var slot in (item.slots??Array.Empty<FurnitureSlot>()).Where(s=>s.status=="ready"))
                {
                    if(string.IsNullOrWhiteSpace(slot.id)||string.IsNullOrWhiteSpace(slot.action)||slot.approach==null||slot.position==null||slot.rotation==null||slot.exit==null)throw new InvalidDataException("ベッドの利用地点が不正です。");
                    slot.approach.Validate();slot.position.Validate();slot.rotation.Validate();slot.exit.Validate();
                }
            }
            return catalog;
        }
    }
    [DataContract] internal sealed class FurnitureInstance
    {
        [DataMember] public string id;
        [DataMember] public int item;
        [DataMember] public FurnitureVector position,rotation;
        internal void Validate()
        {
            if(!Guid.TryParse(id,out _)||item<default(int)||position==null||rotation==null)throw new InvalidDataException("家具の保存内容が不正です。");
            position.Validate();rotation.Validate();
        }
    }
    [DataContract] internal sealed class FurnitureLayout
    {
        [DataMember] public string version="ai-furniture-v1",world="ai-island";
        [DataMember] public FurnitureInstance[] items=Array.Empty<FurnitureInstance>();
        internal void Validate()
        {
            if(version!="ai-furniture-v1"||world!="ai-island"||items==null||items.Any(i=>i==null)||items.Select(i=>i.id).Distinct().Count()!=items.Length)
                throw new InvalidDataException("家具配置の形式・島・識別子が不正です。");
            foreach(var item in items)item.Validate();
        }
    }
    internal static class FurnitureJson
    {
        internal static T Read<T>(string text) { using(var stream=new MemoryStream(Encoding.UTF8.GetBytes(text)))return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream); }
        internal static string Write<T>(T value) { using(var stream=new MemoryStream()){new DataContractJsonSerializer(typeof(T)).WriteObject(stream,value);return Encoding.UTF8.GetString(stream.ToArray());} }
        internal static T Copy<T>(T value)=>Read<T>(Write(value));
    }
    internal sealed class FurnitureStore
    {
        private readonly string _path;
        internal FurnitureStore(string path){_path=Path.GetFullPath(path);}
        internal FurnitureLayout Read()
        {
            if(!File.Exists(_path))return new FurnitureLayout();
            var result=FurnitureJson.Read<FurnitureLayout>(File.ReadAllText(_path));
            if(result==null)throw new InvalidDataException("家具配置が空です。元の保存を保持します。");
            result.Validate();return result;
        }
        internal void Write(FurnitureLayout layout)
        {
            layout.Validate();Read();Directory.CreateDirectory(Path.GetDirectoryName(_path));var temporary=_path+".tmp";
            try
            {
                using(var stream=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None))
                using(var writer=new StreamWriter(stream,new UTF8Encoding(false))){writer.Write(FurnitureJson.Write(layout));writer.Flush();stream.Flush(true);}
                FurnitureJson.Read<FurnitureLayout>(File.ReadAllText(temporary)).Validate();
                if(File.Exists(_path))File.Replace(temporary,_path,_path+".bak");else File.Move(temporary,_path);
            }
            finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
    }
    internal sealed class FurnitureReservations
    {
        private readonly Dictionary<string,string> _owners=new Dictionary<string,string>(StringComparer.Ordinal);
        internal bool Busy(string instance)=>_owners.Keys.Any(k=>k.StartsWith(instance+"/",StringComparison.Ordinal));
        internal IDisposable Acquire(string instance,string slot,string operation)
        {
            if(string.IsNullOrWhiteSpace(operation)||Busy(instance))throw new InvalidOperationException("このベッドは利用中です。");
            var key=instance+"/"+slot;_owners.Add(key,operation);
            return new Release(()=>{if(_owners.TryGetValue(key,out var owner)&&owner==operation)_owners.Remove(key);});
        }
        private sealed class Release:IDisposable
        {private Action _release;internal Release(Action release){_release=release;}public void Dispose(){var action=_release;_release=null;action?.Invoke();}}
    }
#pragma warning restore CS0649
}
