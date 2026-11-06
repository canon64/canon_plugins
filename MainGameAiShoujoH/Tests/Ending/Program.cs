using System;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;
using MainGameAiShoujoH.Integration;
class Host : EndingCommandMachine.IHost
{
 public int Texts;
 public void Present(EndingCue cue, Action completed, Action<Exception> failed) { ++Texts; completed(); }
 public void ActorCommand(string command, string[] arguments) { }
}
class Program
{
 static void Main(string[] args)
 {
  var catalog = JsonSerializer.Deserialize<EndingCatalog>(File.ReadAllText(args[0]), new JsonSerializerOptions { IncludeFields = true }); int cases = 0;
  foreach (var script in catalog.scripts) for (int seed=0;seed<16;++seed) {
   var random = new Random(seed); var host = new Host(); bool complete=false; Exception failure=null;
   using var machine = new EndingCommandMachine(host, new Dictionary<string,object> {
    ["H_StatsTable[1]"]=50f,["H_DesireTable[1]"]=20f,["P_SpendMoney"]=100,["isWeaknessH"]=(seed&1)!=0,["JumpTag"]="OUT",
    ["numOrgasmFemale"]=seed,["ConditionMode"]=seed%3,["isPainAction"]=(seed&2)!=0,["isConstraintAction"]=(seed&4)!=0
   }, random.Next);
   machine.Start(script,()=>complete=true,error=>failure=error);
   if (failure!=null) throw new Exception(script.personality+":"+script.name+" seed="+seed,failure);
   if(!complete) throw new Exception("Synchronous presentation stalled "+script.name);
   if(script.name=="13" || script.name=="14") if(Convert.ToSingle(machine.Variables["H_DesireTable[1]"])!=70f) throw new Exception("Ending bath desire setter lost");
   ++cases;
  }
  Console.WriteLine("DEDICATED_H_ENDING_SCRIPT_PASS cases="+cases+" scripts="+catalog.scripts.Length);
 }
}
