using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MapEnvironmentRuntime;
using UnityEngine;

namespace MainGameAiShoujoMode
{
    public sealed partial class AiAnimationTrial : MonoBehaviour,IAnimationApplicationControls
    {
        public sealed class Mapping { public string source,target,path; }
        public sealed class Clip { public string name,source; }
        public sealed class Report { public string controller;public Mapping[] mapping;public Clip[] clips;public bool nativeSamplingPassed,kksVerified; }
        private SandboxHost _host;
        private NavigationWorld _navigation;
        private AiGirlAutonomy _autonomy;
        private PodCharacterAnimation _appearance;
        private string _content;
        internal string ContentDirectory => _content;
        internal bool PodSequenceActive { get; private set; }
        internal bool LifeMotionOccupied { get; private set; }
        internal void CompletePodSequence(Vector3 recovery, Quaternion rotation)
        {
            if (!PodSequenceActive || _stage == null || _actor == null) throw new InvalidOperationException("出現中の女の子がいません。");
            if (_appearance?.Playing == true) throw new InvalidOperationException("出現アニメがまだ終了していません。");
            if (!_navigation.TryProject(recovery, out var floor)) throw new InvalidOperationException("カプセル出口に歩ける場所がありません。");
            _stage.transform.SetPositionAndRotation(floor, rotation);
            // Persist the capsule exit before handing movement to the resident's saved life state.
            LifeDestinationReached?.Invoke();
            PodSequenceActive = false;
            LifeActionReady?.Invoke();
        }
        internal void CancelPodSequence() { PodSequenceActive = false; }
        public void BeginPodAppearance(Action completed, Action<Exception> failed)
        {
            try
            {
                if (_actor?.animBody == null || _stage == null) throw new InvalidOperationException("女の子を読み込んでください。");
                if (_appearance?.Playing == true) throw new InvalidOperationException("出現アニメを再生中です。");
                ConversationInvalidated?.Invoke();
                _autonomy?.Stop();
                _appearance = _actor.animBody.gameObject.GetComponent<PodCharacterAnimation>() ?? _actor.animBody.gameObject.AddComponent<PodCharacterAnimation>();
                _state = "カプセル出現";
                _appearance.Begin(_actor.animBody, _stage.transform, _content, P,
                    () => { _state = "Idle"; Record(); completed?.Invoke(); if (!PodSequenceActive) LifeActionReady?.Invoke(); },
                    ex => { Failure(ex); failed?.Invoke(ex); });
                Record();
            }
            catch (Exception ex) { Failure(ex); failed?.Invoke(ex); }
        }
        public event Action ConversationInvalidated;
        public event Action<ChaControl> ActorChanged;
        internal event Action LifeActionReady, LifeDestinationReached;
        public ChaControl CurrentActor => _actor;
        public string ConversationIdentityKey { get; private set; }
        public Transform Root=>_stage==null?null:_stage.transform;
        public bool SpecialMoving { get; set; }
        public bool SpecialAnimationPlaying { get; set; }
        public Func<Vector3,bool> SpecialRoute { get; set; }
        public Func<Vector3,System.Collections.Generic.ISet<string>,System.Random,NavigationWaypoint> SpecialWaypoint { get; set; }
        public void WalkSpecialSegment(Vector3 target,Action completed,Action<Exception> failed)=>_autonomy.WalkSegment(target,completed,failed);
        public void StopSpecialSegment()=>_autonomy?.Stop();
        internal void SuspendSpecialAnimation() => _autonomy?.SuspendSpecialAnimation();
        public SaveData.Heroine ExportHParticipant()
        {
            if (ConversationTarget == null || _actor?.chaFile == null) throw new InvalidOperationException("Hへ渡せる女の子がいません。");
            if (!_actor.AssignCoordinate((ChaFileDefine.CoordinateType)_actor.fileStatus.coordinateType))
                throw new InvalidOperationException("現在のコーディネートをHへ引き継げません。");
            var heroine = new SaveData.Heroine(_actor.chaFile, false);
            heroine.SetRoot(_actor.gameObject);
            return heroine;
        }
        public Action HideForH()
        {
            if (_actor == null || _stage == null) throw new InvalidOperationException("女の子が読み込まれていません。");
            var actor = _actor; var stage = _stage;
            bool active = stage.activeSelf, visible = actor.visibleAll, restored = false;
            stage.SetActive(false);
            return () =>
            {
                if (restored) return; restored = true;
                if (actor == null || stage == null || !ReferenceEquals(actor, _actor) || !ReferenceEquals(stage, _stage)) return;
                actor.visibleAll = visible; stage.SetActive(active);
            };
        }
        public Transform ConversationTarget => _actor != null && !PodSequenceActive && !LifeMotionOccupied && _appearance?.Playing != true && !SpecialMoving ? _stage?.transform : null;
        public string ConversationName => _actor?.chaFile?.parameter?.fullname ?? "女の子";
        public Action PauseForConversation(Transform partner)
        {
            if (ConversationTarget == null) throw new InvalidOperationException("会話できる女の子がいません。");
            var actor = _actor; var autonomy = _autonomy;
            var state = autonomy?.PauseForConversation();
            Play("Idle");
            var facing = Vector3.ProjectOnPlane(partner.position - _stage.transform.position, Vector3.up);
            if (facing.sqrMagnitude > P.N("render.epsilon")) _stage.transform.rotation = Quaternion.LookRotation(facing, Vector3.up);
            return () => { if (actor != null && ReferenceEquals(actor, _actor) && autonomy != null) autonomy.ResumeAfterConversation(state); };
        }
        public Action AdditionalControls { get; set; }
        internal void ApplyLifeAction(string action, Transform player)
        {
            if (_autonomy == null || PodSequenceActive || SpecialMoving || _appearance?.Playing == true) throw new InvalidOperationException("女の子の移動を開始できない状態です。");
            switch (action)
            {
                case "normal": _autonomy.Patrol(); break;
                case "follow": _autonomy.Follow(player); break;
                default: _autonomy.Stop(); break;
            }
            if ((action == "normal" || action == "follow") && _autonomy.Error != null) throw new InvalidOperationException(_autonomy.Error);
        }
        public Action PlayerControls { get; set; }
        public Action PauseForExternalMotion()
        {
            if (ConversationTarget == null) throw new InvalidOperationException("女の子は出現動作中、または未読込です。");
            var actor = _actor; var autonomy = _autonomy;
            var state = autonomy?.PauseForConversation();
            var release = new ExternalMotionRelease(() => actor != null && ReferenceEquals(actor, _actor),
                () => LifeMotionOccupied = false, () => autonomy?.ResumeAfterConversation(state), () => LifeActionReady?.Invoke());
            LifeMotionOccupied = true;
            try { LifeActionReady?.Invoke(); }
            catch { release.Release(); throw; }
            return release.Release;
        }
        public void DrawCapsuleControls() => AdditionalControls?.Invoke();
        public void DrawQuickControls()
        {
            PlayerControls?.Invoke();
            GUILayout.Label("女の子：" + (_actor == null ? "未読込" : _card) + "　" + (_autonomy?.State ?? ""));
            bool previous = GUI.enabled; GUI.enabled = previous && _actor != null && _appearance?.Playing != true;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("巡回を開始")) _autonomy?.Patrol();
            if (GUILayout.Button("ここへ呼ぶ")) _autonomy?.Call(_host.Walker?.ViewCamera?.transform);
            if (GUILayout.Button("待機")) _autonomy?.Stop();
            GUILayout.EndHorizontal(); GUI.enabled = previous;
        }
        public bool SpawnAt(string cardPath, Vector3 point, Quaternion rotation) => LoadCharacter(cardPath, point, rotation);
        internal void RestoreMoment(AiMomentSave.Actor saved)
        {
            var pose = saved.pose;
            var file = HotelRoomAppearanceRuntime.Decode(saved.card);
            if (!LoadCharacter(saved.source, new Vector3(pose.x, pose.y, pose.z), new Quaternion(pose.qx, pose.qy, pose.qz, pose.qw),
                identityKey: saved.identity, savedCard: file)) throw new InvalidDataException("セーブした女の子を復元できません：" + _error);
            int layer = P.I("math.zero");
            if (!_actor.animBody.HasState(layer, saved.animation)) throw new InvalidDataException("保存したアニメーションが見つかりません。");
            _actor.animBody.Play(saved.animation, layer, saved.animationTime); _actor.animBody.speed = saved.animationSpeed;
            _actor.animBody.Update(P.N("math.zero"));
            HotelRoomAppearanceRuntime.RestoreCardStatus(_actor, saved.card);
        }
        public bool SpawnAt(string cardPath, string coordinatePath, Vector3 point, Quaternion rotation, string identityKey = null) => LoadCharacter(cardPath, point, rotation, coordinatePath, identityKey, true);
        public void SetNavigation(NavigationWorld world) { ConversationInvalidated?.Invoke(); _navigation=world; _autonomy?.SetNavigation(world); LifeActionReady?.Invoke(); }
        private Action<string> _log;
        private AssetBundle _bundle;
        private RuntimeAnimatorController _controller;
        private Report _report;
        private ChaControl _actor;
        private GameObject _stage;
        private string _state="未読込",_card="標準女性",_error;
        private string _movementSettings;
        private AiCardPicker _cardPicker;
        private bool _drawActor;
        private Parameters P=>_host.Session.Settings;
        public void Initialize(SandboxHost host,string content,Action<string> log)
        {
            _host=host;_log=log;_content=content;
            _report=JsonCodec.FromJson<Report>(File.ReadAllText(Path.Combine(content,"animation-application.json")));
            if(_report?.mapping==null||_report.clips==null||!_report.nativeSamplingPassed)throw new InvalidDataException("アニメ適用検証レポートが未完成です。");
            _bundle=AssetBundle.LoadFromFile(Path.Combine(content,P.T("animation.bundle")));
            if(_bundle==null)throw new InvalidDataException("検証アニメbundleを読めません。");
            _controller=_bundle.LoadAsset<RuntimeAnimatorController>(_report.controller);
            if(_controller==null||_controller.animationClips.Distinct().Count()!=_report.clips.Length)throw new InvalidDataException("検証用controller/clipが不足しています。");
            _cardPicker = new AiCardPicker(P, P.T("animation.card.directory"), Failure);
            _host.Session.AnimationTrial=this;_host.Session.ParametersApplied+=ApplySettings;
            _log("AI_ANIMATION_TRIAL_READY mappedBones="+_report.mapping.Length+" nativeSamplingPassed="+_report.nativeSamplingPassed+" kksVisualVerified=false");
        }
        private void ApplySettings(Parameters settings)
        {
            _cardPicker?.Configure(settings);
            if (_appearance?.Playing == true) return;
            string movement = string.Join("|", settings.numbers.Where(item => item.key.StartsWith("navigation.actor.", StringComparison.Ordinal)).Select(item => item.key + "=" + item.value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            if (movement != _movementSettings) { ConversationInvalidated?.Invoke(); _autonomy?.Configure(settings); _movementSettings = movement; }
            if(_actor?.animBody!=null)_actor.animBody.speed=_state=="停止"?settings.N("math.zero"):settings.N("animation.speed");
        }
        public void DrawControls()
        {
            PlayerControls?.Invoke();
            if(Event.current.type==EventType.Layout)
            {
                _drawActor=_actor!=null;
            }
            GUILayout.Label("女の子・AI少女アニメ・自律歩行");
            GUILayout.Label("状態："+_state+" / "+_card+" / 対応骨 "+_report.mapping.Length+"。見た目は実機確認が必要です。");
            if (GUILayout.Button("目の前でカプセル出現アニメを試す"))
            {
                if (_actor != null || LoadCharacter(null)) BeginPodAppearance(null, null);
            }
            if(GUILayout.Button("標準女性を目の前に読み込む"))LoadCharacter(null);
            GUILayout.Label("女性カードの画像をクリックすると、選択を保存して目の前へ読み込みます。");
            _cardPicker.Draw(P.T("animation.card.path"), SelectCard);
            if(_drawActor)
            {
                GUILayout.BeginHorizontal();if(GUILayout.Button("AI待機")){_autonomy?.Stop();Play("Idle");}if(GUILayout.Button("AI歩行（その場）")){_autonomy?.Stop();Play("Walk");}if(GUILayout.Button("停止")&&_actor?.animBody!=null){_autonomy?.Stop();_actor.animBody.speed=P.N("math.zero");_state="停止";Record();}GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if(GUILayout.Button("自律巡回を開始"))_autonomy?.Patrol();
                if(GUILayout.Button("巡回を停止"))_autonomy?.Stop();
                if(GUILayout.Button("ここへ呼ぶ"))_autonomy?.Call(_host.Walker?.ViewCamera?.transform);
                if(GUILayout.Button("解散して巡回へ"))_autonomy?.Patrol();
                GUILayout.EndHorizontal();
                GUILayout.Label("歩行状態："+(_autonomy?.State??"未準備")+" / "+(_autonomy?.Error??""));
                if(GUILayout.Button("検証キャラを目の前へ"))TryPlace();
                if(GUILayout.Button("検証キャラを削除")){ReleaseActor();_state="未読込";Record();}
            }
            GUILayout.Label(_error??"アニメの動きはUnity標準Animatorが再生します。移動はUnity標準NavMeshAgent、到着は専用トリガーで通知します。ドア開閉とはしごは未接続です。");
        }
        private void SelectCard(string path)
        {
            try
            {
                var card = new ChaFileControl();
                if (!card.LoadCharaFile(path) || card.parameter.sex != P.I("math.one")) throw new InvalidDataException("KKSの女性カードではありません。");
                var next = P.Copy(); next.text.Single(item => item.key == "animation.card.path").value = path;
                next.revision = Guid.NewGuid().ToString("N"); _host.Session.Apply(next, true); LoadCharacter(path);
            }
            catch (Exception ex) { Failure(ex); }
        }
        private bool LoadCharacter(string path, Vector3? position = null, Quaternion? rotation = null, string coordinatePath = null, string identityKey = null, bool podSequence = false, ChaFileControl savedCard = null)
        {
            try
            {
                ChaFileControl file=savedCard;
                if(savedCard==null && path!=null){file=new ChaFileControl();if(!file.LoadCharaFile(path)||file.parameter.sex!=P.I("math.one"))throw new InvalidDataException("女性キャラカードではありません。");}
                if (!string.IsNullOrWhiteSpace(coordinatePath))
                {
                    var coordinate = new ChaFileCoordinate();
                    if (!coordinate.LoadFile(coordinatePath)) throw new InvalidDataException("初期コーディネートカードを読めません。");
                }
                ReleaseActor();PodSequenceActive=podSequence;_stage=new GameObject("AI Animation Trial Character");_stage.transform.SetParent(transform,false);
                BeginBodyTrace(file, path, coordinatePath);
                int id=FindObjectsOfType<ChaControl>().Select(actor=>actor.chaID).DefaultIfEmpty(P.I("math.zero")).Max()+P.I("math.one");
                _actor=Manager.Character.CreateFemale(_stage,id,file,true);
                if(_actor==null)throw new InvalidOperationException("標準キャラ生成に失敗しました。");
                _actor.Load();
                MarkBodyTrace("model_loaded");
                if (!string.IsNullOrWhiteSpace(coordinatePath))
                {
                    if (!_actor.nowCoordinate.LoadFile(coordinatePath)) throw new InvalidDataException("初期コーディネートをキャラへ読み込めません。");
                    _actor.Reload(noChangeClothes:false,noChangeHead:true,noChangeHair:true,noChangeBody:true);
                    MarkBodyTrace("coordinate_loaded");
                }
                if(!_actor.loadEnd||_actor.objAnim==null||_actor.animBody==null)throw new InvalidOperationException("キャラのモデル読込が完了していません。");
                var missing=_report.mapping.Where(pair=>_actor.objAnim.transform.Find(pair.path)==null).Select(pair=>pair.target).ToArray();
                if(missing.Any())throw new InvalidDataException("対応先の骨が不足："+string.Join(", ",missing));
                _actor.animBody.runtimeAnimatorController=_controller;_actor.animBody.applyRootMotion=false;_actor.animBody.fireEvents=false;_actor.animBody.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                _actor.animBody.Rebind();_actor.SetActiveTop(true);_actor.visibleAll=true;_card=path??"標準女性";
                MarkBodyTrace("controller_rebound");
                if (position.HasValue) _stage.transform.SetPositionAndRotation(position.Value, rotation.Value); else Place();
                Play("Idle");
                MarkBodyTrace("idle_applied"); CaptureBodyAfterStart();
                _autonomy=_stage.AddComponent<AiGirlAutonomy>();
                _autonomy.Initialize(P,_navigation,state => { if (_appearance?.Playing != true && !SpecialAnimationPlaying) Play(state); },_log);
                _autonomy.DestinationReached += () => LifeDestinationReached?.Invoke();
                _autonomy.SpecialRoute=target=>SpecialRoute?.Invoke(target)==true;
                _autonomy.SpecialWaypoint=(position,reservations,random)=>SpecialWaypoint?.Invoke(position,reservations,random);
                _error=null;
                ConversationIdentityKey = identityKey ?? (string.IsNullOrWhiteSpace(path) ? "preview:default" : "card:" + Path.GetFullPath(path).ToUpperInvariant());
                ActorChanged?.Invoke(_actor);
                return true;
            }
            catch(Exception ex){ReleaseActor();Failure(ex);return false;}
        }
        private void Play(string state)
        {
            try
            {
                if(_actor?.animBody==null)throw new InvalidOperationException("検証キャラが未準備です。");
                var animator=_actor.animBody;int layer=P.I("math.zero");string name=animator.GetLayerName(layer)+"."+state;
                if(!animator.HasState(layer,Animator.StringToHash(name)))throw new InvalidDataException("検証用stateが見つかりません："+name);
                animator.speed=P.N("animation.speed");animator.Play(name,layer,P.N("math.zero"));animator.Update(P.N("math.zero"));_state=state;_error=null;Record();
            }
            catch(Exception ex){Failure(ex);}
        }
        private void TryPlace(){try{ConversationInvalidated?.Invoke();_autonomy?.Stop();Place();Record();}catch(Exception ex){Failure(ex);}}
        private void Place()
        {
            var camera=_host.Walker?.ViewCamera;if(camera==null)throw new InvalidOperationException("カメラがありません。");
            var direction=Vector3.ProjectOnPlane(camera.transform.forward,Vector3.up).normalized;if(direction.sqrMagnitude<=P.N("render.epsilon"))direction=Vector3.forward;
            var point=camera.transform.position+direction*P.N("animation.distance");
            var hits=Physics.RaycastAll(point+Vector3.up*P.N("character.groundProbeHeight"),Vector3.down,P.N("character.groundProbeDistance"),Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore).Where(hit=>!hit.transform.IsChildOf(_stage.transform)).OrderBy(hit=>hit.distance).ToArray();
            if(!hits.Any())throw new InvalidOperationException("目の前に配置できる地面がありません。");
            point.y=hits.First().point.y+P.N("animation.groundOffset");_stage.transform.SetPositionAndRotation(point,Quaternion.LookRotation(-direction,Vector3.up));
        }
        public AnimationApplicationStatus Snapshot()=>new AnimationApplicationStatus{state=_state,card=_card,sourceClip=_report?.clips?.FirstOrDefault(clip=>clip.name==_state)?.source,controller=_report?.controller,report="animation-application.json",mappedBones=_report?.mapping?.Length??default(int),characterReady=_actor!=null,nativeSamplingPassed=_report!=null&&_report.nativeSamplingPassed,kksVisualVerified=false,rootMotion=false,error=_error,autonomyState=_autonomy?.State,autonomyError=_autonomy?.Error,destinationId=_autonomy?.DestinationId};
        private void Record()
        {
            string json=JsonCodec.ToJson(Snapshot(),true);Parameters.AtomicWrite(Path.Combine(P.T("share.directory"),"animation-application-runtime.json"),json);_log("AI_ANIMATION_TRIAL_STATE "+json);
        }
        private void Failure(Exception ex){_error=ex.GetBaseException().Message;_log("AI_ANIMATION_TRIAL_ERROR "+ex);}
        private void ReleaseActor()
        {
            EndBodyTrace();
            ConversationInvalidated?.Invoke();
            if (_actor != null) ActorChanged?.Invoke(null);
            if (_appearance != null) { _appearance.Cancel(); Destroy(_appearance); _appearance = null; }
            if(_autonomy!=null){_autonomy.Release();Destroy(_autonomy);_autonomy=null;}
            if(_actor!=null){_actor.StopAllCoroutines();Manager.Character.DeleteChara(_actor);_actor=null;}
            if(_stage!=null){Destroy(_stage);_stage=null;}
            PodSequenceActive = false;
            LifeMotionOccupied = false;
        }
        private void OnDestroy()
        {
            _cardPicker?.Dispose(); _cardPicker = null;
            if(_host?.Session!=null){_host.Session.ParametersApplied-=ApplySettings;if(ReferenceEquals(_host.Session.AnimationTrial,this))_host.Session.AnimationTrial=null;}
            ReleaseActor();if(_bundle!=null){_bundle.Unload(true);_bundle=null;}
        }
    }
}
