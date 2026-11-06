using System;
using System.Collections.Generic;
namespace MainGameSyringe
{
 public enum Hand { None, Left, Right }
 public sealed class InjectionPolicy<TActor>
 {
  public Hand HeldBy {get;private set;}
  readonly HashSet<Hand> _pressed=new HashSet<Hand>();
  readonly HashSet<TActor> _injected=new HashSet<TActor>();
  public bool Grab(Hand hand){if(hand==Hand.None||HeldBy!=Hand.None)return false;HeldBy=hand;return true;}
  public void Drop(){HeldBy=Hand.None;}
  public void ResetInput(){Drop();_pressed.Clear();}
  public bool TriggerDown(Hand hand)=>_pressed.Add(hand)&&HeldBy==hand&&hand!=Hand.None;
  public void TriggerUp(Hand hand)=>_pressed.Remove(hand);
  public bool IsInjected(TActor actor)=>_injected.Contains(actor);
  public void Commit(TActor actor)=>_injected.Add(actor);
  public void Release(TActor actor)=>_injected.Remove(actor);
  public List<TActor> ClearInjected(){var actors=new List<TActor>(_injected);_injected.Clear();return actors;}
  public List<TActor> Clear(){var actors=ClearInjected();_pressed.Clear();Drop();return actors;}
  public static TActor UniqueTarget(IEnumerable<TActor> hits,Func<TActor,bool> eligible)
  {
   var unique=new HashSet<TActor>(hits);
   // Reject overlapping characters, even if only one of them is a doll.
   if(unique.Count!=1)return default(TActor);
   foreach(var actor in unique)return eligible(actor)?actor:default(TActor);
   return default(TActor);
  }
 }
}
