using System;
using CanonGate;
namespace MainGameSyringe
{
 public sealed class SessionLifecycle:IDisposable
 {
  readonly string _guid;readonly Action _begin,_end;readonly IDisposable _registration;
  GateApi.Permit _permit;bool _active;
  public bool Allowed=>_active&&_permit.IsValid;
  public SessionLifecycle(string guid,Action begin,Action end)
  {_guid=guid;_begin=begin;_end=end;_registration=GateApi.Register(guid,Start,Stop);}
  void Start()
  {
   if(_active||GateApi.Current?.Kind!=GateApi.SessionKind.H)return;
   _permit=GateApi.Capture(_guid);if(!_permit.IsValid)return;_active=true;
   try{_begin();}catch{Stop();throw;}
  }
  public void Stop(){if(!_active)return;_active=false;_permit=default;_end();}
  public void Dispose(){_registration.Dispose();Stop();}
 }
}
