using System;
using System.Collections.Generic;
namespace MainGameUnifiedUi
{
 public static class TabOrdering
 {
  // Called only when registrations change. Keeps anchored groups adjacent despite equal numeric orders.
  public static List<T> Arrange<T>(IList<T> sorted,Func<T,string> id,Func<T,string> after)
  {
   var ids=new HashSet<string>(StringComparer.Ordinal);var children=new Dictionary<string,List<T>>(StringComparer.Ordinal);
   foreach(var item in sorted)ids.Add(id(item));
   foreach(var item in sorted)
   {
    string anchor=after(item);if(string.IsNullOrEmpty(anchor))continue;
    if(!children.TryGetValue(anchor,out var list))children[anchor]=list=new List<T>();list.Add(item);
   }
   var result=new List<T>();var placed=new HashSet<string>(StringComparer.Ordinal);
   Action<T> append=null;append=item=>{if(!placed.Add(id(item)))return;result.Add(item);if(children.TryGetValue(id(item),out var list))foreach(var child in list)append(child);};
   foreach(var item in sorted)if(string.IsNullOrEmpty(after(item))||!ids.Contains(after(item)))append(item);
   foreach(var item in sorted)append(item); // Unresolved cycles remain visible in the original order.
   return result;
  }
 }
}
