using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MapEnvironmentRuntime;
using UnityEngine;

namespace MainGameAiShoujoMode
{
    internal sealed class AiSpecialMovement : IDisposable
    {
        private readonly SandboxHost _host;
        private readonly AiAnimationTrial _girl;
        private readonly AiModePlayerAvatar _player;
        private readonly NavigationWorld _world;
        private readonly SandboxFreeCamera _view;
        private readonly Action<string> _log;
        private readonly Action<Exception> _error;
        private readonly SpecialMovementManifest _manifest;
        private readonly AssetBundle _bundle;
        private readonly RuntimeAnimatorController _femaleController,_maleController;
        private readonly Dictionary<int,SpecialMovementPoint> _points;
        private readonly HashSet<int> _occupied=new HashSet<int>();
        private SpecialMovementPlayback _playback;
        private Action _releaseActor,_restorePlayer;
        private bool _busy,_playerMoving,_disposed,_wasBlocked,_cancelling;
        private int? _reserved;
        private Queue<SpecialMovementEdge> _route;
        private Vector3 _routeTarget;
        private Dictionary<string,Vector3> _routePositions;
        private Dictionary<string,Vector3> _nodePositions;
        private List<SpecialMovementEdge> _baseEdges;
        private SpecialMovementStatus _status=new SpecialMovementStatus{state="ready"};
        private Parameters P=>_host.Session.Settings;
        public bool PlayerMoving=>_playerMoving;
        public AiSpecialMovement(SandboxHost host,AiAnimationTrial girl,AiModePlayerAvatar player,NavigationWorld world,string content,Action<string> log,Action<Exception> error)
        {
            _host=host;_girl=girl;_player=player;_world=world;_log=log;_error=error;_view=host.Walker;
            _manifest=JsonCodec.FromJson<SpecialMovementManifest>(File.ReadAllText(Path.Combine(content,P.T("traversal.manifest"))));_manifest.Validate(P);
            _points=_manifest.points.ToDictionary(point=>point.id);
            _bundle=AssetBundle.LoadFromFile(Path.Combine(content,P.T("traversal.bundle")));
            if(_bundle==null)throw new InvalidDataException("特殊移動アニメbundleを読み込めません。");
            try
            {
                _femaleController=_bundle.LoadAsset<RuntimeAnimatorController>(_manifest.femaleController);_maleController=_bundle.LoadAsset<RuntimeAnimatorController>(_manifest.maleController);
                if(_femaleController==null||_maleController==null)throw new InvalidDataException("特殊移動アニメcontrollerが不足しています。");
                _girl.ConversationInvalidated+=Cancel;_world.Rebuilding+=Cancel;_host.Session.ParametersApplied+=Changed;
                _world.Rebuilt+=InvalidateGraph;_host.Island.Doors.PassageChanged+=InvalidateGraph;
                _girl.SpecialRoute=TryGirlRoute;_girl.SpecialWaypoint=ChooseWaypoint;
                _host.Session.SpecialMovementSnapshot=()=>_status;
                _log("AI_SPECIAL_MOVEMENT_READY endpoints="+_points.Count);
            }
            catch{_bundle.Unload(true);throw;}
        }
        private void Changed(Parameters settings){InvalidateGraph();}
        private void InvalidateGraph(){Cancel();_baseEdges=null;_nodePositions=null;}
        private Vector3 Start(SpecialMovementPoint point)=>_host.FromIslandSourcePosition(point.start);
        private Vector3 Command(SpecialMovementPoint point)=>_host.FromIslandSourcePosition(point.command);
        private void Report(string state,string reason,SpecialMovementPoint point=null)
        {
            var feet=_view.transform.position+_view.FeetLocalOffset;
            _status=new SpecialMovementStatus{state=state,reason=reason,candidateId=point?.id,commandName=point?.commandName,
                distance=point==null?P.N("math.zero"):Vector3.Distance(feet,Command(point)),playerSourcePosition=_host.ToIslandSourcePosition(feet),commandSourcePosition=point==null?Vector3.zero:point.command};
            Parameters.AtomicWrite(Path.Combine(P.T("share.directory"),"special-movement-runtime.json"),JsonCodec.ToJson(_status,true));
        }
        private Quaternion Rotation(SpecialMovementPoint point)
        {
            var rotation=new Quaternion(point.rotation.x,point.rotation.y,point.rotation.z,point.rotation.w);
            Vector3 origin=Start(point),forward=_host.FromIslandSourcePosition(point.start+rotation*Vector3.forward)-origin;
            Vector3 up=_host.FromIslandSourcePosition(point.start+rotation*Vector3.up)-origin;
            return Quaternion.LookRotation(forward,up);
        }
        private SpecialMovementSegment[] Segments(SpecialMovementPoint point,bool male)=>
            (male?point.maleSegments:point.femaleSegments).Select(segment=>new SpecialMovementSegment{start=segment.start,end=segment.end,position=_host.FromIslandSourcePosition(segment.position)}).ToArray();
        private bool Project(Vector3 position,out Vector3 projected)=>_world.TryProject(position,out projected)&&Vector3.Distance(position,projected)<=P.N("traversal.projectionTolerance");
        private bool Available(SpecialMovementPoint point)=>!_occupied.Contains(point.id)&&!_occupied.Contains(point.linkedId);
        public bool TryInteract()
        {
            if(_disposed)return false;
            if(P.N("traversal.enabled")==P.N("math.zero")||P.T("movement.mode")!="walk"){Report("rejected","特殊移動が無効、または歩行モードではありません。");return false;}
            if(_playerMoving)return true;
            Vector3 feet=_view.transform.position+_view.FeetLocalOffset;
            var selected=SpecialMovementSelection.Choose(_points.Values,feet,P.N("traversal.reach"),Command);
            if(selected==null){Report("no-candidate","操作基準点が範囲外です。",_points.Values.OrderBy(point=>Vector3.Distance(feet,Command(point))).First());return false;}
            Report("selected",null,selected);
            if(_busy){Report("busy","特殊移動を使用中です。",selected);_log("AI_SPECIAL_MOVEMENT_BUSY");return true;}
            try
            {
                if(!Available(selected))throw new InvalidOperationException("この通路は使用中です。");
                var segments=Segments(selected,true);if(!Project(segments.Last().position,out var landing))throw new InvalidOperationException("特殊通路の出口に歩ける場所がありません："+selected.id);
                _busy=true;_playerMoving=true;_wasBlocked=_host.Session.ExternalInputBlocked;_host.Session.ExternalInputBlocked=true;
                _view.enabled=false;
                Play(selected,true,_player.Actor,_player.Root,segments,(success,ex)=>
                {
                    try{if(success)_view.WarpFeet(landing,Rotation(selected));_restorePlayer?.Invoke();}
                    catch(Exception restoreError){ex=ex??restoreError;}
                    finally
                    {
                        _restorePlayer=null;_host.Session.ExternalInputBlocked=_wasBlocked;_view.enabled=true;_view.SetCursorReleased(false);Finish();
                    }
                    Report(success&&ex==null?"completed":"failed",ex?.GetBaseException().Message,selected);
                    if(ex!=null)_error(ex);
                });
                return true;
            }
            catch(Exception ex){Cancel();Report("failed",ex.GetBaseException().Message,selected);_error(ex);return true;}
        }
        private void Reserve(SpecialMovementPoint point)
        {
            if(!Available(point))throw new InvalidOperationException("特殊通路は使用中です。");
            _occupied.Add(point.id);_occupied.Add(point.linkedId);_reserved=point.id;
        }
        private void Unreserve()
        {
            if(!_reserved.HasValue)return;var point=_points[_reserved.Value];_reserved=null;_occupied.Remove(point.id);_occupied.Remove(point.linkedId);
        }
        private void Play(SpecialMovementPoint point,bool male,ChaControl actor,Transform root,SpecialMovementSegment[] segments,Action<bool,Exception> completed)
        {
            if(actor==null||actor.animBody==null||root==null)throw new InvalidOperationException("特殊移動するキャラの準備が完了していません。");
            Reserve(point);
            var motion=new GameObject("AI Special Movement "+point.id);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(motion,_host.gameObject.scene);
            motion.transform.SetPositionAndRotation(Start(point),Rotation(point));
            try{if(male)_restorePlayer=_player.PrepareTraversal(motion.transform);}
            catch{UnityEngine.Object.Destroy(motion);Unreserve();throw;}
            _playback=motion.AddComponent<SpecialMovementPlayback>();
            _log("AI_SPECIAL_MOVEMENT_BEGIN id="+point.id+" kind="+point.kind+" male="+male);
            _playback.Begin(root,actor.animBody,male?_maleController:_femaleController,male?point.maleState:point.femaleState,
                male?point.maleDuration:point.femaleDuration,Start(point),Rotation(point),segments,P,(success,ex)=>
                {
                    _playback=null;Unreserve();_log("AI_SPECIAL_MOVEMENT_END id="+point.id+" success="+success);completed(success,ex);
                });
        }
        private NavigationWaypoint ChooseWaypoint(Vector3 position,ISet<string> reservations,System.Random random)
        {
            if(_disposed||_busy||_cancelling||P.N("traversal.enabled")==P.N("math.zero"))return null;
            foreach(var waypoint in _world.Waypoints.Where(item=>!reservations.Contains(item.Id)&&Vector3.Distance(position,item.Position)>=P.N("navigation.minDestinationDistance")&&Vector3.Distance(position,item.Position)<=P.N("navigation.maxDestinationDistance")).OrderBy(item=>random.Next()).Take(P.I("traversal.routeCandidates")))
                if(Plan(position,waypoint.Position)!=null)return waypoint;
            return null;
        }
        private SpecialMovementEdge[] Plan(Vector3 start,Vector3 target)
        {
            if(_world.TryCalculatePath(start,target,out _))
            {
                _routePositions=new Dictionary<string,Vector3>{{"start",start},{"target",target}};
                return new[]{new SpecialMovementEdge{from="start",to="target",cost=Vector3.Distance(start,target)}};
            }
            if(_baseEdges==null)
            {
                _nodePositions=new Dictionary<string,Vector3>();
                foreach(var point in _points.Values)if(Project(Start(point),out var projected))_nodePositions[point.id.ToString()]=projected;
                _baseEdges=new List<SpecialMovementEdge>();
                foreach(var from in _nodePositions)
                    foreach(var to in _nodePositions.Where(item=>item.Key!=from.Key))
                        if(_world.TryCalculatePath(from.Value,to.Value,out var path))_baseEdges.Add(new SpecialMovementEdge{from=from.Key,to=to.Key,cost=PathLength(path.corners)});
            }
            var positions=new Dictionary<string,Vector3>{{"start",start},{"target",target}};
            foreach(var node in _nodePositions)positions[node.Key]=node.Value;
            var edges=new List<SpecialMovementEdge>(_baseEdges);
            foreach(var node in _nodePositions)
            {
                if(_world.TryCalculatePath(start,node.Value,out var approach))edges.Add(new SpecialMovementEdge{from="start",to=node.Key,cost=PathLength(approach.corners)});
                if(_world.TryCalculatePath(node.Value,target,out var finish))edges.Add(new SpecialMovementEdge{from=node.Key,to="target",cost=PathLength(finish.corners)});
            }
            foreach(var point in _points.Values.Where(Available))
            {
                string from=point.id.ToString(),to=point.linkedId.ToString();
                if(positions.ContainsKey(from)&&positions.ContainsKey(to)&&Project(Segments(point,false).Last().position,out var exit)
                    &&Vector3.Distance(exit,positions[to])<=P.N("traversal.reach"))
                    edges.Add(new SpecialMovementEdge{from=from,to=to,passage=point.id,cost=Vector3.Distance(positions[from],exit)});
            }
            _routePositions=positions;return SpecialMovementRoutes.Find(edges,"start","target");
        }
        private float PathLength(Vector3[] corners)
        {
            float length=P.N("math.zero");
            for(int index=P.I("math.one");index<corners.Length;index+=P.I("math.one"))length+=Vector3.Distance(corners[index-P.I("math.one")],corners[index]);
            return length;
        }
        private bool TryGirlRoute(Vector3 target)
        {
            if(_disposed||_busy||_cancelling||P.N("traversal.enabled")==P.N("math.zero")||_girl.ConversationTarget==null)return false;
            var route=Plan(_girl.Root.position,target);
            if(route==null||!route.Any(edge=>edge.passage.HasValue))return false;
            _busy=true;_routeTarget=target;_route=new Queue<SpecialMovementEdge>(route);
            try{_releaseActor=_girl.PauseForExternalMotion();_girl.SpecialMoving=true;NextLeg();return true;}
            catch{Cancel();throw;}
        }
        private void NextLeg()
        {
            if(_disposed||!_busy||_playerMoving)return;
            try
            {
                if(_route==null||!_route.Any()){Finish();return;}
                var edge=_route.Dequeue();
                if(!edge.passage.HasValue){_girl.WalkSpecialSegment(_routePositions[edge.to],NextLeg,Failed);return;}
                var point=_points[edge.passage.Value];var segments=Segments(point,false);
                if(!Project(segments.Last().position,out var landing))throw new InvalidOperationException("女の子の通路出口を歩行経路へ接続できません。");
                _girl.SuspendSpecialAnimation();
                _girl.SpecialAnimationPlaying=true;
                Play(point,false,_girl.CurrentActor,_girl.Root,segments,(success,ex)=>
                {
                    _girl.SpecialAnimationPlaying=false;
                    if(!success){if(_cancelling){Finish();return;}Failed(ex??new OperationCanceledException("特殊移動を中断しました。"));return;}
                    if(_girl.Root!=null)_girl.Root.position=landing;NextLeg();
                });
            }
            catch(Exception ex){Failed(ex);}
        }
        private void Failed(Exception ex){try{Cancel();}finally{_error(ex);}}
        private void Finish()
        {
            _busy=false;_playerMoving=false;_girl.SpecialMoving=false;_girl.SpecialAnimationPlaying=false;_route=null;Unreserve();
            var release=_releaseActor;_releaseActor=null;release?.Invoke();
        }
        public void Cancel()
        {
            if(!_busy||_cancelling)return;
            _cancelling=true;
            try
            {
            _route=null;
            var playback=_playback;_playback=null;
            if(playback!=null){playback.Cancel();return;}
            if(_playerMoving)
            {
                _restorePlayer?.Invoke();_restorePlayer=null;_host.Session.ExternalInputBlocked=_wasBlocked;_view.enabled=true;_view.SetCursorReleased(false);
            }
            else _girl.StopSpecialSegment();
            Finish();
            }
            finally{_cancelling=false;}
        }
        public void Dispose()
        {
            if(_disposed)return;Cancel();_disposed=true;
            _girl.SpecialRoute=null;_girl.SpecialWaypoint=null;_girl.ConversationInvalidated-=Cancel;_world.Rebuilding-=Cancel;_host.Session.ParametersApplied-=Changed;
            _world.Rebuilt-=InvalidateGraph;_host.Island.Doors.PassageChanged-=InvalidateGraph;
            _host.Session.SpecialMovementSnapshot=null;
            _bundle.Unload(true);
        }
    }
}
