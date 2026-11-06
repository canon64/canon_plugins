using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace MainGameBlankMapAdd
{
    [DataContract]
    internal sealed class HLocationPreset
    {
        [DataMember] public string Id, MapKey, Name, Image;
        [DataMember] public float[] Position, Rotation, Center;
        [DataMember] public float[] CameraPosition, CameraRotation, CameraFocus;
        [DataMember] public float Fov;
        [DataMember] public bool Legacy;
        [DataMember(EmitDefaultValue=false)] public bool IsDefault;
        internal HLocationPreset Clone() => new HLocationPreset {
            Id=Id, MapKey=MapKey, Name=Name, Image=Image, Fov=Fov, Legacy=Legacy, IsDefault=IsDefault,
            Position=Copy(Position), Rotation=Copy(Rotation), Center=Copy(Center),
            CameraPosition=Copy(CameraPosition), CameraRotation=Copy(CameraRotation), CameraFocus=Copy(CameraFocus) };
        private static float[] Copy(float[] value) => value == null ? null : (float[])value.Clone();
        internal void Validate()
        {
            if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(MapKey) || string.IsNullOrWhiteSpace(Name) || Name.Length>80)
                throw new InvalidDataException("H場所のID・マップ・名前が不正です。");
            if(Id.IndexOfAny(Path.GetInvalidFileNameChars())>=0||Id.Contains("..")||Id.IndexOfAny(new[]{'/', '\\', ':'})>=0)throw new InvalidDataException("H場所IDが不正です。");
            foreach(var value in new[]{Position,Rotation,Center}) CheckVector(value);
            if (!Legacy) {
                foreach(var value in new[]{CameraPosition,CameraRotation,CameraFocus}) CheckVector(value);
                if (float.IsNaN(Fov)||float.IsInfinity(Fov)||Fov<1||Fov>179) throw new InvalidDataException("画角が不正です。");
            }
            if (!string.IsNullOrEmpty(Image) && (Path.GetFileName(Image)!=Image || Image.IndexOfAny(new[]{'/', '\\', ':'})>=0 || !Image.EndsWith(".png",StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("画像の保存名が不正です。");
        }
        private static void CheckVector(float[] value) {
            if(value==null||value.Length!=3||value.Any(v=>float.IsNaN(v)||float.IsInfinity(v))) throw new InvalidDataException("H場所の座標が不正です。");
        }
    }
    [DataContract]
    internal sealed class HLocationSequence
    {
        [DataMember] public string MapKey;
        [DataMember] public int Next;
        internal HLocationSequence Clone() => (HLocationSequence)MemberwiseClone();
    }
    [DataContract]
    internal sealed class HLocationDocument
    {
        [DataMember] public int Version=1;
        [DataMember] public List<HLocationPreset> Presets=new List<HLocationPreset>();
        [DataMember] public List<HLocationSequence> Sequences=new List<HLocationSequence>();
        internal HLocationDocument Clone() => new HLocationDocument { Version=Version, Presets=Presets.Select(p=>p.Clone()).ToList(), Sequences=Sequences.Select(s=>s.Clone()).ToList() };
        internal HLocationPreset DefaultFor(string key) => Presets.SingleOrDefault(p=>p.IsDefault&&HLocationMapIdentity.Same(p.MapKey,key));
        internal HLocationDocument WithDefault(string key,string id,bool enabled)
        {
            var document=Clone();
            var target=document.Presets.SingleOrDefault(p=>p.Id==id&&HLocationMapIdentity.Same(p.MapKey,key));
            if(target==null||enabled&&target.Legacy)throw new InvalidDataException("撮影・保存済みのH場所を選んでください。");
            foreach(var p in document.Presets.Where(p=>HLocationMapIdentity.Same(p.MapKey,key)))p.IsDefault=enabled&&p.Id==id;
            document.Validate();return document;
        }
        internal string AllocateName(string key) {
            var sequence=Sequences.SingleOrDefault(s=>s.MapKey==key);
            if(sequence==null){ sequence=new HLocationSequence{MapKey=key,Next=1};Sequences.Add(sequence); }
            string name;
            do { name="H場所"+sequence.Next.ToString("D3"); sequence.Next=checked(sequence.Next+1); }
            while(Presets.Any(p=>HLocationMapIdentity.Same(p.MapKey,key)&&p.Name==name));
            return name;
        }
        internal void Validate() {
            if(Version!=1||Presets==null||Sequences==null)throw new InvalidDataException("H場所ファイルの形式が不正です。");
            var ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(var p in Presets){if(p==null)throw new InvalidDataException("H場所が空です。");p.Validate();if(!ids.Add(p.Id))throw new InvalidDataException("H場所IDが重複しています。");}
            if(Presets.Any(p=>p.IsDefault&&p.Legacy)||Presets.Where(p=>p.IsDefault).GroupBy(p=>HLocationMapIdentity.Canonical(p.MapKey)).Any(g=>g.Count()>1))
                throw new InvalidDataException("デフォルトH場所の指定が不正です。");
            var keys=new HashSet<string>(StringComparer.Ordinal);
            foreach(var s in Sequences)if(s==null||string.IsNullOrEmpty(s.MapKey)||s.Next<1||!keys.Add(s.MapKey))throw new InvalidDataException("連番が不正です。");
        }
        internal static HLocationDocument Import(MapWarpDocument old) {
            var result=new HLocationDocument();
            foreach(var p in old.Points)result.Presets.Add(new HLocationPreset {Id=p.Id,MapKey=p.MapKey,Name=p.Name,Legacy=true,
                Position=new[]{p.X,p.Y,p.Z},Rotation=new float[3],Center=p.HasRotationCenter?new[]{p.CenterX,p.CenterY,p.CenterZ}:new[]{p.X,p.Y,p.Z}});
            result.Validate();return result;
        }
    }
    internal static class HLocationPresetStore
    {
        internal static HLocationDocument Load(string path,string legacy) {
            if(!File.Exists(path))return File.Exists(legacy)?HLocationDocument.Import(MapWarpPointStore.Load(legacy)):new HLocationDocument();
            using(var input=File.OpenRead(path)) {
                var result=(HLocationDocument)new DataContractJsonSerializer(typeof(HLocationDocument)).ReadObject(input);
                if(result==null)throw new InvalidDataException("H場所ファイルが空です。");result.Validate();return result;
            }
        }
        internal static void Save(string path,HLocationDocument document) {
            document.Validate();string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try {
                using(var output=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
                    new DataContractJsonSerializer(typeof(HLocationDocument)).WriteObject(output,document);output.Flush(true);
                }
                if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);
            } finally {if(File.Exists(temp))File.Delete(temp);}
        }
        internal static void SaveImage(string path,string directory,HLocationDocument document,HLocationPreset preset,byte[] png) {
            string previous=preset.Image;
            string image=preset.Id+"_"+Guid.NewGuid().ToString("N")+".png";
            string destination=Path.Combine(directory,image),temp=destination+".tmp";
            Directory.CreateDirectory(directory);
            bool committed=false;
            try {
                using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(png,0,png.Length);stream.Flush(true);}
                File.Move(temp,destination);preset.Image=image;Save(path,document);committed=true;
            } finally {
                if(File.Exists(temp))File.Delete(temp);
                if(!committed){preset.Image=previous;if(File.Exists(destination))File.Delete(destination);}
            }
            // Retain prior image revisions: the JSON backup can still refer to them.
        }
    }
}
