using System;
using MainGameSpiralEyes;
using UnityEngine;

static class PupilFollowKkutsTests
{
 public static void Run(Action<bool,string> check)
 {
  var nativeFollow=new Shader("canon/PupilFollow");
  var utsFollow=new Shader("canon/PupilFollowKKUTSeye");
  var native=new Shader("Koikano/main_eye");var uts=new Shader("KKUTSeye");
  var binding=new PupilFollowBindings(nativeFollow,utsFollow);
  var texture=new Texture();var originalTexture=new Texture();
  var left=new Renderer();var right=new Renderer();var standard=new Renderer();
  foreach(var eye in new[]{left,right})
  {
   var material=eye.sharedMaterials[0];material.shader=uts;
   material.SetTexture("_MainTex",originalTexture);material.SetTexture("_overtex2",originalTexture);
   material.SetFloat("_CustomUtsValue",.73f);material.SetFloat("_exppower",.4f);
   material.shaderKeywords=new[]{"_UTS_CUSTOM"};material.renderQueue=2997;
  }
  standard.sharedMaterials[0].shader=native;
  binding.Apply(left,texture,Vector2.zero,1,0,false);
  binding.Apply(right,texture,Vector2.zero,1,1,false);
  binding.Apply(standard,texture,Vector2.zero,1,0,false);
  check(binding.Count==3,"KKUTSeye and standard bindings coexist");
  check(left.sharedMaterials[0].shader==utsFollow&&right.sharedMaterials[0].shader==utsFollow,"KKUTSeye chooses its own compiled pass");
  check(standard.sharedMaterials[0].shader==nativeFollow,"standard keeps standard compiled pass");
  foreach(var eye in new[]{left,right})
  {
   var material=eye.sharedMaterials[0];
   check(eye.sharedMaterials.Length==1,"KKUTSeye does not append a particle material");
   check(material.GetTexture("_MainTex")==originalTexture&&material.GetTexture("_overtex2")==originalTexture,"native pupil and highlight retained");
   check(material.GetFloat("_CustomUtsValue")==.73f&&material.GetFloat("_exppower")==.4f,"KKUTS values and existing expression retained");
   check(material.renderQueue==2997&&material.shaderKeywords[0]=="_UTS_CUSTOM","KKUTS keywords and queue retained");
  }
  binding.SetOpacity(left,.25f);
  check(left.sharedMaterials[0].GetFloat("_CanonSpiralOpacity")==.25f,"KKUTS fading reaches its shader");
  binding.Apply(left,texture,Vector2.zero,1,0,true);
  check(left.sharedMaterials[0].GetFloat("_exppower")==0,"manual spiral suppression works on KKUTS");
  binding.Restore(left);
  check(left.sharedMaterials[0].shader==uts&&left.sharedMaterials[0].GetFloat("_exppower")==.4f,"KKUTS restores original shader and expression");
  check(right.sharedMaterials[0].shader==utsFollow,"restoring one eye leaves the other bound");
  var replacement=new Shader("third-party replacement");right.sharedMaterials[0].shader=replacement;
  binding.Restore();binding.Restore();
  check(right.sharedMaterials[0].shader==replacement,"cleanup preserves later third-party shader replacement");
  check(standard.sharedMaterials[0].shader==native&&binding.Count==0,"mixed cleanup restores standard shader");
  var missing=new Renderer();missing.sharedMaterials[0].shader=uts;missing.sharedMaterials[0].Expression=false;
  bool rejected=false;try{binding.Apply(missing,texture,Vector2.zero,1,0,false);}catch(InvalidOperationException){rejected=true;}
  check(rejected&&missing.sharedMaterials[0].shader==uts,"incomplete KKUTS material is rejected without replacement");
 }
}
