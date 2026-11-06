using System;
using System.Linq;
using System.Collections.Generic;
using MainGameAiShoujoMode.Life;
using UnityEngine;

namespace MainGameAiShoujoMode.Furniture
{
    internal sealed partial class AiFurnitureSession
    {
        private static string Target(string instance,FurnitureSlot slot)=>"bed:"+instance+"/"+slot.id;
        private PlacedBed FindBed(string target)
        {
            foreach(var placed in _placed.Values)foreach(var slot in placed.Item.slots??Array.Empty<FurnitureSlot>())
                if(Target(placed.Data.id,slot)==target&&slot.status=="ready")return new PlacedBed{Instance=placed.Data.id,Root=placed.Root.transform,Slot=slot};
            throw new InvalidOperationException("指定されたベッドの利用地点がありません。");
        }
        private void ValidateBed(PlacedBed bed,AiLifeController resident)
        {
            Require();if(Editing||resident==null||resident.InH||_actor.Root==null||_reservations.Busy(bed.Instance))throw new InvalidOperationException("現在はこのベッドを利用できません。");
            if(resident.State.sickness!=resident.Rules.healthySickness)throw new InvalidOperationException("病気のため通常のベッド休息は開始できません。");
            resident.Activities.Animations.Sequence(bed.Slot.action);
            var navigation=_host.Session.Navigation;var approach=bed.Root.TransformPoint(Vector(bed.Slot.approach));var exit=bed.Root.TransformPoint(Vector(bed.Slot.exit));
            if(navigation==null||!navigation.TryProject(approach,out var floor)||!navigation.TryCalculatePath(_actor.Root.position,floor,out _)
                ||!navigation.TryProject(exit,out _))throw new InvalidOperationException("指定されたベッドの接近・退出地点へ移動できません。");
        }
        internal IEnumerable<BedOption> AvailableBeds(AiLifeController resident)
        {
            if(!Allowed||_broken||Editing||resident==null||_actor.Root==null)yield break;
            foreach(var placed in _placed.Values)foreach(var slot in placed.Item.slots??Array.Empty<FurnitureSlot>())
            {
                if(slot.status!="ready")continue;
                var bed=new PlacedBed{Instance=placed.Data.id,Root=placed.Root.transform,Slot=slot};
                bool valid;try{ValidateBed(bed,resident);valid=true;}catch{valid=false;}
                if(valid)yield return new BedOption{Target=Target(placed.Data.id,slot),Name=placed.Item.name,Bed=bed};
            }
        }
        private void QueueBed(string target)
        {
            var resident=_resident()??throw new InvalidOperationException("女の子がいません。");var bed=FindBed(target);ValidateBed(bed,resident);
            if(resident.Conversing)throw new InvalidOperationException("会話を終えてからベッドを指定してください。");
            if(_host.Session.Panel)_host.Session.TogglePanel();resident.QueueActivity("rest",Guid.NewGuid().ToString("N"),furnitureTarget:target);
        }
        internal IDisposable BeginBed(AiLifeController life,AiLifeActivity activity,Action completed,Action<Exception> failed)
        {
            var bed=FindBed(activity.furnitureTarget);ValidateBed(bed,life);
            var run=new BedRun(this,life,activity,bed,completed,failed);_beds.Add(run);
            try{run.Start();return run;}catch{run.Dispose();throw;}
        }
        private sealed class BedRun:IDisposable
        {
            private readonly AiFurnitureSession _owner;
            private readonly AiLifeController _life;
            private readonly AiLifeActivity _activity;
            private readonly PlacedBed _bed;
            private readonly ChaControl _identity;
            private readonly Action _completed;
            private readonly Action<Exception> _failed;
            private IDisposable _reservation;
            private AiLifeActivityMotion _motion;
            private bool _done,_aligned;
            private Quaternion _standing;
            internal BedRun(AiFurnitureSession owner,AiLifeController life,AiLifeActivity activity,PlacedBed bed,Action completed,Action<Exception> failed)
            {_owner=owner;_life=life;_activity=activity;_bed=bed;_completed=completed;_failed=failed;_identity=owner._actor.CurrentActor;}
            private bool Current=>!_done&&_owner.Allowed&&ReferenceEquals(_identity,_owner._actor.CurrentActor)&&!_life.Conversing&&!_life.InH&&_life.State.activity?.id==_activity.id;
            internal void Start()
            {
                _reservation=_owner._reservations.Acquire(_bed.Instance,_bed.Slot.id,_activity.id);_standing=_owner._actor.Root.rotation;
                var point=_bed.Root.TransformPoint(Vector(_bed.Slot.approach));
                if(!_owner._host.Session.Navigation.TryProject(point,out var floor))throw new InvalidOperationException("ベッドの接近地点を確認できません。");
                _life.ActivityDestination(_activity.id,_activity.furnitureTarget,floor.x,floor.y,floor.z);
                _owner._actor.WalkSpecialSegment(floor,Arrived,Fail);
            }
            private void Arrived()
            {
                if(!Current)return;
                try
                {
                    _owner._actor.StopSpecialSegment();_aligned=true;
                    _owner._actor.Root.SetPositionAndRotation(_bed.Root.TransformPoint(Vector(_bed.Slot.position)),_bed.Root.rotation*Quaternion.Euler(Vector(_bed.Slot.rotation)));
                    _life.ActivityArrived(_activity.id);
                    var current=FurnitureJson.Copy(_life.State.activity);current.animationActionId=_bed.Slot.action;
                    _motion=new AiLifeActivityMotion(_owner._actor,_life.Activities,current,false);
                    _motion.Begin(current,Finish,Fail);
                }
                catch(Exception error){Fail(error);}
            }
            private void Finish()
            {
                if(!Current)return;
                try{Dispose();_completed();}catch(Exception error){_failed(error);}
            }
            private void Fail(Exception error){if(!Current)return;Dispose();_failed(error);}
            internal void Cancel(string reason){if(Current)Fail(new InvalidOperationException(reason));else{Dispose();_life.CancelActivity(reason,_activity.id);}}
            public void Dispose()
            {
                if(_done)return;_done=true;
                try
                {
                    _motion?.Dispose();_motion=null;
                    if(ReferenceEquals(_identity,_owner._actor.CurrentActor))
                    {
                        _owner._actor.StopSpecialSegment();
                        if(_aligned&&_bed.Root!=null)
                        {
                            var exit=_bed.Root.TransformPoint(Vector(_bed.Slot.exit));
                            if(!_owner._host.Session.Navigation.TryProject(exit,out var floor))floor=_bed.Root.TransformPoint(Vector(_bed.Slot.approach));
                            _owner._actor.Root.SetPositionAndRotation(floor,_standing);
                        }
                    }
                }
                finally{_reservation?.Dispose();_reservation=null;_owner._beds.Remove(this);}
            }
        }
    }
}
