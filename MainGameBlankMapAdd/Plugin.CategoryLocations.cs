using System;
using System.Collections;
using System.Globalization;
using System.IO;
using CanonPlugins.Shared;
using MainGameTransformGizmo;
using UnityEngine;

namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        private static Plugin _roomFeatureOwner;
        private PoseLocationDocument _categoryLocations;
        private HLocationPreset _poseInitialPlacement;
        private Transform _poseInitialRoot;
        private string _poseInitialKey;
        private bool _categoryEditing;
        private string _categoryEditPose;
        private GameObject _categoryGizmoObject;
        private TransformGizmo _categoryGizmo;
        private long _roomRestoreGeneration;
        private bool _hotelPoseLocationsReady;
        private string _poseLocationFailureNotification;
        public enum PoseLocationResult { NotHandled, Applied, Deferred, Failed }
        public static void PreserveHotelRoomPlacement()
        {
            var owner = _roomFeatureOwner;
            if (owner == null || !owner._sessionStarted || !owner._hotelPlaybackSession || !PluginSessionGate.Capture().IsValid)
                throw new InvalidOperationException("ホテル客室の配置所有権を取得できません。");
            owner._hDefaultRequest.Cancel();
            ++owner._roomRestoreGeneration;
        }
        // Called before restoring the hotel's saved pose or placement.
        public static void PrepareHotelPoseLocations()
        {
            var owner = _roomFeatureOwner;
            if (owner == null || !owner._sessionStarted || !owner._hotelPlaybackSession || !PluginSessionGate.Capture().IsValid)
                throw new InvalidOperationException("ホテルの体位場所を準備できません。");
            owner._hotelPoseLocationsReady = false;
            owner._hDefaultRequest.Cancel();
            ++owner._roomRestoreGeneration;
            // Native VR walking lends its ActionMap to HSceneProc.Start. At the
            // gate-start event that private map field has not been assigned yet.
            owner.BindOwnedMapContext();
            if (!owner.HPlaceContext(out string key, out Transform root))
                throw new InvalidOperationException("マップの配置基準が未準備です。");
            owner.BindHPlaceContext(key, root);
            owner.CapturePoseInitialPlacement(key, root);
        }
        public static string ApplyCurrentPoseLocation(bool startup)
        {
            var owner = _roomFeatureOwner;
            if (owner == null) return PoseLocationResult.NotHandled.ToString();
            if (startup && owner._hotelPlaybackSession) owner._hotelPoseLocationsReady = true;
            return owner.ApplyCategoryLocation(startup).ToString();
        }
        private void ReportPoseLocationFailure(string message)
        {
            _hPlaceStatus = message;
            _poseLocationFailureNotification = MainGameNotifications.NotificationApi.Publish(
                GUID, "体位のH場所", message, MainGameNotifications.NotificationSeverity.Failure, _poseLocationFailureNotification);
        }
        private string CategoryLocationPath => Path.Combine(_pluginDir, "HLocationPosePlacements.json");

        private void LoadRoomCustomizations()
        {
            _roomFeatureOwner = this;
            TraceHPlace("pose-load.begin",()=>"path="+CategoryLocationPath+" exists="+File.Exists(CategoryLocationPath));
            try { _categoryLocations = RoomCustomizationStore.Load<PoseLocationDocument>(CategoryLocationPath, d => d.Validate()); TraceHPlacePoses("pose-load.parsed",_categoryLocations); }
            catch (Exception ex) { TraceHPlace("pose-load.failed",()=>"path="+CategoryLocationPath+" reason="+ex);_categoryLocations = null; ReportPoseLocationFailure("体位の場所は読込失敗。元ファイルは保持しています：" + ex.Message); }
        }
        private string CurrentPoseKey()
        {
            var info = PluginSessionGate.HScene?.flags?.nowAnimationInfo;
            if (info == null) return null;
            return string.Join("\n", info.mode.ToString(), info.id.ToString(CultureInfo.InvariantCulture),
                info.paramFemale?.path?.assetpath ?? "", info.paramFemale?.path?.file ?? "");
        }
        private void CapturePoseInitialPlacement(string key, Transform root)
        {
            if ((_poseInitialPlacement != null && _poseInitialRoot == root && _poseInitialKey == key) || _femaleChara == null) return;
            var position = HA(root.InverseTransformPoint(_femaleChara.transform.position));
            _poseInitialPlacement = new HLocationPreset {
                Id = "pose-initial", Name = "今回のマップ開始位置", MapKey = key, Legacy = true,
                Position = position, Center = (float[])position.Clone(),
                Rotation = HA((Quaternion.Inverse(root.rotation) * _femaleChara.transform.rotation).eulerAngles) };
            _poseInitialRoot = root; _poseInitialKey = key;
            TraceHPlace("pose-initial.captured",()=>HPlaceRecord(_poseInitialPlacement)+HPlaceActual(root));
        }
        private void QueueRoomCustomizationRestore()
        {
            if (!_sessionStarted || !HPlaceContext(out string key, out Transform root)) return;
            BindHPlaceContext(key, root);
            CapturePoseInitialPlacement(key, root);
            if (_hotelPlaybackSession && !_hotelPoseLocationsReady) return; // Wait for the hotel's saved-pose restore.
            long ticket = _hDefaultRequest.Begin(key, root);
            if (ticket == 0) return;
            long generation = ++_roomRestoreGeneration;
            TraceHPlace("pose-restore.queued",()=>"generation="+generation+" map="+HTraceText(key));
            var permit = PluginSessionGate.Capture();
            StartCoroutine(RestoreRoomCustomization(generation, key, root, permit, ticket));
        }
        private IEnumerator RestoreRoomCustomization(long generation, string key, Transform root, CanonGate.GateApi.Permit permit, long ticket)
        {
            // One finite settle point lets the native startup and movement listeners finish.
            yield return new WaitForEndOfFrame();
            if (!permit.IsValid || !_sessionStarted || generation != _roomRestoreGeneration || !_hDefaultRequest.Current(ticket, key, root) ||
                !HPlaceContext(out string current, out Transform actual) || current != key || actual != root) {TraceHPlace("pose-restore.cancelled",()=>"generation="+generation+" currentGeneration="+_roomRestoreGeneration+" permit="+permit.IsValid+" session="+_sessionStarted);yield break;}
            bool applied=ApplyCategoryLocation(true) == PoseLocationResult.Applied;
            TraceHPlace("pose-restore.finished",()=>"generation="+generation+" applied="+applied+HPlaceActual(root));
        }
        private bool TryApplyCategoryLocation() => ApplyCategoryLocation(false) == PoseLocationResult.Applied;
        private PoseLocationResult ApplyCategoryLocation(bool startup)
        {
            if (!_sessionStarted || !PluginSessionGate.Capture().IsValid || _categoryLocations?.Automatic == false)
                return PoseLocationResult.NotHandled;
            if (_categoryLocations == null || _hLocations == null) return PoseLocationResult.Failed;
            if ((_hotelPlaybackSession && !_hotelPoseLocationsReady) || _hPlaceDraft != null || _hPlaceBusy ||
                !HPlaceContext(out string key, out Transform root)) return PoseLocationResult.Deferred;
            string pose = CurrentPoseKey();
            if (pose == null) {TraceHPlace("pose-recall.skipped",()=>"reason=pose-unavailable");return PoseLocationResult.Deferred;}
            HPlaceSnapshot before = null;
            try
            {
                BindHPlaceContext(key, root);
                // Early native pose callbacks can run before the lifecycle's initial capture.
                // They must not move actors and contaminate the start that will be captured next.
                if (_poseInitialPlacement == null)
                {
                    if (!startup) return PoseLocationResult.Deferred;
                    throw new InvalidOperationException("今回のマップ開始位置を取得できていません。自動移動は行いません。");
                }
                // Refresh the map default on every event; never read the old saved Initial list.
                var mapDefault = _hLocations.DefaultFor(key) ?? _poseInitialPlacement;
                var individual = _categoryLocations.Find(key, pose);
                var placement = individual?.Placement ?? mapDefault;
                TraceHPlace("pose-recall.selected",()=>"path="+CategoryLocationPath+" pose="+HTraceText(pose)+" source="+(individual!=null?"saved-pose":_hLocations.DefaultFor(key)!=null?"map-default":"native-start")+" "+HPlaceRecord(placement)+HPlaceActual(root));
                if (placement == null) throw new InvalidOperationException("今回のマップ開始位置を取得できていません。自動移動は行いません。");
                bool restoreCamera = startup && individual == null && !placement.Legacy;
                if (restoreCamera && !AdoptHPlaceCamera()) throw new InvalidOperationException("カメラの操作権を取得できません。");
                var target = root.TransformPoint(HV(placement.Position));
                var rotation = root.rotation * Quaternion.Euler(HV(placement.Rotation));
                if (!restoreCamera && (_femaleChara.transform.position - target).sqrMagnitude < 0.00000001f && Quaternion.Angle(_femaleChara.transform.rotation, rotation) < 0.0001f)
                {
                    _hPlaceActive = placement.Clone();
                    return PoseLocationResult.Applied;
                }
                before = SnapshotHPlace();
                ApplyHPlace(placement, root, restoreCamera, "pose-recall");
                _hPlaceOriginal = _hPlaceOriginal ?? before;
                _hPlaceActive = placement.Clone();
                return PoseLocationResult.Applied;
            }
            catch (Exception ex)
            {
                TraceHPlace("pose-recall.failed",()=>"reason="+ex);
                string reason = ex.Message;
                try { RestoreHPlace(before); }
                catch (Exception rollback) { reason += "／編集前の配置へ戻せません：" + rollback.Message; }
                ReportPoseLocationFailure("体位の場所を呼び出せません：" + reason);
                return PoseLocationResult.Failed;
            }
        }
        public static bool TryApplyCurrentCategoryLocation() => _roomFeatureOwner?.TryApplyCategoryLocation() == true;
        public static bool IsCategoryLocationEditing() => _roomFeatureOwner?._categoryEditing == true;

        private void DrawCategoryLocationControls()
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("体位の場所");
            var info = PluginSessionGate.HScene?.flags?.nowAnimationInfo;
            string pose = CurrentPoseKey();
            GUILayout.Label("現在の体位：" + (info?.nameAnimation ?? "準備中"));
            GUILayout.Label("個別保存がない体位は、マップのデフォルト位置を使います。");
            if (_categoryLocations == null) GUILayout.Label(_hPlaceStatus ?? "保存ファイルを読み込めません。");
            else if (HPlaceContext(out string key, out _))
            {
                bool automatic = GUILayout.Toggle(_categoryLocations.Automatic, "体位に合わせて自動移動", GUILayout.ExpandWidth(false));
                if (automatic != _categoryLocations.Automatic)
                {
                    try { var next = _categoryLocations.ForSave(); next.Automatic = automatic; SaveHLocationPoses(next); _categoryLocations = next; }
                    catch (Exception ex) { ReportPoseLocationFailure("自動移動設定を保存できません：" + ex.Message); }
                }
                bool stored = pose != null && _categoryLocations.Find(key, pose) != null;
                GUILayout.Label(stored ? "この体位は個別保存済み" : "この体位はマップのデフォルト位置");
                bool enabled = GUI.enabled;
                GUI.enabled = enabled && pose != null && !_hPlaceBusy && _hPlaceDraft == null;
                if (GUILayout.Button("この体位の場所を調整", GUILayout.Width(205f), GUILayout.Height(30f))) QueueHPlaceUi(BeginCategoryLocationEdit);
                if (stored && GUILayout.Button("登録を解除", GUILayout.Width(110f)))
                {
                    QueueHPlaceUi(() => RemoveCategoryLocation(key, pose));
                }
                GUI.enabled = enabled;
            }
            GUILayout.EndVertical();
        }
        private void BeginCategoryLocationEdit()
        {
            if (_categoryLocations == null || _hPlaceDraft != null || _hPlaceBusy || !HPlaceContext(out string key, out Transform root)) return;
            string pose = CurrentPoseKey(); if (pose == null) return;
            _hDefaultRequest.Cancel(); ++_roomRestoreGeneration;
            BindHPlaceContext(key, root);
            HPlaceSnapshot before = null;
            try
            {
                before = SnapshotHPlace();
                var position = root.InverseTransformPoint(_femaleChara.transform.position);
                _hPlaceDraft = new HLocationPreset { Id = "pose-" + Guid.NewGuid().ToString("N"), MapKey = key, Name = PluginSessionGate.HScene.flags.nowAnimationInfo.nameAnimation,
                    Legacy = true, Position = HA(position), Center = HA(position), Rotation = HA((Quaternion.Inverse(root.rotation) * _femaleChara.transform.rotation).eulerAngles) };
                _hPlaceEditBefore = before; _categoryEditPose = pose; _categoryEditing = true; _hPlaceEditValid = true;
                SyncHPlaceEditNumbers();
                for (int group = 0; group < 3; group++) { RecenterHPlaceSliders(group); _hPlaceWidthInputs[group] = _hPlaceSliderWidths[group].ToString("R", CultureInfo.InvariantCulture); }
                _categoryGizmoObject = new GameObject("__HCategoryLocationGizmo");
                _categoryGizmoObject.transform.SetPositionAndRotation(_femaleChara.transform.position, _femaleChara.transform.rotation);
                _categoryGizmo = TransformGizmoApi.Attach(_categoryGizmoObject);
                if (_categoryGizmo == null) throw new InvalidOperationException("ギズモを作成できません。");
                _categoryGizmo.AllowScale = false;
                _categoryGizmo.CenterDragOnXZPlane = true;
                _categoryGizmo.TransformChanged += CategoryGizmoChanged;
                _categoryGizmo.DragStateChanged += CategoryGizmoDragStateChanged;
                TransformGizmoApi.EnableCameraInputCaptureOnDrag(_categoryGizmo);
                _categoryGizmo.SetVisible(true);
                _hPlaceStatus = "移動・回転して「この場所を覚える」で、この体位だけに保存します。未保存の体位はマップのデフォルト位置を使います。";
            }
            catch (Exception ex) { EndCategoryGizmo(); RestoreHPlace(before); _hPlaceDraft = null; _hPlaceEditBefore = null; _hPlaceStatus = ex.Message; }
        }
        private void CategoryGizmoChanged()
        {
            if (!_categoryEditing || _categoryGizmoObject == null || _hPlaceDraft == null || !PluginSessionGate.Capture().IsValid ||
                !HPlaceContext(out string key, out Transform root) || key != _hPlaceDraft.MapKey) return;
            var target = _categoryGizmoObject.transform;
            _hPlaceDraft.Position = HA(root.InverseTransformPoint(target.position));
            _hPlaceDraft.Rotation = HA((Quaternion.Inverse(root.rotation) * target.rotation).eulerAngles);
            SyncHPlaceEditNumbers();
            PreviewHPlaceEdit();
            // Undo/redo also emits TransformChanged, without a pointer drag to end.
            if (_categoryGizmo != null && !_categoryGizmo.IsDragging)
                for (int group = 0; group < 2; group++) RecenterHPlaceSliders(group);
        }
        private void SyncCategoryGizmo()
        {
            if (_categoryEditing && _categoryGizmoObject != null && _hPlaceDraft != null && _hPlaceRoot != null)
                _categoryGizmoObject.transform.SetPositionAndRotation(_hPlaceRoot.TransformPoint(HV(_hPlaceDraft.Position)), _hPlaceRoot.rotation * Quaternion.Euler(HV(_hPlaceDraft.Rotation)));
        }
        private void CategoryGizmoDragStateChanged(bool dragging)
        {
            NotifyGizmoDragState(dragging);
            if (!dragging && _categoryEditing && _hPlaceDraft != null)
                for (int group = 0; group < 2; group++) RecenterHPlaceSliders(group);
        }
        private void SaveCategoryLocation()
        {
            if (!_categoryEditing || _hPlaceDraft == null || !_hPlaceEditValid || !HPlaceContext(out string key, out _) || key != _hPlaceDraft.MapKey) return;
            try
            {
                var next = _categoryLocations.ForSave();
                if (_categoryEditPose != CurrentPoseKey()) throw new InvalidOperationException("編集対象の体位が変わりました。");
                next.Entries.RemoveAll(e => e.Pose == _categoryEditPose && HLocationMapIdentity.Same(e.Placement.MapKey, key));
                next.Entries.Add(new PoseLocation { Pose = _categoryEditPose, Placement = _hPlaceDraft.Clone() });
                SaveHLocationPoses(next);
                _categoryLocations = next; _hPlaceOriginal = _hPlaceOriginal ?? _hPlaceEditBefore; _hPlaceActive = _hPlaceDraft.Clone();
                EndCategoryGizmo(); ReleaseHPlaceSliderCapture(); _hPlaceDraft = null; _hPlaceEditBefore = null;
                _hPlaceStatus = "この体位だけの配置を保存しました。";
            }
            catch (Exception ex) { ReportPoseLocationFailure("保存できません：" + ex.Message); }
        }
        private void RemoveCategoryLocation(string key, string pose)
        {
            if (_hPlaceDraft != null || !HPlaceContext(out string current, out _) || key != current) return;
            try { var next = _categoryLocations.ForSave(); next.Entries.RemoveAll(e => e.Pose == pose && HLocationMapIdentity.Same(e.Placement.MapKey, key)); SaveHLocationPoses(next); _categoryLocations = next; _hPlaceStatus = "登録を解除しました。次の体位変更からマップのデフォルト位置を使います。"; }
            catch (Exception ex) { ReportPoseLocationFailure("解除できません：" + ex.Message); }
        }
        private void EndCategoryGizmo()
        {
            _categoryEditing = false;
            if (_categoryGizmo != null)
            {
                _categoryGizmo.TransformChanged -= CategoryGizmoChanged;
                _categoryGizmo.DragStateChanged -= CategoryGizmoDragStateChanged;
                TransformGizmoApi.DisableCameraInputCaptureOnDrag(_categoryGizmo);
                _categoryGizmo.SetVisible(false);
            }
            _categoryGizmo = null;
            if (_categoryGizmoObject != null) Destroy(_categoryGizmoObject);
            _categoryGizmoObject = null; NotifyGizmoDragState(false);
        }
    }
}
