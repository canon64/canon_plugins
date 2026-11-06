using System;
using System.Linq;
using CanonGate;
using HarmonyLib;
using MapEnvironmentRuntime;
using UnityEngine;

namespace MainGameAiShoujoMode.Authoring
{
    // Owns the prepared scene through the Preview -> native H permission boundary.
    internal sealed class AuthoringFreeHBridge : IDisposable
    {
        private const string Guid = Plugin.Guid + ".event-h";
        private readonly EventAuthoringEditor owner;
        private readonly Action ready, ended;
        private readonly Action<Exception> failed;
        private IDisposable registration;
        private GateApi.Permit permit;
        private GateApi.HotelModeScope scope;
        private AiModeHSession session;
        private bool disposing;
        internal AuthoringStage Stage { get; private set; }
        internal AuthoringProject Project { get; private set; }
        internal string EventId, NodeId;
        internal bool Preview;
        internal bool HoldingStage => Stage != null;
        internal bool Ready => session?.RoomReady == true && permit.IsValid;
        internal AuthoringFreeHBridge(EventAuthoringEditor owner, Action ready, Action ended, Action<Exception> failed)
        {
            this.owner = owner; this.ready = ready; this.ended = ended; this.failed = failed;
            registration = GateApi.Register(Guid, () => permit = GateApi.Capture(Guid), () => { permit = default(GateApi.Permit); if (session?.Busy == true && session.RoomReady && !disposing) session.Cancel(); });
        }
        internal void Begin(AuthoringStage stage, AuthoringProject project, AuthoringEvent item, string node, bool preview, Parameters settings)
        {
            if (HoldingStage) throw new InvalidOperationException("フリーHの終了を待ってください。");
            var female = stage.Actor(item.freeH.femaleId);
            var male = string.IsNullOrEmpty(item.freeH.maleId) ? null : stage.Actor(item.freeH.maleId);
            if (female.chaFile.parameter.sex != settings.I("math.one") || male != null && male.chaFile.parameter.sex != settings.I("math.zero")) throw new InvalidOperationException("フリーHの女キャラ・男キャラの割当てを確認してください。");
            if (stage.MapRoot == null) throw new InvalidOperationException("フリーHで使うマップが読み込まれていません。");
            Stage = stage; Project = AuthoringJson.Copy(project); EventId = item.id; NodeId = node; Preview = preview;
            try
            {
                var heroine = new SaveData.Heroine(HotelRoomAppearanceRuntime.CloneCard(female.chaFile), false); heroine.SetRoot(female.gameObject);
                SaveData.Player player = null;
                if (male != null) { player = new SaveData.Player(HotelRoomAppearanceRuntime.CloneCard(male.chaFile), false); player.SetRoot(male.gameObject); }
                scope = GateApi.EnterHotelMode(owner, "event:" + item.id);
                session = new AiModeHSession { AllRoomSelections = true };
                session.Initialize(owner, settings, () => { }, Finished, null, ex => { if (!(ex.GetBaseException() is OperationCanceledException)) failed(ex); }, NativeReady);
                session.RoomSelectionChanged += Reapply;
                female.visibleAll = false; if (male != null) male.visibleAll = false; if (stage.Camera != null) stage.Camera.enabled = false;
                session.Begin(heroine, player, stage.MapRoot, new AiModeHSession.HLocation { SourceId = "event:" + item.id,
                    Position = AuthoringStage.Vector(item.freeH.position), Rotation = Quaternion.Euler(AuthoringStage.Vector(item.freeH.rotation)),
                    Categories = new[] { settings.I("hSession.category") }, VerifiedGround = true });
            }
            catch { DisposeSession(); throw; }
        }
        private void NativeReady()
        {
            if (!Ready || Stage == null) throw new InvalidOperationException("フリーHの許可または登場人物の読込を確認できません。");
            var item = Project.events.events.Single(e => e.id == EventId);
            Stage.AdoptNative(session.ActiveCamera, item.freeH.femaleId, item.freeH.maleId, session.RoomActors());
            Stage.CameraPoseApplied = camera =>
            {
                if (!Ready) return; var control = session.RoomFlags.ctrlCamera; var parent = control.transform.parent;
                var position = parent == null ? camera.transform.position : parent.InverseTransformPoint(camera.transform.position);
                var rotation = parent == null ? camera.transform.rotation : Quaternion.Inverse(parent.rotation) * camera.transform.rotation;
                control.SetCamera(position, rotation.eulerAngles, rotation, control.CameraDir); control.CameraFov = camera.fieldOfView;
            };
            Reapply(); ready();
        }
        private void Reapply() { if (Ready && Stage != null) Stage.Place(Project.events.events.Single(e => e.id == EventId).placements); }
        internal HotelRoomSelection[] Animations() => Ready ? session.AuthoringAnimations() : Array.Empty<HotelRoomSelection>();
        internal void Animate(HAnimationCue cue)
        {
            if (!Ready) throw new InvalidOperationException("体位を変えるにはフリーHの開始完了を待ってください。");
            if (!session.RestoreRoomSelection(new HotelRoomSelection { mode = cue.mode, id = cue.id, name = cue.name, bundle = cue.bundle, asset = cue.asset })) throw new InvalidOperationException("指定したフリーHアニメが現在の参加者で見つかりません：" + cue.name);
        }
        internal bool RequiresRestart(AuthoringEvent item)
        {
            if (!Ready) return false; var previous = Project.events.events.Single(e => e.id == EventId);
            return item.sceneMode != previous.sceneMode || item.mapId != previous.mapId || AuthoringJson.Write(item.freeH) != AuthoringJson.Write(previous.freeH);
        }
        internal void UpdateProject(AuthoringProject source) { Project = AuthoringJson.Copy(source); }
        internal void Cancel() { if (session?.Busy == true) session.Cancel(); else Finished(); }
        private void Finished()
        {
            if (!HoldingStage) return;
            scope?.Dispose(); scope = null; var stage = Stage; Stage = null; stage.Dispose();
            var native = session; session = null; native?.Dispose();
            if (!disposing) ended();
        }
        private void DisposeSession()
        {
            var native = session; session = null; native?.Dispose(); Finished(); scope?.Dispose(); scope = null;
        }
        public void Dispose()
        {
            if (disposing) return; disposing = true; DisposeSession(); registration?.Dispose(); registration = null;
        }
    }
}

namespace MainGameAiShoujoMode
{
    public sealed partial class AiModeHSession
    {
        internal HotelRoomSelection[] AuthoringAnimations()
        {
            if (!RoomReady || _proc == null) return Array.Empty<HotelRoomSelection>();
            var lists = (System.Collections.Generic.List<HSceneProc.AnimationListInfo>[])AccessTools.Field(typeof(HSceneProc), "lstUseAnimInfo").GetValue(_proc);
            return lists.Where(l => l != null).SelectMany(l => l).Where(i => i != null).Distinct().Select(Describe).ToArray();
        }
    }
}
