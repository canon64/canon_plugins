using System;
using System.Collections.Generic;
namespace MainGameSpiralEyes
{
 public sealed class OverlayOwners<TActor,TPermit>
 {
  readonly Dictionary<TActor,Dictionary<string,TPermit>> _requests=new Dictionary<TActor,Dictionary<string,TPermit>>();
  public bool Add(TActor actor,string owner,TPermit permit)
  {
   if(!_requests.TryGetValue(actor,out var owners))_requests.Add(actor,owners=new Dictionary<string,TPermit>(StringComparer.Ordinal));
   bool added=!owners.ContainsKey(owner);owners[owner]=permit;return added;
  }
  public bool Remove(TActor actor,string owner)
  {
   if(!_requests.TryGetValue(actor,out var owners)||!owners.Remove(owner))return false;
   if(owners.Count==0)_requests.Remove(actor);return true;
  }
  public bool Has(TActor actor,Func<TPermit,bool> valid)
  {
   if(_requests.TryGetValue(actor,out var owners))foreach(var permit in owners.Values)if(valid(permit))return true;
   return false;
  }
  public bool TryGet(TActor actor,string owner,out TPermit value)
  {
   if(_requests.TryGetValue(actor,out var owners)&&owners.TryGetValue(owner,out value))return true;
   value=default(TPermit);return false;
  }
  public float Maximum(TActor actor,Func<TPermit,bool> valid,Func<TPermit,float> intensity)
  {
   float maximum=0;
   if(_requests.TryGetValue(actor,out var owners))foreach(var value in owners.Values)if(valid(value))maximum=Math.Max(maximum,intensity(value));
   return maximum;
  }
  public List<KeyValuePair<TActor,string>> Clear()
  {
   var removed=new List<KeyValuePair<TActor,string>>();
   foreach(var pair in _requests)foreach(var owner in pair.Value.Keys)removed.Add(new KeyValuePair<TActor,string>(pair.Key,owner));
   _requests.Clear();return removed;
  }
 }
}
