using System;
using System.IO;
using System.Linq;
using System.Text;
using MainGameAiShoujoMode.Life;
using MapEnvironmentRuntime;
using UnityEngine;

namespace MainGameAiShoujoMode
{
    public sealed partial class AiModeSession
    {
        private AiMomentSaveStore _momentStore;
        private AiMomentSaveMenu _momentMenu;
        private AiMomentSave _resumeMoment;
        private bool _resumeClockPlaying;
        private bool _restartFromMoment;
        internal Action RestartMoment { get; set; }
        private string LoadMoment()
        {
            EnsureMomentAvailable();
            if (_momentStore.Read() == null) throw new InvalidOperationException("まだ保存地点がありません。先に「セーブ」を押してください。");
            _restartFromMoment = true; BeginReturn();
            return "保存地点から再開しています。";
        }
        private static AiMomentSave.Pose CapturePose(Transform transform, Vector3? position = null)
        {
            var p = position ?? transform.position; var q = transform.rotation;
            return new AiMomentSave.Pose { x = p.x, y = p.y, z = p.z, qx = q.x, qy = q.y, qz = q.z, qw = q.w };
        }
        private static Vector3 Position(AiMomentSave.Pose pose) => new Vector3(pose.x, pose.y, pose.z);
        private static Quaternion Rotation(AiMomentSave.Pose pose) => new Quaternion(pose.qx, pose.qy, pose.qz, pose.qw);
        private void InitializeMomentSave(string content)
        {
            var lifePath = AiLifeRuntimeSettings.LoadContent(content).savePath;
            _momentStore = new AiMomentSaveStore(Path.Combine(Path.GetDirectoryName(lifePath), "moment-save.json"));
            _resumeMoment = _momentStore.Read();
            if (_resumeMoment == null) return;
            var restoredSettings = MomentParameterCompatibility.Restore(_resumeMoment.parameters, _settings);
            // All later resume stages read this upgraded in-memory document. Never rewrite the checkpoint.
            _resumeMoment.parameters = restoredSettings.Json();
            _settings = restoredSettings;
            if (_resumeMoment.playerCard != null && HotelRoomAppearanceRuntime.Decode(_resumeMoment.playerCard).parameter.sex != _settings.I("math.zero") && HotelRoomAppearanceRuntime.Decode(_resumeMoment.playerCard).parameter.sex != _settings.I("math.one"))
                throw new InvalidDataException("保存した主人公のカード種別が不正です。");
            if (_resumeMoment.girl != null && HotelRoomAppearanceRuntime.Decode(_resumeMoment.girl.card).parameter.sex != _settings.I("math.one"))
                throw new InvalidDataException("保存した女の子のカード種別が不正です。");
            _momentStore.PrepareWorkingCopy(_resumeMoment);
        }
        private void RestoreMomentEnvironment()
        {
            if (_resumeMoment == null) return;
            var next = JsonCodec.FromJson<Parameters>(_resumeMoment.parameters);
            _host.Session.Apply(next, false); _settings = _host.Session.Settings;
            _resumeClockPlaying = _host.Session.World.Playing;
            _host.Session.World.Playing = false;
            _host.Session.World.SetDate(next.I("calendar.year"), next.I("calendar.month"), next.I("calendar.day"), next.N("clock.hours"));
            _host.Island.Doors.RestoreMoment(_resumeMoment.doors.Select(d => new SandboxIslandDoors.SavedDoor { path = d.path, open = d.open, right = d.right }).ToArray());
        }
        private void RestoreMomentActors()
        {
            if (_resumeMoment == null) return;
            MainGameAiShoujoH.Plugin.ImportResidentProfiles(_resumeMoment.dedicatedHProfiles);
            _pods.RestoreMoment(_resumeMoment.pods);
            if (_resumeMoment.playerCard != null) HotelRoomAppearanceRuntime.RestoreCardStatus(_playerAvatar.Actor, _resumeMoment.playerCard);
            if (_resumeMoment.girl != null)
            {
                _animationTrial.RestoreMoment(_resumeMoment.girl);
                if (_conversation.Resident == null) throw new InvalidDataException("保存した女の子の生活状態を復元できません。");
            }
            if (_resumeMoment.pickup != null) _pickups.RestoreMoment(Position(_resumeMoment.pickup), Rotation(_resumeMoment.pickup));
            var p = _resumeMoment.player; var v = _resumeMoment.view;
            _host.Walker.RestoreSavedPose(Position(p), Rotation(p), Position(v), Rotation(v), _resumeMoment.fieldOfView, _resumeMoment.crouching);
        }
        private void CompleteMomentResume()
        {
            if (_resumeMoment == null) return;
            var next = JsonCodec.FromJson<Parameters>(_resumeMoment.parameters);
            _host.Session.World.SetDate(next.I("calendar.year"), next.I("calendar.month"), next.I("calendar.day"), next.N("clock.hours"));
            RestoreMomentActors();
            _host.Session.World.Playing = _resumeClockPlaying;
            _resumeMoment = null;
        }
        private void EnsureMomentAvailable()
        {
            if (!Plugin.ModeAllowed || _loading || _closing || _returnRequested || _host?.Ready != true || _gateScope == null
                || !ReferenceEquals(CanonGate.GateApi.AiShoujoMode, _gateScope))
                throw new InvalidOperationException("AI少女モードの開始・終了処理中はセーブできません。");
            if (_conversation?.Active == true || _hSession?.Busy == true || MainGameAiShoujoH.Plugin.Busy || _specialMovement?.PlayerMoving == true
                || _pods?.Busy == true || _animationTrial.PodSequenceActive || _animationTrial.LifeMotionOccupied
                || _furniture?.Busy == true || _authoring?.BlocksMomentSave == true || MainGameVMDPlay.Plugin.ReadPlaybackBarSnapshot()?.Owns == true)
                throw new InvalidOperationException("会話・H・出現・特殊動作・イベント・VMDを終えてからセーブしてください。");
        }
        private string SaveMoment()
        {
            EnsureMomentAvailable();
            var doors = _host.Island.Doors.CaptureMoment();
            _lifeActor?.SynchronizeClock();
            var life = _life.Store.Read();
            var actor = _animationTrial.CurrentActor;
            AiMomentSave.Actor girl = null;
            if (actor != null)
            {
                var resident = _conversation.Resident ?? throw new InvalidOperationException("女の子の状態管理が準備できていません。");
                var state = life.residents.Single(r => r.id == resident.ResidentId);
                var root = _animationTrial.Root;
                state.hasPosition = true; state.x = root.position.x; state.y = root.position.y; state.z = root.position.z; state.yaw = root.eulerAngles.y;
                var animation = actor.animBody.GetCurrentAnimatorStateInfo(_settings.I("math.zero"));
                girl = new AiMomentSave.Actor { card = HotelRoomAppearanceRuntime.Encode(actor), identity = _animationTrial.ConversationIdentityKey,
                    source = _animationTrial.Snapshot().card, pose = CapturePose(root), animation = animation.fullPathHash,
                    animationTime = animation.normalizedTime, animationSpeed = actor.animBody.speed };
            }
            var next = _host.Session.Settings.Copy(); var world = _host.Session.World;
            next.numbers.Single(n => n.key == "calendar.year").value = world.Year;
            next.numbers.Single(n => n.key == "calendar.month").value = world.Month;
            next.numbers.Single(n => n.key == "calendar.day").value = world.Day;
            next.numbers.Single(n => n.key == "clock.hours").value = world.Hour;
            next.text.Single(n => n.key == "clock.playing").value = (_lighting?.ClockPlayingForSave ?? world.Playing).ToString();
            next.Validate();
            var camera = _host.Walker.ViewCamera;
            var save = new AiMomentSave { savedUtc = DateTimeOffset.UtcNow.ToString("O"), world = "ai-island", parameters = next.Json(),
                life = Encoding.UTF8.GetString(AiMomentSaveStore.Encode(life)), conversation = _conversation.ExportMoment(),
                dedicatedHProfiles = MainGameAiShoujoH.Plugin.ExportResidentProfiles(life.residents.Select(r => r.id).Concat(new[] { MainGameAiShoujoH.Plugin.PlayerProfileIdentity(_life.PlayerInventoryKey) }).ToArray(), _momentStore.Read()?.dedicatedHProfiles),
                player = CapturePose(_host.Walker.transform, _host.Walker.transform.position + _host.Walker.FeetLocalOffset), view = CapturePose(camera.transform),
                fieldOfView = camera.fieldOfView, crouching = _host.Walker.Crouching, playerCard = HotelRoomAppearanceRuntime.Encode(_playerAvatar.Actor),
                girl = girl, pickup = _pickups.MomentMarker == null ? null : CapturePose(_pickups.MomentMarker),
                doors = doors.Select(d => new AiMomentSave.Door { path = d.path, open = d.open, right = d.right }).ToArray(), pods = _pods.CaptureMoment(),furniture=_furniture?.Snapshot() };
            _momentStore.Write(save);
            return "今の状態をセーブしました。\n次回、AI少女モードへ入るとこの地点から再開します。\n日時・位置・キャラ・生活・会話・所持品・ドアの状態を保存しています。";
        }
    }
}
