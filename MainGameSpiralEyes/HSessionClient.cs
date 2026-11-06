using System;
using CanonGate;
using UnityEngine;
namespace MainGameSpiralEyes
{
 public sealed class HSessionClient : IDisposable
 {
  readonly string _guid; readonly Func<bool> _enabled; readonly Action<MonoBehaviour> _begin; readonly Action _end;
  readonly IDisposable _registration; GateApi.Permit _permit; bool _active;
  public bool Allowed=>_active&&_permit.IsValid&&_enabled();
  public HSessionClient(string guid,Func<bool> enabled,Action<MonoBehaviour> begin,Action end)
  {_guid=guid;_enabled=enabled;_begin=begin;_end=end;_registration=GateApi.Register(guid,Start,Stop);}
  void Start()
  {
   if(_active||!_enabled()||GateApi.Current?.Kind!=GateApi.SessionKind.H)return;
   _permit=GateApi.Capture(_guid);if(!_permit.IsValid)return;_active=true;
   try{_begin(GateApi.Current.Scene);}catch{Stop();throw;}
  }
  public void Stop(){if(!_active)return;_active=false;_permit=default;_end();}
  public void Dispose(){_registration.Dispose();Stop();}
 }
}
