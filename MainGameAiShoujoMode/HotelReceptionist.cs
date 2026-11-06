using System;
using System.IO;
using System.Linq;
using MapEnvironmentRuntime;
using UnityEngine;

namespace MainGameAiShoujoMode
{
    internal sealed partial class HotelReceptionist : IDisposable
    {
        private readonly Transform _parent;
        private readonly Parameters _p;
        private readonly HotelRoomControlSettings _cfg;
        private GameObject _stage;
        private ChaControl _actor;
        private Transform _lookTarget;
        internal string Name => _actor?.chaFile?.parameter?.fullname ?? "受付嬢";
        internal ChaControl Actor => _actor;
        internal void SetLookTarget(Transform target)
        {
            _lookTarget = target; FocusPlayer();
        }
        internal void FocusPlayer()
        {
            if (_actor == null || _lookTarget == null) return;
            var eyes = _actor.eyeLookCtrl?.eyeLookScript?.eyeTypeStates;
            var neck = _actor.neckLookCtrl?.neckLookScript?.neckTypeStates;
            int eyePattern = eyes == null ? -_p.I("math.one") : Array.FindIndex(eyes, state => state.lookType == EYE_LOOK_TYPE.TARGET);
            int neckPattern = neck == null ? -_p.I("math.one") : Array.FindIndex(neck, state => state.lookType == NECK_LOOK_TYPE_VER2.TARGET);
            if (eyePattern >= _p.I("math.zero")) { _actor.ChangeLookEyesTarget(_p.I("math.zero"), _lookTarget); _actor.ChangeLookEyesPtn(eyePattern); }
            if (neckPattern >= _p.I("math.zero")) { _actor.ChangeLookNeckTarget(_p.I("math.zero"), _lookTarget); _actor.ChangeLookNeckPtn(neckPattern, _p.N("math.one")); }
        }
        private readonly HotelRoomStateStore _store;
        private string _pose, _status;
        private bool _showPoses, _drawPoses;
        private Vector2 _scroll;
        internal HotelReceptionist(HotelFloorRuntime lobby, Parameters p, HotelRoomControlSettings cfg)
        {
            _parent = lobby.Roots.OrderByDescending(r => r.GetComponentsInChildren<Renderer>(true).Length).First().transform; _p = p; _cfg = cfg;
            _store = new HotelRoomStateStore(Path.Combine(BepInEx.Paths.GameRootPath, "UserData", "HotelMode", "RoomSessions"), "reception");
        }
        internal void Load(HotelRoomCard saved)
        {
            Dispose();
            var state = _store.Load();
            _pose = state.receptionistPose ?? _cfg.receptionistIdle;
            _placement = state.receptionistPlacement?.Copy() ?? new HotelReceptionistPlacement { x = _cfg.receptionistPosition.x, y = _cfg.receptionistPosition.y, z = _cfg.receptionistPosition.z, ry = _cfg.receptionistYaw };
            _savedPlacement = _placement.Copy(); ResetPlacementInputs();
            if (saved == null) return;
            var file = new ChaFileControl();
            if (!File.Exists(saved.card) || !file.LoadCharaFile(saved.card) || file.parameter.sex != _p.I("math.one")) throw new InvalidDataException("受付嬢のカードを読み込めません。登録し直してください。");
            try
            {
                _stage = new GameObject("Hotel receptionist"); _stage.transform.SetParent(_parent, false);
                ApplyPlacement();
                int id = UnityEngine.Object.FindObjectsOfType<ChaControl>().Select(c => c.chaID).DefaultIfEmpty(_p.I("math.zero")).Max() + _p.I("math.one");
                _actor = Manager.Character.CreateFemale(_stage, id, file, true);
                if (_actor == null) throw new InvalidOperationException("受付嬢を生成できません。");
                _actor.Load();
                var controller = CommonLib.LoadAsset<RuntimeAnimatorController>(_cfg.receptionistControllerBundle, _cfg.receptionistControllerAsset, true);
                if (controller == null || !_actor.loadEnd || _actor.animBody == null) throw new InvalidOperationException("受付嬢の待機動作を読み込めません。");
                _actor.animBody.runtimeAnimatorController = controller; _actor.animBody.applyRootMotion = false; _actor.animBody.fireEvents = false;
                _actor.animBody.Rebind(); PlayPose(_pose); _actor.SetActiveTop(true); _actor.visibleAll = true;
            }
            catch { Dispose(); throw; }
        }
        private void SelectPose(string pose)
        {
            GUIUtility.keyboardControl = _p.I("math.zero");
            try { PlayPose(pose); _store.SaveReceptionistPose(pose); _pose = pose; _status = "保存しました：" + PoseLabel(pose); }
            catch (Exception ex) { _status = ex.GetBaseException().Message; }
        }
        private void StepPose(int direction)
        {
            int count = _cfg.receptionistPoses.Length;
            int index = Math.Max(_p.I("math.zero"), Array.IndexOf(_cfg.receptionistPoses, _pose));
            SelectPose(_cfg.receptionistPoses[(index + direction + count) % count]);
        }
        private void PlayPose(string pose)
        {
            if (!_cfg.receptionistPoses.Contains(pose)) throw new InvalidDataException("受付嬢の保存ポーズが現在の一覧にありません。");
            if (_actor == null) return;
            int hash = Animator.StringToHash(pose);
            if (!_actor.animBody.HasState(_p.I("math.zero"), hash)) throw new InvalidDataException("受付嬢の待機ポーズがAnimatorにありません：" + pose);
            _actor.animBody.Play(hash, _p.I("math.zero"), _p.N("math.zero"));
            FocusPlayer();
        }
        private static string PoseLabel(string pose)
        {
            if (string.IsNullOrWhiteSpace(pose)) return "未選択";
            string name = pose.Split('.').Last();
            return name.Replace("Stand_Joy_", "喜び ").Replace("Stand_Ang_", "怒り ").Replace("Stand_Sat_", "落ち込み ")
                .Replace("Stand_Oth_", "その他 ").Replace("Stand_", "立ち待機 ");
        }
        internal void DrawPoseMenu()
        {
            if (Event.current.type == EventType.Layout) _drawPoses = _showPoses;
            if (_showPoses && Application.isFocused && Event.current.type == EventType.KeyDown
                && !(GUI.GetNameOfFocusedControl() ?? "").StartsWith("MainGameAiShoujoMode.Hotel", StringComparison.Ordinal)
                && (Event.current.keyCode == KeyCode.UpArrow || Event.current.keyCode == KeyCode.DownArrow))
            { StepPose(Event.current.keyCode == KeyCode.UpArrow ? -_p.I("math.one") : _p.I("math.one")); Event.current.Use(); }
            GUILayout.BeginVertical(GUI.skin.box);
            if (GUILayout.Button((_showPoses ? "▼ " : "▶ ") + "受付嬢の待機ポーズ", GUILayout.ExpandWidth(false))) _showPoses = !_showPoses;
            if (_drawPoses)
            {
                GUILayout.Label("選択：" + PoseLabel(_pose) + "　↑ 前へ／↓ 次へ（末尾から先頭へ循環）");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("前へ ↑", GUILayout.ExpandWidth(false))) StepPose(-_p.I("math.one"));
                if (GUILayout.Button("次へ ↓", GUILayout.ExpandWidth(false))) StepPose(_p.I("math.one"));
                GUILayout.EndHorizontal();
                _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(_cfg.lightListHeight));
                foreach (string pose in _cfg.receptionistPoses)
                    if (GUILayout.Button((_pose == pose ? "● " : "○ ") + PoseLabel(pose), GUILayout.ExpandWidth(false)))
                        SelectPose(pose);
                GUILayout.EndScrollView();
                if (!string.IsNullOrWhiteSpace(_status)) GUILayout.Label(_status);
            }
            GUILayout.EndVertical();
            DrawPlacement();
        }
        internal bool CanTalk(Camera camera)
        {
            if (_actor == null || _stage == null || !_stage.activeInHierarchy || camera == null) return false;
            var delta = _stage.transform.position + Vector3.up * _cfg.receptionistHeadHeight - camera.transform.position;
            return delta.magnitude <= _cfg.receptionistReach && Vector3.Dot(camera.transform.forward, delta.normalized) >= _cfg.receptionistViewDot;
        }
        public void Dispose()
        {
            if (_actor != null) { _actor.StopAllCoroutines(); Manager.Character.DeleteChara(_actor); _actor = null; }
            if (_stage != null) { UnityEngine.Object.Destroy(_stage); _stage = null; }
        }
    }
}
