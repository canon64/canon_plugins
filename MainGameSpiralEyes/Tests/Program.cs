using System;
using System.IO;
using MainGameSpiralEyes;
class Program
{
 static void Main(string[] args)
 {
  LifecycleTests.Run();
  if(!SpiralSelection.Matches("sideloader/canon_spiral_eyes.unity3d","canon_spiral"))throw new Exception("New eye not recognized");
  if(!SpiralSelection.Matches("SIDELOADER\\CANON_SPIRAL_EYES.UNITY3D","canon_spiral"))throw new Exception("Normalized eye path not recognized");
  foreach(var pair in new[]{new[]{"chara/mt_eye_00.unity3d","cf_t_expression_00"},new[]{"other/canon_spiral_eyes.unity3d","canon_spiral"},new[]{"sideloader/canon_spiral_eyes.unity3d","other_eye"},new string[]{null,null}})
   if(SpiralSelection.Matches(pair[0],pair[1]))throw new Exception("Unrelated eye incorrectly selected");
  Console.WriteLine("PASS: selection by added eye asset identity, independent of remapped numeric ID; normal/other/null eyes rejected");
  var definitions=ParameterDefinitions.Load(args[0]);
  if(definitions.Length!=35)throw new Exception("Expected all 35 settings");
  if(Array.Find(definitions,x=>x.key=="colorSpeed").value>=0)throw new Exception("Inward flow default missing");
  if(Array.Find(definitions,x=>x.key=="spacing").value!=.125f)throw new Exception("Pitch default changed");
  var temp=Path.Combine(Path.GetTempPath(),"spiral-definitions-"+Guid.NewGuid()+".json");
  try
  {
   foreach(var invalid in new[]{"{}","{\"parameters\":null}","{\"parameters\":[]}"})
   {
    File.WriteAllText(temp,invalid);
    bool rejected=false;
    try{ParameterDefinitions.Load(temp);}catch(Exception){rejected=true;}
    if(!rejected)throw new Exception("Invalid definitions accepted");
   }
  }
  finally{if(File.Exists(temp))File.Delete(temp);}
  Console.WriteLine("PASS: 35 parameter definitions, inward flow, pitch, 3 malformed documents rejected");
 }
}
