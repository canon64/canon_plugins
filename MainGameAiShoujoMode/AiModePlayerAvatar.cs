using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MapEnvironmentRuntime;
using UnityEngine;
using UnityEngine.Rendering;

namespace MainGameAiShoujoMode
{
    // Movement and yaw are inherited from the existing controller hierarchy.
    // Rebind only on card, camera-mode, parameter, crouch and H lifecycle events.
    internal sealed class AiModePlayerAvatar : IDisposable
    {
        private readonly SandboxHost _host;
        private readonly SandboxFreeCamera _view;
        private readonly Action<string> _log;
        private readonly Action<Exception> _error;
        private readonly AiCardPicker _picker;
        private readonly Dictionary<Renderer, ShadowCastingMode> _head = new Dictionary<Renderer, ShadowCastingMode>();
        private GameObject _root;
        private ChaControl _actor;
        private Transform _eye;
        private string _card;
        private bool _showCards, _drawCards, _disposed, _femaleCards;
        private Parameters P => _host.Session.Settings;
        internal ChaControl Actor=>_actor;
        internal Transform Root=>_root==null?null:_root.transform;
        internal Action PrepareTraversal(Transform motion)
        {
            if(_actor==null||_root==null||_eye==null)throw new InvalidOperationException("主人公を読み込んでください。");
            if (_view.UsesNativeWalking)
            {
                var restoreNative = _view.PrepareNativeTraversal(motion);
                return () => { restoreNative(); BindView(); };
            }
            var camera=_view.ViewCamera.transform;var parent=camera.parent;var position=camera.localPosition;var rotation=camera.localRotation;
            _root.transform.localScale=Vector3.one;camera.SetParent(_eye,false);camera.localPosition=P.V("player.eyeOffset");camera.localRotation=Quaternion.identity;
            bool restored=false;
            return ()=>
            {
                if(restored)return;restored=true;
                if(camera!=null){camera.SetParent(parent,false);camera.localPosition=position;camera.localRotation=rotation;}
                BindView();
            };
        }
        public AiModePlayerAvatar(SandboxHost host, Action<string> log, Action<Exception> error, ChaFileControl savedCard = null)
        {
            _host = host; _log = log; _error = error;
            _view = host.Walker;
            if (_view == null) throw new InvalidOperationException("主人公の歩行カメラがありません。");
            _picker = new AiCardPicker(P, P.T("player.card.directory"), error);
            try
            {
                Load(P.T("player.card.path"), savedCard);
                _host.Session.ParametersApplied += Apply;
                _view.ViewGeometryChanged += BindView;
            }
            catch { Dispose(); throw; }
        }
        internal void Load(string path, ChaFileControl savedCard = null)
        {
            GameObject candidate = null; ChaControl actor = null;
            try
            {
                ChaFileControl file = savedCard ?? Manager.Game.saveData?.player?.charFile;
                if (savedCard == null && !string.IsNullOrWhiteSpace(path))
                {
                    file = new ChaFileControl();
                    if (!file.LoadCharaFile(path) || (file.parameter.sex != P.I("math.zero") && file.parameter.sex != P.I("math.one"))) throw new InvalidDataException("主人公用のキャラカードではありません。");
                }
                candidate = new GameObject("AI Mode Player");
                candidate.transform.SetParent(_host.transform, false);
                int id = UnityEngine.Object.FindObjectsOfType<ChaControl>().Select(item => item.chaID).DefaultIfEmpty(P.I("math.zero")).Max() + P.I("math.one");
                actor = file?.parameter.sex == P.I("math.one") ? Manager.Character.CreateFemale(candidate, id, file, true) : Manager.Character.CreateMale(P.I("math.zero"), candidate, id, file, true);
                if (actor == null) throw new InvalidOperationException("主人公キャラを生成できません。");
                actor.Load();
                if (!actor.loadEnd || actor.animBody == null) throw new InvalidOperationException("主人公キャラの読込が完了していません。");
                var eye = actor.GetReferenceInfo(ChaReference.RefObjKey.N_EyeBase);
                if (eye == null) throw new InvalidDataException("主人公の目の基準点 n_eyebase がありません。");
                actor.animBody.applyRootMotion = false;
                actor.SetActiveTop(true); actor.visibleAll = true;
                foreach (var collider in candidate.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                Release();
                _root = candidate; _actor = actor; _eye = eye.transform; _card = path;
                foreach (var part in new[] { actor.objHead }.Concat(actor.objHair ?? Array.Empty<GameObject>()))
                    if (part != null) foreach (var renderer in part.GetComponentsInChildren<Renderer>(true))
                        if (!_head.ContainsKey(renderer)) _head.Add(renderer, renderer.shadowCastingMode);
                BindView();
                _log("AI_MODE_PLAYER_READY card=" + (string.IsNullOrWhiteSpace(path) ? "current/default male" : path) + " eye=" + _eye.name + " liveVerified=false");
            }
            catch
            {
                if (!ReferenceEquals(candidate, _root))
                {
                    if (actor != null) Manager.Character.DeleteChara(actor);
                    if (candidate != null) UnityEngine.Object.Destroy(candidate);
                }
                throw;
            }
        }
        private void Apply(Parameters settings)
        {
            _picker.Configure(settings);
            if (_card != settings.T("player.card.path")) Load(settings.T("player.card.path"));
            else BindView();
        }
        private void BindView()
        {
            if (_disposed || _root == null || _eye == null) return;
            bool walking = P.T("movement.mode") == "walk";
            if (walking)
            {
                _root.transform.SetParent(_view.transform, false);
                _root.transform.localPosition = _view.FeetLocalOffset;
                _root.transform.localRotation = Quaternion.identity;
                float scale = _view.Crouching ? P.N("movement.crouchHeight") / P.N("movement.bodyHeight") : P.N("math.one");
                _root.transform.localScale = Vector3.one * scale;
                if (!_view.UsesNativeWalking) _view.ViewCamera.transform.localPosition = _view.transform.InverseTransformPoint(_eye.position) + P.V("player.eyeOffset");
            }
            else
            {
                _root.transform.SetParent(_host.transform, true);
                _root.transform.localScale = Vector3.one;
                if (!_view.UsesNativeWalking) _view.ViewCamera.transform.localPosition = Vector3.zero;
            }
            foreach (var pair in _head) if (pair.Key != null) pair.Key.shadowCastingMode = walking ? ShadowCastingMode.ShadowsOnly : pair.Value;
        }
        public SaveData.Player ExportPlayer()
        {
            if (_actor == null) throw new InvalidOperationException("主人公が読み込まれていません。");
            var player = new SaveData.Player(_actor.chaFile, false);
            player.SetRoot(_actor.gameObject); return player;
        }
        public void Suspend() { if (_root != null) _root.SetActive(false); }
        internal Action AcquireDedicatedH()
        {
            if (_root == null || _actor == null) throw new InvalidOperationException("専用Hの主人公が未読込です。");
            var root = _root.transform; var parent = root.parent; var position = root.localPosition; var rotation = root.localRotation; var scale = root.localScale;
            bool active = _root.activeSelf;
            root.SetParent(_host.transform, true); root.localScale = Vector3.one; _root.SetActive(true);
            foreach (var pair in _head) if (pair.Key != null) pair.Key.shadowCastingMode = pair.Value;
            bool restored = false;
            return () =>
            {
                if (restored) return; restored = true;
                if (root == null || !ReferenceEquals(root.gameObject, _root)) return;
                root.SetParent(parent, false); root.localPosition = position; root.localRotation = rotation; root.localScale = scale; _root.SetActive(active); BindView();
            };
        }
        public void Resume() { if (_root != null) { _root.SetActive(true); BindView(); } }
        public void DrawControls()
        {
            if (Event.current.type == EventType.Layout) _drawCards = _showCards;
            GUILayout.Label("主人公：" + (string.IsNullOrWhiteSpace(_card) ? "現在の主人公／標準男性" : Path.GetFileNameWithoutExtension(_card)));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_showCards ? "主人公カード一覧を閉じる" : "主人公カードを画像で選ぶ", GUILayout.ExpandWidth(false))) _showCards = !_showCards;
            if (GUILayout.Button(_femaleCards ? "男性カードへ切替" : "女性カードへ切替", GUILayout.ExpandWidth(false)))
            {
                _femaleCards = !_femaleCards; _picker.Refresh(P.T(_femaleCards ? "animation.card.directory" : "player.card.directory")); _showCards = true;
            }
            GUILayout.EndHorizontal();
            if (_drawCards) _picker.Draw(_card, path =>
            {
                try
                {
                    var next = P.Copy(); next.text.Single(item => item.key == "player.card.path").value = path;
                    next.revision = Guid.NewGuid().ToString("N"); _host.Session.Apply(next, true);
                }
                catch (Exception ex) { _error(ex); }
            });
        }
        private void Release()
        {
            _head.Clear(); _eye = null;
            if (_actor != null) Manager.Character.DeleteChara(_actor);
            _actor = null;
            if (_root != null) UnityEngine.Object.Destroy(_root);
            _root = null;
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            _host.Session.ParametersApplied -= Apply;
            _view.ViewGeometryChanged -= BindView;
            _picker.Dispose();
            if (_view.ViewCamera != null) _view.ViewCamera.transform.localPosition = Vector3.zero;
            Release();
        }
    }
}
