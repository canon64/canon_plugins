using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace MainGameSyringe
{
 public enum VoiceCueEvent { Prepared, Inserted, Injected }
 public sealed class VoiceCueEntry
 {
  public string Id,Dialogue,FilePath;
 }
 public static class VoiceCueCatalog
 {
  [DataContract] sealed class Row
  {
   [DataMember] public int? id {get;set;}
   [DataMember] public string dialogue {get;set;}
   [DataMember] public string text {get;set;}
   [DataMember] public string file {get;set;}
   [DataMember] public Row[] items {get;set;}
  }
  // Both the batch GUI manifest and the user's id/dialogue JSON are read-only inputs.
  public static List<VoiceCueEntry> Parse(string json,string directory)
  {
   if(string.IsNullOrWhiteSpace(json))throw new InvalidDataException("JSONが空です。");
   Row[] rows;
   using(var stream=new MemoryStream(Encoding.UTF8.GetBytes(json)))
   {
    if(json.TrimStart().StartsWith("[",StringComparison.Ordinal))
     rows=(Row[])new DataContractJsonSerializer(typeof(Row[])).ReadObject(stream);
    else
    {
     var root=(Row)new DataContractJsonSerializer(typeof(Row)).ReadObject(stream);
     rows=root?.items??new[]{root};
    }
   }
   var result=new List<VoiceCueEntry>();var ids=new HashSet<string>(StringComparer.Ordinal);
   foreach(var row in rows)
   {
    if(row==null)throw new InvalidDataException("各項目はJSONオブジェクトにしてください。");
    string id=row.id?.ToString(CultureInfo.InvariantCulture)??"",text=row.dialogue;
    if(string.IsNullOrWhiteSpace(text))text=row.text??"";
    string file=row.file;
    if(string.IsNullOrWhiteSpace(file))
    {
     if(!int.TryParse(id,NumberStyles.None,CultureInfo.InvariantCulture,out int number)||number<0)
      throw new InvalidDataException("fileのない項目には数値idが必要です。");
     file=number.ToString(CultureInfo.InvariantCulture)+".wav";
     var padded=number.ToString("D3",CultureInfo.InvariantCulture)+".wav";
     if(!File.Exists(Path.Combine(directory,file))&&File.Exists(Path.Combine(directory,padded)))file=padded;
    }
    if(Path.GetFileName(file)!=file||file.IndexOfAny(new[]{'/', '\\', ':'})>=0||
      !string.Equals(Path.GetExtension(file),".wav",StringComparison.OrdinalIgnoreCase))
     throw new InvalidDataException("WAV名だけを指定してください: "+file);
    if(id.Length>0&&!ids.Add(id))throw new InvalidDataException("idが重複しています: "+id);
    result.Add(new VoiceCueEntry{Id=id,Dialogue=text,FilePath=Path.Combine(directory,file)});
   }
   return result;
  }
  public static VoiceCueEntry Pick(IList<VoiceCueEntry> entries,string lastPath,Func<int,int> random)
  {
   if(entries.Count==0)return null;
   var choices=new List<VoiceCueEntry>();
   foreach(var entry in entries)if(entries.Count==1||!string.Equals(entry.FilePath,lastPath,StringComparison.OrdinalIgnoreCase))choices.Add(entry);
   if(choices.Count==0)choices.AddRange(entries);
   return choices[random(choices.Count)];
  }
 }
}
