using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MainGameHCharacterMaker
{
    public sealed partial class Plugin
    {
        private const int OptionsPerPage = 12;
        private static readonly string[] PageNames = { "体型", "顔の形", "髪", "瞳・眉", "化粧", "肌・体" };
        private readonly List<AppearancePart> _parts = AppearanceCatalog.Create();
        private readonly Dictionary<int, ValueEditSession> _appearanceEdits = new Dictionary<int, ValueEditSession>();
        private readonly Dictionary<string, List<ListInfoBase>> _optionCache = new Dictionary<string, List<ListInfoBase>>();
        private readonly Dictionary<string, Texture2D> _thumbCache = new Dictionary<string, Texture2D>();
        private readonly Dictionary<string, string> _colorText = new Dictionary<string, string>();
        private readonly Dictionary<int, AppearancePart> _selectedParts = new Dictionary<int, AppearancePart>();
        private List<ListInfoBase> _filteredOptions = new List<ListInfoBase>();
        private readonly Dictionary<int, string> _optionNames = new Dictionary<int, string>();
        private GUIContent[] _tiles = Array.Empty<GUIContent>();
        private int _page, _optionPage;
        private string _search = "";
        private AppearancePart _part;

        private void EnsureAppearanceEdit(int index)
        {
            if (_appearanceEdits.ContainsKey(index)) return;
            var actor = _actors[index];
            var identity = new NativeTarget(this, actor);
            var hair = actor.fileHair;
            var makeup = AppearanceCatalog.Makeup(actor);
            _appearanceEdits.Add(index, new ValueEditSession(
                () => identity.IsAlive && ReferenceEquals(actor.fileHair, hair)
                    && ReferenceEquals(AppearanceCatalog.Makeup(actor), makeup), () => CanEdit));
        }
        private void SelectPage(int page)
        {
            BreakAppearanceMerge();
            _page = page; _body = page == 0; _scroll = Vector2.zero;
            if (_lastPage != null && _lastPage.Value != page) _lastPage.Value = page;
            if (page < 2) return;
            if (!_selectedParts.TryGetValue(page, out var part))
            {
                part = _parts.Find(x => x.Page == page);
                _selectedParts[page] = part;
            }
            SelectPart(part);
        }
        private void SelectPart(AppearancePart part)
        {
            BreakAppearanceMerge();
            _part = part; _selectedParts[_page] = part;
            _search = ""; _optionPage = 0; _colorText.Clear();
            PrepareOptions();
        }
        private void PrepareOptions()
        {
            _filteredOptions = new List<ListInfoBase>(); _optionNames.Clear();
            if (_part?.Read == null) { _tiles = Array.Empty<GUIContent>(); return; }
            var actor = _actors[_selected];
            string cacheKey = _selected + ":" + (int)_part.Category;
            if (!_optionCache.TryGetValue(cacheKey, out var options))
            {
                options = new List<ListInfoBase>();
                var source = actor.lstCtrl.GetCategoryInfo(_part.Category);
                if (source != null)
                    foreach (var entry in source) if (entry.Value != null) options.Add(entry.Value);
                options.Sort((a,b) => {
                    int order = StringComparer.CurrentCultureIgnoreCase.Compare(a.Name,b.Name);
                    return order == 0 ? a.Id.CompareTo(b.Id) : order;
                });
                _optionCache.Add(cacheKey, options);
            }
            foreach (var option in options)
            {
                _optionNames[option.Id] = option.Name;
                if (string.IsNullOrEmpty(_search) || (option.Name ?? "").IndexOf(_search, StringComparison.CurrentCultureIgnoreCase) >= 0
                    || option.Id.ToString(CultureInfo.InvariantCulture).IndexOf(_search, StringComparison.Ordinal) >= 0)
                    _filteredOptions.Add(option);
            }
            _optionPage = Mathf.Clamp(_optionPage, 0, Math.Max(0, (_filteredOptions.Count - 1) / OptionsPerPage));
            PrepareTiles();
        }
        private void PrepareTiles()
        {
            int start = _optionPage * OptionsPerPage;
            int count = Math.Min(OptionsPerPage, _filteredOptions.Count - start);
            _tiles = new GUIContent[Math.Max(0, count)];
            for (int i=0; i<_tiles.Length; i++)
            {
                var option = _filteredOptions[start+i];
                string bundle = option.GetInfo(ChaListDefine.KeyType.ThumbAB);
                string asset = option.GetInfo(ChaListDefine.KeyType.ThumbTex);
                string key = bundle + ":" + asset;
                if (!_thumbCache.TryGetValue(key, out var texture))
                {
                    texture = null;
                    if (!string.IsNullOrEmpty(bundle) && !string.IsNullOrEmpty(asset))
                        try { texture = CommonLib.LoadAsset<Texture2D>(bundle, asset, false, ""); }
                        catch { /* An unavailable thumbnail must not make a registered part unselectable. */ }
                    _thumbCache[key] = texture;
                }
                _tiles[i] = new GUIContent(option.Name, texture, option.Name + " (" + option.Id + ")");
            }
        }
        private void DrawAppearance()
        {
            var edit = _appearanceEdits[_selected];
            GUILayout.BeginHorizontal();
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && edit.CanUndo;
            if (GUILayout.Button("パーツ・色を元に戻す", GUILayout.Width(168), GUILayout.Height(28)))
                _pendingUi = () => { if (edit.Undo()) { _colorText.Clear(); _status = "パーツ・色の変更を1つ戻しました。"; } };
            GUI.enabled = enabled && edit.CanRedo;
            if (GUILayout.Button("やり直す", GUILayout.Width(90), GUILayout.Height(28)))
                _pendingUi = () => { if (edit.Redo()) { _colorText.Clear(); _status = "パーツ・色の変更をやり直しました。"; } };
            GUI.enabled = enabled;
            GUILayout.EndHorizontal();

            if (!edit.CanCommit)
            {
                GUILayout.Label("編集対象が変更・破棄されました。閉じてから開き直してください。", GUILayout.ExpandHeight(true));
                return;
            }

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("変更する部位");
            int col = 0;
            int columns = Math.Max(1, Math.Min(4, (int)(_window.width - 44) / 146));
            foreach (var part in _parts)
            {
                if (part.Page != _page) continue;
                if (col == 0) GUILayout.BeginHorizontal();
                var selectedPart = part;
                if (GUILayout.Toggle(_part == part, part.Name, GUI.skin.button, GUILayout.Width(142), GUILayout.Height(28)) && _part != part)
                    _pendingUi = () => SelectPart(selectedPart);
                if (++col == columns) { GUILayout.EndHorizontal(); col=0; }
            }
            if (col != 0) GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            var actor = _actors[_selected];
            if (_part != null && _part.Read != null)
            {
                int current = _part.Read(actor);
                string currentName = _optionNames.TryGetValue(current,out var name) ? name : "一覧にないパーツ ("+current+")";
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label(_part.Name + "：" + currentName, new GUIStyle(GUI.skin.label) { wordWrap=true });
                GUILayout.BeginHorizontal();
                GUILayout.Label("名前・ID検索", GUILayout.Width(100));
                GUI.SetNextControlName("hcm-part-search");
                string search = GUILayout.TextField(_search, GUILayout.MinWidth(60), GUILayout.MaxWidth(250));
                if (search != _search) _pendingUi = () => { _search=search; _optionPage=0; PrepareOptions(); };
                if (GUILayout.Button("検索クリア", GUILayout.Width(100))) _pendingUi=()=> { _search=""; _optionPage=0; PrepareOptions(); };
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUI.enabled = enabled && _optionPage>0;
                if (GUILayout.Button("前へ", GUILayout.Width(70))) _pendingUi=()=> { _optionPage--; PrepareTiles(); };
                GUI.enabled = enabled;
                GUILayout.Label((_optionPage+1)+" / "+Math.Max(1,(_filteredOptions.Count+OptionsPerPage-1)/OptionsPerPage)
                    +" ページ　"+_filteredOptions.Count+" 件", GUILayout.Width(240));
                GUI.enabled = enabled && (_optionPage+1)*OptionsPerPage<_filteredOptions.Count;
                if (GUILayout.Button("次へ", GUILayout.Width(70))) _pendingUi=()=> { _optionPage++; PrepareTiles(); };
                GUI.enabled=enabled;
                GUILayout.EndHorizontal();
                var tileStyle = new GUIStyle(GUI.skin.button) { imagePosition=ImagePosition.ImageAbove, wordWrap=true, fontSize=12 };
                for (int row=0; row<(_tiles.Length+columns-1)/columns; row++)
                {
                    GUILayout.BeginHorizontal();
                    for (int column=0; column<columns; column++)
                    {
                        int index=row*columns+column;
                        if (index>=_tiles.Length) break;
                        int id = _filteredOptions[_optionPage*OptionsPerPage+index].Id;
                        Color previous = GUI.backgroundColor;
                        if (id == current) GUI.backgroundColor = new Color(0.35f,0.8f,0.65f);
                        bool choose = GUILayout.Toggle(id==current, _tiles[index], tileStyle, GUILayout.Width(142), GUILayout.Height(92));
                        GUI.backgroundColor=previous;
                        if (choose && id!=current)
                        {
                            var part = _part;
                            _pendingUi=()=> {
                                if (!edit.Preview(part.Key, ()=>part.Read(actor), v=>part.Write(actor,v), id))
                                    throw new InvalidOperationException("編集対象またはHの許可が終了しています。");
                                _status=part.Name+"を変更しました。"; _colorText.Clear();
                            };
                        }
                    }
                    GUILayout.EndHorizontal();
                }
                if (_filteredOptions.Count==0) GUILayout.Label("該当するパーツがありません。検索条件を変えてください。");
                GUILayout.EndVertical();
            }
            if (_part != null)
                foreach (var color in _part.Colors) DrawAppearanceColor(actor,edit,color);
            GUILayout.EndScrollView();
        }
        private void DrawAppearanceColor(ChaControl actor, ValueEditSession edit, AppearanceColor field)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            var color = field.Read(actor);
            GUILayout.BeginHorizontal();
            GUILayout.Label(field.Name, GUILayout.Width(140));
            Color old = GUI.color; GUI.color=color;
            GUILayout.Box(Texture2D.whiteTexture, GUILayout.Width(52), GUILayout.Height(22)); GUI.color=old;
            GUILayout.Label("RGBA 0～255（濃さはA）",GUILayout.Width(240));
            GUILayout.EndHorizontal();
            for (int i=0; i<4; i++)
            {
                int channel=i;
                float value=color[i]*255;
                string key=field.Key+":"+i;
                if (!_colorText.TryGetValue(key,out var text)) { text=value.ToString("0.##",CultureInfo.InvariantCulture); _colorText[key]=text; }
                GUILayout.BeginHorizontal();
                GUILayout.Label(new[] { "R 赤", "G 緑", "B 青", "A 濃さ" }[i],GUILayout.Width(65));
                float next=GUILayout.HorizontalSlider(value,Math.Min(0,value),Math.Max(255,value),GUILayout.MinWidth(60),GUILayout.MaxWidth(330));
                GUI.SetNextControlName("hcm-color-"+key);
                string typed=GUILayout.TextField(text,GUILayout.Width(80));
                bool changed=ShapeEditSession.Finite(next) && next!=value;
                if (typed!=text)
                {
                    _colorText[key]=typed;
                    changed=float.TryParse(typed,NumberStyles.Float,CultureInfo.InvariantCulture,out next) && ShapeEditSession.Finite(next);
                }
                else if (changed) _colorText[key]=next.ToString("0.##",CultureInfo.InvariantCulture);
                if (changed)
                {
                    var desired=color; desired[channel]=next/255f;
                    try
                    {
                        if (!edit.Preview(field.Key,()=>field.Read(actor),v=>field.Write(actor,v),desired,true))
                            _status="色の変更が拒否されました。";
                        color=field.Read(actor);
                    }
                    catch (Exception ex) { _status="色の反映失敗："+ex.Message; }
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
        }
        private void BreakAppearanceMerge()
        { foreach (var edit in _appearanceEdits.Values) edit.BreakMerge(); }
        private void ClearAppearance()
        {
            _appearanceEdits.Clear(); _optionCache.Clear(); _thumbCache.Clear(); _colorText.Clear();
            _filteredOptions.Clear(); _optionNames.Clear(); _tiles=Array.Empty<GUIContent>();
            _part=null; _search=""; _optionPage=0;
        }
    }
}
