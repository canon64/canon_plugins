using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MapEnvironmentRuntime;
using System.Runtime.Serialization;

namespace MainGameAiShoujoMode.Conversation
{
    // Only passive UI types are accepted. No source game controller is instantiated.
    internal sealed class KksUiHierarchy : IDisposable
    {
#pragma warning disable CS0649
        [Serializable, DataContract] internal sealed class Geometry
        {
            [DataMember] public string path;
            [DataMember] public Vector2 anchorMin, anchorMax, pivot, position, size;
            internal void Apply(RectTransform rect)
            {
                rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
                rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.pivot = pivot;
                rect.sizeDelta = size; rect.anchoredPosition = position;
            }
        }
        [Serializable] internal sealed class Reference
        { public string field, asset, kind; public long localId; }
        [Serializable] internal sealed class Part
        { public long id; public string type, json; public bool enabled; public Reference[] references; }
        [Serializable] internal sealed class Node
        { public string path, parent, name; public bool active; public long rectId; public Geometry rect; public Part[] components; }
        [Serializable, DataContract] internal sealed class Document
        {
            [DataMember] public string bundle, bundleSha256;
            [DataMember] public int sortingOrder, additionalShaderChannels, zero, one;
            [DataMember] public float scrollSensitivity;
            [DataMember] public Geometry menuViewport;
            [DataMember] public Geometry[] rectOverrides;
            [DataMember] public Node[] nodes;
        }
        [Serializable] private sealed class NativeGroup
        { public float m_Alpha; public bool m_Interactable, m_BlocksRaycasts, m_IgnoreParentGroups; }
        [Serializable] private sealed class NativeRenderer { public bool m_CullTransparentMesh; }
#pragma warning restore CS0649
        private static readonly Dictionary<string, Type> Types = new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            { "Image", typeof(Image) }, { "Text", typeof(Text) }, { "Button", typeof(Button) },
            { "Toggle", typeof(Toggle) }, { "Scrollbar", typeof(Scrollbar) },
            { "HorizontalLayoutGroup", typeof(HorizontalLayoutGroup) }, { "VerticalLayoutGroup", typeof(VerticalLayoutGroup) },
            { "ContentSizeFitter", typeof(ContentSizeFitter) }, { "LayoutElement", typeof(LayoutElement) },
            { "Outline", typeof(Outline) }, { "Shadow", typeof(Shadow) },
            { "CanvasGroup", typeof(CanvasGroup) }, { "CanvasRenderer", typeof(CanvasRenderer) },
            { "CanvasScaler", typeof(CanvasScaler) }, { "TextMeshProUGUI", typeof(TextMeshProUGUI) }
        };
        private readonly Dictionary<string, GameObject> _nodes = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        private readonly Dictionary<long, Component> _components = new Dictionary<long, Component>();
        private readonly Dictionary<string, UnityEngine.Object> _assets = new Dictionary<string, UnityEngine.Object>(StringComparer.Ordinal);
        private AssetBundle _bundle;
        private GameObject _root;
        internal Document Data { get; private set; }
        internal GameObject Root => _root;

        internal KksUiHierarchy(Transform parent, string contentDirectory, string descriptor = "kks-conversation-ui.json")
        {
            try
            {
                if (Path.GetFileName(descriptor) != descriptor) throw new FormatException("KKS UI descriptor must be a local file name.");
                Data = JsonCodec.FromJson<Document>(File.ReadAllText(Path.Combine(contentDirectory, descriptor)));
                if (Data?.nodes == null || Data.rectOverrides == null || Data.menuViewport == null)
                    throw new FormatException("KKS UI descriptor is incomplete.");
                if (Path.GetFileName(Data.bundle) != Data.bundle) throw new FormatException("KKS UI bundle must be a local file name.");
                var bundlePath = Path.Combine(contentDirectory, Data.bundle);
                using (var stream = File.OpenRead(bundlePath))
                using (var sha = SHA256.Create())
                {
                    if (!string.Equals(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""), Data.bundleSha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("KKS UI bundle differs from the verified descriptor.");
                }
                _bundle = AssetBundle.LoadFromFile(bundlePath);
                if (_bundle == null) throw new InvalidDataException("KKS UI resource bundle could not be loaded.");
                foreach (var part in Data.nodes.SelectMany(node => node.components))
                {
                    if (!Types.ContainsKey(part.type)) throw new NotSupportedException("Non-passive KKS UI component: " + part.type);
                    foreach (var reference in part.references.Where(reference => !string.IsNullOrEmpty(reference.asset)))
                    {
                        if (_assets.ContainsKey(reference.asset)) continue;
                        Type type;
                        switch (reference.kind)
                        {
                            case "Sprite": type = typeof(Sprite); break;
                            case "Font": type = typeof(Font); break;
                            case "Material": type = typeof(Material); break;
                            case "TMP_FontAsset": type = typeof(TMP_FontAsset); break;
                            default: throw new NotSupportedException("KKS UI resource type: " + reference.kind);
                        }
                        var asset = _bundle.LoadAsset(reference.asset, type);
                        if (asset == null) throw new InvalidDataException("KKS UI resource is missing: " + reference.asset);
                        _assets.Add(reference.asset, asset);
                    }
                }
                foreach (var node in Data.nodes)
                {
                    var item = new GameObject(node.name, typeof(RectTransform)); item.SetActive(false);
                    if (_root == null) { _root = item; item.transform.SetParent(parent, false); }
                    else item.transform.SetParent(string.IsNullOrEmpty(node.parent) ? _root.transform : _nodes[node.parent].transform, false);
                    _nodes.Add(node.path, item); _components.Add(node.rectId, item.transform);
                    node.rect.Apply((RectTransform)item.transform);
                    foreach (var part in node.components)
                    {
                        var type = Types[part.type];
                        // Shadow and Outline may coexist; do not retrieve Outline as its Shadow base.
                        var component = item.GetComponents<Component>().FirstOrDefault(existing => existing.GetType() == type) ?? item.AddComponent(type);
                        _components.Add(part.id, component);
                        Apply(component, part);
                    }
                }
                foreach (var node in Data.nodes)
                    foreach (var part in node.components)
                        foreach (var reference in part.references) Bind(_components[part.id], reference);
                foreach (var geometry in Data.rectOverrides) geometry.Apply((RectTransform)Get(geometry.path).transform);
                // CanvasScaler requires Canvas and can already have created it on this inactive root.
                var canvas = _root.GetComponent<Canvas>() ?? _root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = Data.sortingOrder;
                canvas.additionalShaderChannels = (AdditionalCanvasShaderChannels)Data.additionalShaderChannels;
                if (_root.GetComponent<GraphicRaycaster>() == null) _root.AddComponent<GraphicRaycaster>();
                foreach (var node in Data.nodes.Where(node => _nodes[node.path] != _root)) _nodes[node.path].SetActive(node.active);
                foreach (var graphic in _components.Values.OfType<Graphic>()) graphic.SetAllDirty();
            }
            catch { Dispose(); throw; }
        }

        private static void Apply(Component component, Part part)
        {
            if (component is CanvasGroup group)
            {
                var data = JsonCodec.FromJson<NativeGroup>(part.json);
                group.alpha = data.m_Alpha; group.interactable = data.m_Interactable;
                group.blocksRaycasts = data.m_BlocksRaycasts; group.ignoreParentGroups = data.m_IgnoreParentGroups;
            }
            else if (component is CanvasRenderer renderer) renderer.cullTransparentMesh = JsonCodec.FromJson<NativeRenderer>(part.json).m_CullTransparentMesh;
            else JsonUtility.FromJsonOverwrite(part.json, component);
            if (component is Behaviour behaviour) behaviour.enabled = part.enabled;
        }

        private void Bind(Component component, Reference reference)
        {
            UnityEngine.Object value = string.IsNullOrEmpty(reference.asset) ? _components[reference.localId] : _assets[reference.asset];
            switch (reference.field)
            {
                case "m_Sprite": ((Image)component).sprite = Require<Sprite>(value, reference); return;
                case "m_Material": ((Graphic)component).material = Require<Material>(value, reference); return;
                case "m_FontData.m_Font": ((Text)component).font = Require<Font>(value, reference); return;
                case "m_fontAsset": ((TMP_Text)component).font = Require<TMP_FontAsset>(value, reference); return;
                case "m_sharedMaterial": ((TMP_Text)component).fontSharedMaterial = Require<Material>(value, reference); return;
                case "m_TargetGraphic": ((Selectable)component).targetGraphic = Require<Graphic>(value, reference); return;
                case "graphic": ((Toggle)component).graphic = Require<Graphic>(value, reference); return;
                case "m_Group": ((Toggle)component).group = Require<ToggleGroup>(value, reference); return;
                case "m_HandleRect": ((Scrollbar)component).handleRect = Require<RectTransform>(value, reference); return;
            }
            if (reference.field.StartsWith("m_SpriteState.", StringComparison.Ordinal))
            {
                var selectable = (Selectable)component; var state = selectable.spriteState; var sprite = Require<Sprite>(value, reference);
                switch (reference.field)
                {
                    case "m_SpriteState.m_HighlightedSprite": state.highlightedSprite = sprite; break;
                    case "m_SpriteState.m_PressedSprite": state.pressedSprite = sprite; break;
                    case "m_SpriteState.m_SelectedSprite": state.selectedSprite = sprite; break;
                    case "m_SpriteState.m_DisabledSprite": state.disabledSprite = sprite; break;
                    default: throw new NotSupportedException(reference.field);
                }
                selectable.spriteState = state; return;
            }
            if (reference.field.StartsWith("m_Navigation.", StringComparison.Ordinal))
            {
                var selectable = (Selectable)component; var navigation = selectable.navigation; var target = Require<Selectable>(value, reference);
                switch (reference.field)
                {
                    case "m_Navigation.m_SelectOnUp": navigation.selectOnUp = target; break;
                    case "m_Navigation.m_SelectOnDown": navigation.selectOnDown = target; break;
                    case "m_Navigation.m_SelectOnLeft": navigation.selectOnLeft = target; break;
                    case "m_Navigation.m_SelectOnRight": navigation.selectOnRight = target; break;
                    default: throw new NotSupportedException(reference.field);
                }
                selectable.navigation = navigation; return;
            }
            throw new NotSupportedException("Unbound KKS UI reference: " + reference.field);
        }
        private static T Require<T>(UnityEngine.Object value, Reference reference) where T : UnityEngine.Object
        {
            if (value is T typed) return typed;
            throw new InvalidDataException("Invalid KKS UI reference type at " + reference.field);
        }
        internal GameObject Get(string path) => _nodes[path];
        internal T Get<T>(string path) where T : Component => Get(path).GetComponent<T>() ?? throw new InvalidDataException("KKS UI component missing at " + path);
        public void Dispose()
        {
            if (_root != null) { _root.SetActive(false); UnityEngine.Object.Destroy(_root); _root = null; }
            _nodes.Clear(); _components.Clear(); _assets.Clear();
            if (_bundle != null) { _bundle.Unload(true); _bundle = null; }
        }
    }
}
