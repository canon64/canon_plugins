// Generated from saved actors-raw-tables hitemobj; HSceneManager.CheckHadItem1042.
using System; namespace MainGameAiShoujoH.Progression { public static class NativeHItemRequirements {
public const int InventoryCategory = 14;
public static int[] Required(int mode,int id) { switch(mode.ToString()+":"+id) {
case "0:9": return new int[] { 1 };
case "0:15": return new int[] { 0 };
case "0:16": return new int[] {  };
case "0:20": return new int[] { 0 };
case "1:17": return new int[] {  };
case "2:19": return new int[] { 2 };
case "2:27": return new int[] {  };
case "2:44": return new int[] {  };
case "2:45": return new int[] {  };
case "3:2": return new int[] {  };
case "3:3": return new int[] { 3 };
case "3:10": return new int[] {  };
case "3:11": return new int[] {  };
case "3:12": return new int[] {  };
case "3:13": return new int[] {  };
case "3:14": return new int[] {  };
default:return Array.Empty<int>(); } }
public static bool Check(int mode,int id,Func<int,bool> ownsNativeItemId) { if(ownsNativeItemId==null) throw new ArgumentNullException(nameof(ownsNativeItemId)); foreach(int required in Required(mode,id)) if(!ownsNativeItemId(required)) return false; return true; }
} }