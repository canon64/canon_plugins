using System;
using System.Collections.Generic;
using System.Linq;
using MainGameAiShoujoH.Contracts;
using UnityEngine;
using DedicatedH = MainGameAiShoujoH.Plugin;

namespace MainGameAiShoujoMode
{
    public sealed partial class AiModeSession
    {
        private string _dedicatedAdditionalResident;
        private void ShowDedicatedHParticipants()
        {
            if (!_conversation.Active) { BeginDedicatedH(); return; }
            var residents = _life.Store.Read().residents.Where(r => r.id != _conversation.Resident.ResidentId)
                .Select(r => new KeyValuePair<string, string>(r.id, System.IO.Path.GetFileNameWithoutExtension(r.origin.StartsWith("card:", StringComparison.Ordinal) ? r.origin.Substring("card:".Length) : r.origin))).ToArray();
            bool femalePlayer = _playerAvatar.Actor.sex == _settings.I("math.one");
            _conversation.ChooseDedicatedHParticipants(femalePlayer ? "女性（第二女性）" : "男性", femalePlayer ? Array.Empty<KeyValuePair<string, string>>() : residents,
                femalePlayer ? null : _dedicatedAdditionalResident, selected => {
                    _dedicatedAdditionalResident = selected;
                    BeginDedicatedH(residentIds: selected == null ? null : new[] { selected });
                });
        }
        public SessionSnapshot ReadDedicatedHState() => DedicatedH.ReadState();
        public object ReadDedicatedHEnvironment()
        {
            if (_host?.Session?.World == null || _host.Island?.Root == null || _host.Walker?.ViewCamera == null) return new { ready = false };
            var world = _host.Session.World; var camera = _host.Walker.ViewCamera; var light = _lighting?.DedicatedControlLight;
            return new { ready = _host.Ready, island = _host.Island.Root.GetInstanceID(), camera = camera.GetInstanceID(),
                cameraEnabled = camera.enabled, cameraActive = camera.gameObject.activeInHierarchy,
                cameraPosition = new[] { camera.transform.position.x, camera.transform.position.y, camera.transform.position.z }, cameraRotation = new[] { camera.transform.rotation.x, camera.transform.rotation.y, camera.transform.rotation.z, camera.transform.rotation.w }, camera.fieldOfView,
                listeners = camera.GetComponentsInChildren<AudioListener>(true).Select(a => new { id = a.GetInstanceID(), a.enabled, active = a.gameObject.activeInHierarchy }).ToArray(),
                light = light == null ? null : new { id = light.GetInstanceID(), light.enabled, light.intensity, color = new[] { light.color.r, light.color.g, light.color.b }, rotation = new[] { light.transform.localRotation.x, light.transform.localRotation.y, light.transform.localRotation.z, light.transform.localRotation.w } },
                ambient = new[] { RenderSettings.ambientSkyColor.r, RenderSettings.ambientSkyColor.g, RenderSettings.ambientSkyColor.b }, RenderSettings.ambientIntensity,
                primaryActor = _animationTrial.CurrentActor != null ? _animationTrial.CurrentActor.GetInstanceID() : default(int),
                playerActor = _playerAvatar.Actor != null ? _playerAvatar.Actor.GetInstanceID() : default(int),
                world.Year, world.Month, world.Day, world.Hour, world.Playing, world.Weather,
                inputBlocked = _host.Session.ExternalInputBlocked, walkerEnabled = _host.Walker.enabled };
        }
        public void StartDedicatedH() => BeginDedicatedH();
        public void StartDedicatedHWithMotion(string motionId, int eventId, int height) => BeginDedicatedH(motionId, eventId, height);
        public void StartDedicatedHWithResidents(string motionId, string[] residentIds, int eventId, int height) => BeginDedicatedH(motionId, eventId, height, residentIds);
        public void CommandDedicatedH(string name, string value, float amount) => DedicatedH.Execute(new Command { Name = name, Value = value, Amount = amount });
        public void EndDedicatedH(bool completed) => DedicatedH.End(completed ? EndReason.Completed : EndReason.Cancelled);
        public bool TradeDedicatedHMerchant(string operationId, int giveCategory, int giveItem, int giveCount, int receiveCategory, int receiveItem, int receiveCount)
        {
            if (_closing || _loading || DedicatedH.Busy || !_conversation.Active) throw new InvalidOperationException("H/ロード中または会話外では取引できません。");
            if (!ReferenceEquals(_gateScope, CanonGate.GateApi.AiShoujoMode) || !ReferenceEquals(_gateScope?.Owner, this)) throw new InvalidOperationException("現在のAIワールド操作権がありません。");
            if (!DedicatedH.ReadResidentProfile(_conversation.Resident.ResidentId).Merchant) throw new InvalidOperationException("この住人は専用商人プロフィールではありません。");
            return _life.TradeNativeMerchant(operationId, giveCategory, giveItem, giveCount, receiveCategory, receiveItem, receiveCount, _settings.I("dedicatedH.merchantSpendMaximum"));
        }
        public void OpenDedicatedHMerchant(int nativeArea)
        {
            if (_closing || _loading || DedicatedH.Busy || !_conversation.Active) throw new InvalidOperationException("取引を開ける会話状態ではありません。");
            if (!ReferenceEquals(_gateScope, CanonGate.GateApi.AiShoujoMode) || !ReferenceEquals(_gateScope?.Owner, this)) throw new InvalidOperationException("現在のAIワールド操作権がありません。");
            if (!DedicatedH.ReadResidentProfile(_conversation.Resident.ResidentId).Merchant) throw new InvalidOperationException("専用商人プロフィールが必要です。");
            var world = _host.Session.World;
            _life.OpenNativeMerchant(nativeArea, world.Year + ":" + world.Month + ":" + world.Day, UnityEngine.Random.Range);
        }
        public object ReadDedicatedHMerchant() => new { total = _life.Store.Read().nativeMerchantSpendTotal,
            stock = _life.Store.Read().inventories.Where(i => i.Key == _life.MerchantInventoryKey).SelectMany(i => i.items).Select(i => new { category = i.CategoryID, item = i.ID, count = i.Count }).ToArray() };
        private void BeginDedicatedH(string motionId = "0:0", int eventId = default(int), int height = default(int), string[] residentIds = null)
        {
            if (DedicatedH.Busy || _hSession?.Busy == true || _closing || _loading) throw new InvalidOperationException("AI専用Hは開始済みか、開始・終了の遷移中です。");
            var girl = _animationTrial.CurrentActor;
            var player = _playerAvatar.Actor;
            var resident = _conversation?.Resident;
            if (girl == null || player == null || resident == null) throw new InvalidOperationException("専用Hの参加キャラ・住人管理がありません。");
            var additional = new List<AiAnimationTrial.AdditionalResidentLease>();
            var ownedHItems = new HashSet<int>();
            int merchantSpendTotal = default(int);
            long vmdToken = default(long); var scope = _gateScope;
            DedicatedSceneControlLease sceneControls = null;
            StartRequest request = null;
            request = new StartRequest
            {
                Owner = this, Island = _host.Island.Root, Camera = _host.Walker.ViewCamera,
                NearbyHPoints = origin => AiModeHLocations.Nearby(_host, origin),
                Participants = new[] {
                    new Participant { Identity = resident.ResidentId, Role = "female", Character = girl },
                    new Participant { Identity = player.sex == _settings.I("math.one") ? DedicatedH.PlayerProfileIdentity(_life.PlayerInventoryKey) : "player", Role = player.sex == _settings.I("math.one") ? "female2" : "male", Character = player, IsPlayer = true }
                },
                MotionId = motionId, EventId = eventId, Height = height,
                SecondFemaleIsPlayer = player.sex == _settings.I("math.one"),
                ReadNativeParameter = ReadDedicatedHParameter,
                SuspendEndingMusic = () => _islandAudio.SuspendMusic(),
                ReadMerchantSpendTotal = () => merchantSpendTotal,
                HasNativeHItem = (mode, id) => MainGameAiShoujoH.Progression.NativeHItemRequirements.Check(mode, id, ownedHItems.Contains),
                Position = girl.transform.position, OriginalPlayerPosition = player.transform.position, SourceWorldScale = _settings.N("island.scale"), Rotation = girl.transform.rotation,
                Cover = done => _loadingScreen.Cover(done), Reveal = done => _loadingScreen.Reveal(() => { done(); if (_returnRequested && !_closing && !DedicatedH.Busy) BeginReturn(); }),
                AcquireControl = () =>
                {
                    vmdToken = MainGameVMDPlay.Plugin.SuspendAiHMotion(girl);
                    var femaleControl = _gateScope.AcquireActorMotion(girl) ?? throw new InvalidOperationException("AIキャラの動作操作権を取得できません。");
                    Action services = null, vmdFace = null;
                    try
                    {
                        services = _gateScope.SuspendActorActivities(DedicatedH.PluginGuid, girl, "com.kks.maingame.voicefaceeventbridge");
                        vmdFace = MainGameVMDPlay.Plugin.AcquireAiHFaceLease(girl);
                        sceneControls = new DedicatedSceneControlLease(_host.Walker.ViewCamera, _lighting?.DedicatedControlLight, () => ReferenceEquals(_gateScope, scope) && ReferenceEquals(scope.Actor, girl) && !_closing && DedicatedH.ReadState().Phase == SessionPhase.Ready);
                        request.SceneControls = sceneControls.Controls;
                        var playerControl = _playerAvatar.AcquireDedicatedH();
                        return () => { try { sceneControls.Dispose(); } finally { try { playerControl(); } finally { try { femaleControl(); } finally { try { vmdFace?.Invoke(); } finally { services?.Invoke(); } } } } };
                    }
                    catch { try { sceneControls?.Dispose(); } finally { try { femaleControl(); } finally { try { vmdFace?.Invoke(); } finally { services?.Invoke(); } } } throw; }
                },
                Results = new MainGameAiShoujoH.Results.ResultServices { Apply = ApplyDedicatedHResult },
                Started = DedicatedHStarted, Ended = DedicatedHEnded, Ready = () => {
                    sceneControls.FrameParticipants(request.Participants.Select(participant => participant.Character).ToArray(), _settings.N("math.two"), _settings.N("math.pi") / _settings.N("math.degreesHalfTurn"));
                    DedicatedHReady();
                }
            };
            request.AbortExternalControl = () => { if (vmdToken != default(long)) MainGameVMDPlay.Plugin.DiscardAstral(vmdToken); vmdToken = default(long); };
            request.RestoreExternalControl = (reason, done, failed) =>
            {
                if (vmdToken == default(long)) { done(); return; }
                if (!ReferenceEquals(_gateScope, scope) || !ReferenceEquals(scope.Actor, girl) || _closing || reason == EndReason.PermissionRevoked)
                { request.AbortExternalControl(); done(); return; }
                StartCoroutine(RestoreDedicatedVmd(vmdToken, () => { vmdToken = default(long); if (reason == EndReason.Completed) NormalizeDedicatedCompletedAppearance(request.Participants); done(); }, failed));
            };
            request.ReleaseParticipants = () =>
            {
                var failures = new List<Exception>();
                foreach (var lease in additional) try { lease.Dispose(); } catch (Exception ex) { failures.Add(ex); }
                additional.Clear(); if (failures.Count != default(int)) throw new AggregateException(failures);
            };
            request.ResolveParticipants = () =>
            {
                var inventoryWorld = _life.Store.Read();
                merchantSpendTotal = inventoryWorld.nativeMerchantSpendTotal;
                string worldIdentity = _life.PlayerInventoryKey.Substring("player:".Length);
                foreach (var inventory in inventoryWorld.inventories.Where(i => i.Key == _life.PlayerInventoryKey || i.kind == "storage" && i.owner == worldIdentity))
                    foreach (var item in inventory.items.Where(i => i.CategoryID == MainGameAiShoujoH.Progression.NativeHItemRequirements.InventoryCategory && i.Count > default(int))) ownedHItems.Add(item.ID);
                if (residentIds == null || !residentIds.Any()) return;
                if (residentIds.Length != _settings.I("math.one") || player.sex == _settings.I("math.one") || residentIds.Contains(resident.ResidentId))
                    throw new InvalidOperationException("追加女性は既存の女性主人公と重複させず、主参加住人以外を一人指定してください。");
                var world = _life.Store.Read();
                var extra = world.residents.SingleOrDefault(r => r.id == residentIds.Single()) ?? throw new InvalidOperationException("追加参加するAI住人がこのワールドに登録されていません。");
                string card, coordinate;
                if (!_pods.TryResolveAssignedResident(extra.origin, out card, out coordinate))
                {
                    if (!extra.origin.StartsWith("card:", StringComparison.Ordinal)) throw new InvalidOperationException("追加住人の保存カード割当がありません。");
                    card = extra.origin.Substring("card:".Length); coordinate = null;
                }
                var lease = _animationTrial.LoadAdditionalResident(extra.id, card, coordinate, request.Position, request.Rotation);
                additional.Add(lease);
                request.Participants = request.Participants.Concat(new[] { new Participant { Identity = extra.id, Role = "female2", Character = lease.Actor } }).ToArray();
            };
            DedicatedH.Begin(request);
        }
        private System.Collections.IEnumerator RestoreDedicatedVmd(long token, Action done, Action<Exception> failed)
        {
            var routine = MainGameVMDPlay.Plugin.RestoreFromAstral(token);
            while (true)
            {
                bool next; object current = null;
                try { next = routine.MoveNext(); if (next) current = routine.Current; }
                catch (Exception ex) { MainGameVMDPlay.Plugin.DiscardAstral(token); failed(ex); yield break; }
                if (!next) break; yield return current;
            }
            try { if (!MainGameVMDPlay.Plugin.IsAiHMotionRestored(token)) throw new InvalidOperationException("VMDの同一対象・再生状態の復元が完了しませんでした。"); MainGameVMDPlay.Plugin.ResumeFromAstral(token); done(); } catch (Exception ex) { MainGameVMDPlay.Plugin.DiscardAstral(token); failed(ex); }
        }
        private void NormalizeDedicatedCompletedAppearance(Participant[] participants)
        {
            var errors = new List<Exception>();
            foreach (var participant in participants)
            {
                var actor = participant.Character; if (actor == null) continue;
                try { MainGameAiShoujoH.Presentation.PresentationModule.NormalizeNativeCompleted(actor); } catch (Exception ex) { errors.Add(ex); }
            }
            if (errors.Any()) throw new AggregateException("正常H終了時の外見状態復帰に失敗しました。", errors);
        }
        private void DedicatedHStarted()
        {
            _hSuspending = true;
            _lighting?.BeforeDedicatedH();
            _furniture?.Suspend(); _authoring?.SuspendActivity(); _specialMovement?.Cancel();
            _playback?.Suspend();
            if (_conversation.Active) _hResume = _conversation.SuspendForH();
            else
            {
                var resident = _conversation.Resident; resident.BeginH();
                bool ended = false; _hResume = () => { if (!ended) { ended = true; resident.EndH(); } };
            }
            if (_help.Visible) _help.SetVisible(false);
            if (_host.Session.Panel) _host.Session.TogglePanel();
            _host.Session.ExternalInputBlocked = true; ReleaseInput();
            SuspendForH(_host.Island.Doors); SuspendForH(_host.Walker);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
        private void DedicatedHReady()
        {
            // Keep the same island, render camera, weather and clock owners.
            _host.Session.World.BindCamera(_host.Walker.ViewCamera);
            _host.Island.Visibility?.BindCamera(_host.Walker.ViewCamera);
            _lighting?.DedicatedReady(_host.Walker.ViewCamera);
        }
        private void DedicatedHEnded()
        {
            if (!_hSuspending) return;
            _hSuspending = false;
            foreach (var pair in _hSuspended) if (pair.Key != null) pair.Key.enabled = pair.Value;
            _hSuspended.Clear();
            if (_host?.Session != null) _host.Session.ExternalInputBlocked = false;
            _playerAvatar?.Resume();
            _lighting?.WalkingReady(_host?.Walker?.ViewCamera);
            var resume = _hResume; _hResume = null; resume?.Invoke();
            _authoring?.ResumeActivity(); _playback?.Resume();
            _host?.Walker?.SetCursorReleased(_conversationCursorReleased);
        }
    }
}
