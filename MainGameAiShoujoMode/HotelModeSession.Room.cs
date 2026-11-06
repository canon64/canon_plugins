using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MainGameAiShoujoMode
{
    internal sealed partial class HotelModeSession
    {
        private AiModeHSession _hSession;
        internal ActionScene NativeWalkingAction => _nativeWalking?.Action;
        private HotelRoomStateStore _roomStateStore;
        private HotelRoomState _roomState;
        private HotelRoomPlacement _livePlacement;
        private HotelRoomPlacementPanel _placementPanel;
        private Transform _sessionRoomRoot;
        private bool _restoringRoomSelection;
        private CanonGate.GateApi.HotelModeScope _hotelScope;
        private HotelRoomGadgets _gadgets;
        private HotelRoomLightingRuntime _roomLighting;
        private HotelRoomObjectLayout _roomObjects;
        private string _roomSource;
        private readonly HotelFinalSaveBoundary _roomFinalSave = new HotelFinalSaveBoundary();
        private string _roomEntryFailure;
        private HotelLoadingAudioScope _roomLoadingAudio;
        private System.Diagnostics.Stopwatch _roomEntryWatch;
        private long _roomEntryLastMilliseconds;
        private string _roomEntryRoom;
        private string _roomEntryStage;
        private Func<bool> _categoryLocationEditing;
        private Func<bool, string> _applyPoseLocation;
        private bool _mapPlacementHandled;
        private void ReleaseRoomLoadingAudio()
        { _roomLoadingAudio?.Dispose(); _roomLoadingAudio = null; }
        private void BeginRoomEntryTrace(string room)
        {
            _roomEntryWatch = null;
            if (!_diagnostics()) return;
            _roomEntryWatch = System.Diagnostics.Stopwatch.StartNew(); _roomEntryLastMilliseconds = default(long); _roomEntryRoom = room;
            TraceRoom("entry-requested");
        }
        private void TraceRoom(string stage)
        {
            if (!_diagnostics()) return;
            if (!stage.EndsWith("completion-event", StringComparison.Ordinal)) _roomEntryStage = stage;
            long elapsed = _roomEntryWatch?.ElapsedMilliseconds ?? default(long);
            _log("HOTEL_ROOM_STAGE " + stage + " floor=" + RoomFloor?.Definition.id
                + " targetRoom=" + _roomEntryRoom + " frame=" + Time.frameCount
                + " elapsedMs=" + elapsed + " stageMs=" + (elapsed - _roomEntryLastMilliseconds));
            _roomEntryLastMilliseconds = elapsed;
        }
        private void ReportRoomFailure(Exception ex, string stage)
        {
            ReleaseRoomLoadingAudio();
            _roomEntryFailure = "客室の開始に失敗しました：" + ex.GetBaseException().Message;
            if (_diagnostics()) { _log("HOTEL_ROOM_FAILED stage=" + stage); _error(ex); }
            _roomCards.ShowError(ex);
        }
        private void TryRoomLighting(Camera camera)
        {
            try { CreateRoomLighting(camera); }
            catch (Exception ex) { ReportRoomFailure(ex, "lighting"); }
        }
        private readonly Dictionary<Behaviour, bool> _roomSuspended = new Dictionary<Behaviour, bool>();
        private void StartRoomSession(string room)
        {
            if (_closing || _returnRequested || _loading || _changingFloor || _hSession?.Busy == true) { TraceRoom("start-blocked"); return; }
            var destination = _floors.SingleOrDefault(floor => floor.Definition.room?.id == room || floor.Definition.room?.cardSlot == room);
            if (destination == null) ReportRoomFailure(new InvalidOperationException("この客室のH用マップは用意されていません。"), "room-map");
            else OpenDirectRoom(destination);
        }
        private void StartRoomSessionAfterLoading(string room)
        {
            string stage = "registered-card";
            try
            {
                _roomEntryFailure = null; TraceRoom(stage);
                _roomFinalSave.Reset();
                var definition = RoomFloor.Definition.room;
                if (definition == null || (definition.id != room && definition.cardSlot != room)) throw new InvalidOperationException("先に、対象の客室へ入室してください。");
                room=definition.id;
                var saved = _roomCards.Registered(definition.cardSlot);
                if (saved == null) throw new InvalidOperationException(definition.label + "で使う女性カードをTABメニューから登録してください。");
                if (!File.Exists(saved.card)) throw new FileNotFoundException("登録したカードが見つかりません。カードを選び直してください。", saved.card);
                var card = new ChaFileControl();
                if (!card.LoadCharaFile(saved.card) || card.parameter.sex != _p.I("math.one")) throw new InvalidDataException("登録カードをKKSの女性として読み込めません。");
                stage = "room-root-and-bed"; TraceRoom(stage);
                // Pass the actual loaded room root, never a substitute native map.
                var root = RoomFloor.Roots.Single(r => r.name == definition.rootObject);
                var bed = root.GetComponentsInChildren<Renderer>(true).Single(r => r.name == definition.bedObject);
                Physics.SyncTransforms();
                var bounds = bed.bounds;
                var point = bounds.center; point.y = bounds.max.y;
                _sessionRoomRoot = root.transform;
                stage = "load-room-state"; TraceRoom(stage);
                _roomStateStore = new HotelRoomStateStore(Path.Combine(BepInEx.Paths.GameRootPath, "UserData", "HotelMode", "RoomSessions"), room);
                _roomSource = saved.card;
                LoadRoomStateForRestore(room);
                stage = "restore-character-state"; TraceRoom(stage);
                card = PrepareRoomCard(true, card);
                _livePlacement = null;
                if (_roomState.placement != null)
                    try { _roomState.placement.Validate(); _livePlacement = _roomState.placement.Copy(); }
                    catch (Exception ex) { _roomRestore.Failure("入室配置", "placement", ex); }
                // Start at the actual map point. Saved placement is selected after native startup;
                // feeding it into Begin would contaminate the fallback captured for this entry.
                if (_hSession == null)
                {
                    stage = "native-session-initialize"; TraceRoom(stage);
                    var session = new AiModeHSession();
                    try
                    {
                        session.Initialize(this, _p, RoomSessionStarted, RoomSessionEnded,
                            message => { if (_diagnostics()) _log(message); },
                            ex => ReportRoomFailure(ex, "native-session"), RoomSessionReady);
                        session.RoomSelectionChanged += RoomSelectionChanged;
                        session.RoomPositionReset += ReapplyRoomPlacement;
                        session.RoomEnding += CloseRoomPlacement;
                        session.RoomBeforeEnding += () => SaveBeforeRoomEnds();
                        session.PrepareRoomPoseCatalog = PrepareRoomPoseProviders;
                        _hSession = session;
                        session.AllRoomSelections = true;
                    }
                    catch { session.Dispose(); throw; }
                }
                stage = "hotel-permission"; TraceRoom(stage);
                _hSession.RoomPoseDiagnostics = _diagnostics;
                _hSession.RoomPoseTrace = (eventName, details) => _roomRestore.Observe(eventName, () =>
                    "room=" + room + " frame=" + Time.frameCount + " gateSession=" + CanonGate.GateApi.Current?.Id + " " + details());
                _hSession.DiagnosticSavedSelection = _roomState.selection;
                _hotelScope = CanonGate.GateApi.EnterHotelMode(this, room);
                var maleCard = PrepareRoomCard(false, null);
                var male = maleCard == null ? null : new SaveData.Player(maleCard, false);
                stage = "native-session-begin"; TraceRoom(stage);
                _hSession.Begin(new SaveData.Heroine(card, false), male, root, new AiModeHSession.HLocation
                {
                    SourceId = "hotel-room-" + room, Position = point, Rotation = bed.transform.rotation,
                    Categories = new[] { _p.I("hSession.category") }, VerifiedGround = true
                });
            }
            catch (Exception ex) { _roomRestore?.Failure("客室開始", stage, ex); if (_roomRestore != null) FinishRoomRestore(); _hotelScope?.Dispose(); _hotelScope = null; ReportRoomFailure(ex, stage); }
        }
        private void SuspendRoomBehaviour(Behaviour target)
        {
            if (target == null || _roomSuspended.ContainsKey(target)) return;
            _roomSuspended.Add(target, target.enabled); target.enabled = false;
        }
        private void RoomSessionStarted()
        {
            RefreshVoiceDiagnostics();
            _nativeWalking?.Suspend();
            _authoring?.SuspendActivity();
            _roomObjects?.Suspend();
            if (_roomLoadingAudio == null) _roomLoadingAudio = new HotelLoadingAudioScope(() => AudioListener.pause, value => AudioListener.pause = value);
            TraceRoom("native-started");
            _receptionist?.RestorePlacement();
            _walkingMenu = false;
            _roomCards.SetVisible(false);
            if (_help.Visible) _help.SetVisible(false);
            _context.WalkingReady = false; ReleaseInput();
            SuspendRoomBehaviour(_walker);
            if (_nativeWalking == null)
            {
                SuspendRoomBehaviour(_walker.ViewCamera);
                SuspendRoomBehaviour(_walker.ViewCamera.GetComponent<AudioListener>());
            }
            _walker.SetCursorReleased(true);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
        private void RoomSessionEnded()
        {
            _voiceDiagnostics?.Dispose(); _voiceDiagnostics = null;
            _roomObjects?.StopEditing();
            ReleaseRoomLoadingAudio();
            CloseRoomPlacement();
            _hotelScope?.Dispose(); _hotelScope = null;
            if (_closing || this == null) return;
            foreach (var pair in _roomSuspended) if (pair.Key != null) pair.Key.enabled = pair.Value;
            _roomSuspended.Clear();
            RestoreDirectRoom();
            _floor.SetActive(true);
            _nativeWalking?.Resume();
            if (_floor.Definition.room != null && !_returnRequested) TryRoomLighting(_walker.ViewCamera);
            if (_returnRequested) { BeginReturn(); return; }
            _loading = true;
            _fade.RevealAfterH(() =>
            {
                _loading = false;
                if (_returnRequested) { BeginReturn(); return; }
                if (_closing) return;
                _context.WalkingReady = true;
                _authoring?.ResumeActivity();
                if (_roomCards.Visible) RegistrationChanged();
                else _walker.SetCursorReleased(false);
            });
        }
        private void RoomSessionReady()
        {
            TraceRoom("native-ready");
            _roomRestore.Item("客室環境", "room-environment", "RoomFloor", () => RoomFloor, value => value.ApplyEnvironment());
            _roomRestore.Observe("actors-before", () => string.Join(";", _hSession.RoomActors().Select(actor => "sex=" + actor.sex + " active=" + actor.gameObject.activeInHierarchy + " loaded=" + actor.loadEnd + " position=" + actor.transform.position)));
            PrepareRoomMapControls();
            RestoreRoomReadyState();
            _placementPanel?.Dispose();
            _placementPanel = new HotelRoomPlacementPanel(_roomControls, _p,
                () => _hSession.ReadRoomPlacement(_sessionRoomRoot), ApplyRoomPlacement,
                value => { _roomStateStore.SavePlacement(value); _roomState.placement = value.Copy(); });
            // The saved pose has now replaced the native entry pose. Finish its map
            // placement before restoring actor-relative objects and IK bindings.
            BindRoomMapControls();
            _gadgets = new HotelRoomGadgets(_sessionRoomRoot, _roomState.gadgets, value => { _roomStateStore.SaveGadgets(value); _roomState.gadgets = value; },
                () => NewRoomSaveBatch("gadgets-button"),
                batch => Plugin.ReportHotelSaveResult(!batch.HasNotice, batch.FailureNotice), _roomStateStore.SaveGadget, _roomRestore);
            if (BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue("com.kks.main.girlbodyikgizmo", out var hip) && hip.Instance != null)
            {
                var complete = hip.Instance.GetType().GetMethod("CompleteHotelRoomRestore", Type.EmptyTypes);
                if (complete != null) _roomRestore.Item("HipHijack初期化", "gadgets[guid=com.kks.main.girlbodyikgizmo]", "hotel pose ownership",
                    () => complete, method => method.Invoke(hip.Instance, null));
            }
            _placementPanel.DrawGadgets = _gadgets.Draw;
            _placementPanel.DrawObjects = () => _roomObjects?.Draw();
            _placementPanel.Opened = _gadgets.Refresh;
            _placementPanel.OpenRegistration = () => _roomCards.SetVisible(true);
            _placementPanel.SaveEverythingWithPlacement = (read, persist) => SaveRoomSnapshot("save-button", read, persist);
            _placementPanel.DrawAudioDiagnostics = DrawVoiceDiagnostics;
            _voiceDiagnostics?.Snapshot("native-ready");
            _roomRestore.Item("照明", "lighting", "room lights/materials/bloom", () =>
            {
                var value = _roomState.lighting ?? throw new InvalidDataException("保存照明が欠損しています。"); value.Validate(); return value;
            }, _ => CreateRoomLighting(_hSession.ActiveCamera), value => _roomLighting != null && _roomLighting.MatchesApplied(value),
                value => "light=" + value.light + " emission=" + value.emission + " bloom=" + value.bloom, () => _roomLighting?.AppliedSummary(), () =>
                {
                    var before = _roomLighting?.State.Copy();
                    return () =>
                    {
                        _roomLighting?.Dispose(); _roomLighting = null;
                        if (before == null) return;
                        var desired = _roomState.lighting;
                        try { _roomState.lighting = before; CreateRoomLighting(_hSession.ActiveCamera); }
                        finally { _roomState.lighting = desired; }
                    };
                });
            if (_roomLighting != null) _placementPanel.DrawLighting = _roomLighting.Draw;
            FinishRoomRestore();
            ReleaseRoomLoadingAudio();
            RevealDirectRoom();
        }
        private void ApplyRoomPlacement(HotelRoomPlacement value)
        {
            _hSession.ApplyRoomPlacement(_sessionRoomRoot, value);
            _livePlacement = value.Copy();
        }
        private void ReapplyRoomPlacement()
        {
            if (_restoringRoomSelection || _hSession?.RoomReady != true) return;
            if (_categoryLocationEditing?.Invoke() == true) return;
            try
            {
                _roomRestore?.Observe("position-event-before", () => "origin=RoomPositionReset/RoomSelectionChanged before=" + PlacementSummary(_hSession.ReadRoomPlacement(_sessionRoomRoot)));
                string result = _applyPoseLocation?.Invoke(false) ?? "NotHandled";
                _mapPlacementHandled = result != "NotHandled";
                if (!_mapPlacementHandled && _livePlacement != null) _hSession.ApplyRoomPlacement(_sessionRoomRoot, _livePlacement);
                _roomRestore?.Observe("position-event-after", () => "origin=category-location-or-hotel-placement actual=" + PlacementSummary(_hSession.ReadRoomPlacement(_sessionRoomRoot)));
                _placementPanel?.RefreshPlacementBaseline();
            }
            catch (Exception ex) { _placementPanel?.Status(ex.Message); }
        }
        private bool SaveRoomSelection()
        {
            try
            {
                var selection = _hSession.ReadRoomSelection();
                if (selection == null) throw new InvalidOperationException("現在の体位を取得できません。");
                _roomStateStore.SaveSelection(selection); _roomState.selection = selection;
                return true;
            }
            catch (Exception ex) { _placementPanel?.Status("体位を保存できません：" + ex.Message); return false; }
        }
        private void RoomSelectionChanged()
        {
            if (_restoringRoomSelection) return;
            ReapplyRoomPlacement(); SaveRoomSelection();
        }
        private void CloseRoomPlacement()
        {
            _voiceDiagnostics?.Dispose(); _voiceDiagnostics = null;
            SaveRoomSnapshot(); _gadgets = null;
            _roomLighting?.Dispose(); _roomLighting = null;
            _placementPanel?.Dispose(); _placementPanel = null;
            _applyPoseLocation = null; _categoryLocationEditing = null; _mapPlacementHandled = false;
        }
        private void PrepareRoomMapControls()
        {
            _applyPoseLocation = null; _categoryLocationEditing = null; _mapPlacementHandled = false;
            if (!BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue("com.kks.maingameblankmapadd", out var plugin))
            { return; }
            try
            {
                var type = plugin.Instance.GetType();
                _applyPoseLocation = (Func<bool, string>)Delegate.CreateDelegate(typeof(Func<bool, string>), type.GetMethod("ApplyCurrentPoseLocation"));
                _categoryLocationEditing = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), type.GetMethod("IsCategoryLocationEditing"));
                var prepare = type.GetMethod("PrepareHotelPoseLocations") ?? throw new MissingMethodException("ホテルの開始位置取得APIがありません。");
                prepare.Invoke(null, null);
                _mapPlacementHandled = _applyPoseLocation(false) != "NotHandled";
            }
            catch (Exception ex)
            {
                // A failed connection must not be interpreted as permission to restore an old room placement.
                _applyPoseLocation = _ => "Failed";
                _mapPlacementHandled = true;
                _roomRestore?.Failure("体位場所連携", "category-location", ex);
            }
        }
        private void BindRoomMapControls()
        {
            _placementPanel.CanEditPlacement = () => _categoryLocationEditing?.Invoke() != true;
            if (_applyPoseLocation == null) return;
            try
            {
                string result = _applyPoseLocation(true);
                _mapPlacementHandled = result != "NotHandled";
                if (_mapPlacementHandled && result != "Applied") throw new InvalidOperationException("体位の場所を適用できません：" + result);
                _placementPanel.RefreshPlacementBaseline();
            }
            catch (Exception ex) { _mapPlacementHandled = true; _roomRestore?.Failure("体位場所連携", "category-location", ex); }
        }
        private bool SaveRoomSnapshot()
            => SaveBeforeRoomEnds();
        private bool SaveBeforeRoomEnds()
            => _roomFinalSave.Save(() => _hSession?.RoomReady == true && _roomStateStore != null,
                () => SaveRoomSnapshot("before-permission-revocation", null, null));
        private bool SaveRoomSnapshot(string trigger, Func<HotelRoomPlacement> readPlacement, Action<HotelRoomPlacement> persistPlacement)
        {
            if (_hSession?.RoomReady != true || _roomStateStore == null) return false;
            var batch = NewRoomSaveBatch(trigger);
            if (readPlacement != null)
                batch.Item("位置・向き", "placement", readPlacement, _roomStateStore.SavePlacement, PlacementSummary, value =>
                    { _roomState.placement = value.Copy(); _livePlacement = value.Copy(); persistPlacement(value); });
            else batch.Item("位置・向き", "placement", () => _hSession.ReadRoomPlacement(_sessionRoomRoot), value =>
                _roomStateStore.SavePlacement(value), PlacementSummary, value => { _roomState.placement = value.Copy(); _livePlacement = value.Copy(); });
            batch.Item("体位", "selection", () => _hSession.ReadRoomSelection() ?? throw new InvalidOperationException("現在の体位を取得できません。"), value =>
                _roomStateStore.SaveSelection(value), HotelPoseCatalogDiagnostics.Identity, value => _roomState.selection = value);
            if (_gadgets != null) _gadgets.Save(batch, value => _roomStateStore.SaveGadget(value));
            else batch.Item<bool>("小物", "gadgets", () => throw new InvalidOperationException("小物の保存セッションがありません。"), _ => { });
            if (_roomLighting != null)
                batch.Item("照明", "lighting", () => _roomLighting.State.Copy(), value =>
                    _roomStateStore.SaveLighting(value), value => "light=" + value.light + " emission=" + value.emission + " bloom=" + value.bloom, value => _roomState.lighting = value);
            else batch.Skip("照明", "lighting", "lighting-session-unavailable");
            SaveRoomActorsAndPreferences(batch);
            bool success = batch.Finish();
            _placementPanel?.Status(batch.Status);
            Plugin.ReportHotelSaveResult(!batch.HasNotice, batch.FailureNotice);
            return success;
        }
        private HotelRoomSaveBatch NewRoomSaveBatch(string trigger)
        {
            var batch = new HotelRoomSaveBatch(_diagnostics, _log, _roomStateStore.FilePath, trigger);
            batch.BeginFile(_roomStateStore.BeginSave, _roomStateStore.CommitSave, _roomStateStore.EndSave);
            return batch;
        }
        private static string PlacementSummary(HotelRoomPlacement value)
            => value == null ? "none" : "xyz=" + value.x + "," + value.y + "," + value.z + " yaw=" + value.yaw;
        private void CreateRoomLighting(Camera camera)
        {
            _roomLighting?.Dispose();
            _roomLighting = new HotelRoomLightingRuntime(_sessionRoomRoot, camera, _roomControls, _roomState.lighting,
                value => { _roomStateStore.SaveLighting(value); _roomState.lighting = value; }, RoomFloor.Definition.room?.rootObject == "HotelRoomProps__201");
        }
        private void RefreshRoomPortalPositions()
        {
            foreach(var pair in RoomFloor.PortalColliders)
            {
                var collider=pair.Value.FirstOrDefault(c=>c!=null&&!c.isTrigger);
                if(collider!=null)pair.Key.interaction=collider.bounds.center;
            }
        }
    }
}
