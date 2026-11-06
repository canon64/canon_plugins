using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace MainGameSpiralEyes
{
 [DataContract]
 public sealed class Definition
 {
  [DataMember(IsRequired=true)] public string key;
  [DataMember(IsRequired=true)] public string label;
  [DataMember(IsRequired=true)] public string group;
  [DataMember(IsRequired=true)] public float value;
  [DataMember(IsRequired=true)] public float min;
  [DataMember(IsRequired=true)] public float max;
 }
 public static class ParameterDefinitions
 {
  [DataContract]
  sealed class Document
  {
   [DataMember(IsRequired=true)] public Definition[] parameters { get; set; }
  }
  public static Definition[] Load(string path)
  {
   Document document;
   using(var stream=File.OpenRead(path))
    document=(Document)new DataContractJsonSerializer(typeof(Document)).ReadObject(stream);
   if(document?.parameters==null||document.parameters.Length==0)
    throw new InvalidDataException("SpiralEyes parameters are missing: "+path);
   var keys=new HashSet<string>(StringComparer.Ordinal);
   foreach(var d in document.parameters)
    if(d==null||string.IsNullOrWhiteSpace(d.key)||string.IsNullOrWhiteSpace(d.group)
       ||!keys.Add(d.key)||!Finite(d.min)||!Finite(d.max)||!Finite(d.value)
       ||d.min>d.max||d.value<d.min||d.value>d.max)
     throw new InvalidDataException("Invalid SpiralEyes parameter definition: "+path);
   return document.parameters;
  }
  static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
 }
}
