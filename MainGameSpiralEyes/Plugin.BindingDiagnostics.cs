using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace MainGameSpiralEyes
{
 public sealed partial class Plugin
 {
  // Failure-event only. Capture before BindActor rolls partial bindings back.
  readonly HashSet<int> _bindingFailuresLogged=new HashSet<int>();
  static readonly string[] BindingTextureProperties={"_MainTex","_expression","_overtex1","_overtex2"};
  void LogBindingFailureOnce(ChaControl actor,Exception error)
  {
   if(_diagnostics?.Value!=true||actor==null||!_bindingFailuresLogged.Add(actor.GetInstanceID()))return;
   try
   {
    var report=new StringBuilder();
    report.AppendLine("SPIRAL_BINDING_FAILURE v1.6.2 "+DateTime.Now.ToString("o")
     +" actor="+actor.name+"#"+actor.GetInstanceID()+" eyesPtn="+actor.fileStatus.eyesPtn);
    report.AppendLine("error="+error);
    var eyes=actor.rendEye;
    report.AppendLine("eyeRenderers="+(eyes==null?0:eyes.Length));
    if(eyes!=null)for(int side=0;side<eyes.Length;side++)
    {
     var renderer=eyes[side];
     if(renderer==null){report.AppendLine("eye["+side+"]=null");continue;}
     var materials=renderer.sharedMaterials;
     bool bound=_followBindings!=null&&_followBindings.HasBinding(renderer);
     report.AppendLine("eye["+side+"]="+renderer.name+"#"+renderer.GetInstanceID()
      +" bound="+bound+" materials="+materials.Length);
     for(int index=0;index<materials.Length;index++)
     {
      var material=materials[index];
      if(material==null){report.AppendLine(" slot["+index+"]=null");continue;}
      var shader=material.shader;
      var missing=new List<string>();
      foreach(var property in new[]{"_MainTex","_expression","_rotation"})
       if(!material.HasProperty(property))missing.Add(property);
      string reason=shader==null?"no-shader":(shader.name=="Koikano/main_eye"||shader.name=="KKUTSeye")
       ?(missing.Count==0?"native-candidate":"missing-required-properties")
       :(shader.name=="canon/PupilFollow"||shader.name=="canon/PupilFollowKKUTSeye")?(bound?"follow-bound":"follow-without-renderer-binding")
       :"shader-name-mismatch";
      report.AppendLine(" slot["+index+"]="+material.name+"#"+material.GetInstanceID()
       +" shader="+(shader==null?"<null>":shader.name+"#"+shader.GetInstanceID())
       +" supported="+(shader!=null&&shader.isSupported)+" reason="+reason
       +" missing="+(missing.Count==0?"<none>":string.Join(",",missing)));
      report.Append("  properties:");
      foreach(var property in BindingTextureProperties)report.Append(" "+property+"="+material.HasProperty(property));
      report.AppendLine(" _rotation="+material.HasProperty("_rotation"));
      report.Append("  sameMaterialSlots:");
      for(int otherSide=0;otherSide<eyes.Length;otherSide++)
      {
       if(eyes[otherSide]==null)continue;
       var others=eyes[otherSide].sharedMaterials;
       for(int otherIndex=0;otherIndex<others.Length;otherIndex++)
        if(ReferenceEquals(material,others[otherIndex])||material==others[otherIndex])
         report.Append(" eye["+otherSide+"]/slot["+otherIndex+"]");
      }
      report.AppendLine();
     }
    }
    string text=report.ToString();
    Logger.LogWarning(text);
    string path=Path.Combine(Folder,"SpiralEyesBindingFailure.log");
    if(File.Exists(path)&&new FileInfo(path).Length>262144)File.WriteAllText(path,text,Encoding.UTF8);
    else File.AppendAllText(path,text,Encoding.UTF8);
   }
   catch(Exception diagnosticError)
   {
    Logger.LogWarning("SpiralEyes binding diagnostic failed: "+diagnosticError.Message);
   }
  }
 }
}
