using System;
using System.IO;
using System.Linq;
using MapEnvironmentRuntime;
using UnityEngine;

namespace MainGameAiShoujoMode
{
    internal sealed class HotelRoomCardPanel : IDisposable
    {
        private readonly Parameters _p;
        private readonly HotelRoomCards _store;
        private readonly Action _changed;
        private readonly Action<string> _start;
        internal Action<string> Saved;
        internal void SelectRoom(string room) { Select(room); }
        private static string Label(string room) => room == "reception" ? "受付嬢" : room + "号室";
        private AiCardPicker _picker;
        private HotelRoomCardsDocument _data;
        private string _room = HotelRoomCards.Rooms.First(), _draft, _status;
        private string _drawRoom;
        private Vector2 _scroll;
        private bool _readonly;
        internal bool Visible { get; private set; }
        internal Rect InputRect => new Rect(_p.N("math.zero"), _p.N("math.zero"), Screen.width, Screen.height);
        internal HotelRoomCardPanel(Parameters p, HotelRoomCards store, Action changed, Action<string> start)
        { _p = p; _store = store; _changed = changed; _start = start; Reload(); }
        internal HotelRoomCard Registered(string room) => _store.Load().rooms.FirstOrDefault(r => r.room == room);
        internal string ErrorStatus { get; private set; }
        internal void ShowError(Exception ex) { ErrorStatus = ex.GetBaseException().Message; Failure(ex); }
        private void Reload()
        {
            try { _data = _store.Load(); _readonly = false; Select(_room); _status = "部屋を選び、カード画像を選択して「登録を保存」を押してください。"; }
            catch (Exception ex) { _readonly = true; _data = new HotelRoomCardsDocument(); Failure(ex); }
        }
        private void Select(string room)
        { _room = room; _draft = _data.rooms.FirstOrDefault(r => r.room == room)?.card; }
        internal void SetVisible(bool shown)
        {
            Visible = shown;
            if (shown)
            {
                Reload();
                if (!string.IsNullOrWhiteSpace(ErrorStatus)) _status = ErrorStatus;
                if (_picker == null) _picker = new AiCardPicker(_p, _p.T("animation.card.directory"), Failure);
            }
            else _draft = null;
            _changed();
        }
        internal void Draw()
        {
            if (!Visible) return;
            if (Event.current.type == EventType.Layout) _drawRoom = _room;
            GUI.Box(InputRect, GUIContent.none);
            float margin = _p.N("aiMode.help.x");
            GUILayout.BeginArea(new Rect(margin, margin, Screen.width - margin * _p.N("math.two"), Screen.height - margin * _p.N("math.two")));
            GUILayout.Label("ホテルメニュー ＞ 客室・受付嬢のカード登録");
            if (GUILayout.Button("閉じる（未保存の選択は破棄）", GUILayout.ExpandWidth(false))) SetVisible(false);
            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.BeginVertical(GUI.skin.box);
            foreach (var room in HotelRoomCards.Rooms)
            {
                var saved = _data.rooms.FirstOrDefault(r => r.room == room);
                string label = (_drawRoom == room ? "● " : "○ ") + Label(room) + "　" + (saved?.name ?? (saved == null ? "未登録" : Path.GetFileNameWithoutExtension(saved.card)));
                if (GUILayout.Button(label, GUILayout.ExpandWidth(false))) Select(room);
            }
            GUILayout.EndVertical();
            GUILayout.Label(_status ?? "");
            GUILayout.Label(Label(_drawRoom) + "の選択：" + (string.IsNullOrWhiteSpace(_draft) ? "未登録" : Path.GetFileNameWithoutExtension(_draft)));
            bool enabled = GUI.enabled; GUI.enabled = enabled && !_readonly;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("登録を保存", GUILayout.ExpandWidth(false))) Save();
            if (GUILayout.Button("登録を解除して保存", GUILayout.ExpandWidth(false))) { _draft = null; Save(); }
            GUILayout.EndHorizontal(); GUI.enabled = enabled;
            if (_drawRoom != "reception" && GUILayout.Button("保存済みの女の子で、この客室のフリーHを開始", GUILayout.ExpandWidth(false))) _start(_room);
            if (GUILayout.Button("保存データを再読込", GUILayout.ExpandWidth(false))) Reload();
            _picker?.Draw(_draft, path => _draft = path);
            GUILayout.Label("202〜206号室も登録できます。内装が未作成の部屋は、登録しても入室できません。");
            GUILayout.EndScrollView(); GUILayout.EndArea();
        }
        private void Save()
        {
            try
            {
                string name = null;
                if (!string.IsNullOrWhiteSpace(_draft))
                {
                    if (!File.Exists(_draft)) throw new FileNotFoundException("カードが見つかりません。", _draft);
                    var card = new ChaFileControl();
                    if (!card.LoadCharaFile(_draft) || card.parameter.sex != _p.I("math.one")) throw new InvalidDataException("KKSの女性キャラカードを選択してください。");
                    name = card.parameter.fullname;
                }
                _store.Set(_room, _draft, name); _data = _store.Load();
                ErrorStatus = null;
                _status = Label(_room) + "の" + (string.IsNullOrWhiteSpace(_draft) ? "登録を解除しました。" : "登録を保存しました：" + name);
                Saved?.Invoke(_room);
            }
            catch (Exception ex) { Failure(ex); }
        }
        private void Failure(Exception ex) { _status = ex.GetBaseException().Message; }
        public void Dispose() { Visible = false; _picker?.Dispose(); _picker = null; }
    }
}
