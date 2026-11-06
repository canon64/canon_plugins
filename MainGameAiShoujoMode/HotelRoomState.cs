using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Linq;

namespace MainGameAiShoujoMode
{
    [DataContract] public sealed class HotelRoomSelection
    {
        [DataMember] public string mode, name, bundle, asset;
        [DataMember] public int id;
        public bool Matches(HotelRoomSelection other) => other != null && mode == other.mode && id == other.id
            && name == other.name && bundle == other.bundle && asset == other.asset;
    }
    [DataContract] public sealed class HotelRoomPlacement
    {
        [DataMember] public float x, y, z, yaw;
        public HotelRoomPlacement Copy() => (HotelRoomPlacement)MemberwiseClone();
        public void Validate()
        {
            foreach (var value in new[] { x, y, z, yaw })
                if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidDataException("位置・向きには有限の数値を入力してください。");
        }
    }
    [DataContract] public sealed class HotelRoomState
    {
        [DataMember] public string version = "v1", room;
        [DataMember(EmitDefaultValue = false)] public HotelRoomSelection selection;
        [DataMember(EmitDefaultValue = false)] public HotelRoomPlacement placement;
        [DataMember(EmitDefaultValue = false)] public HotelRoomGadget[] gadgets;
        [DataMember(EmitDefaultValue = false)] public HotelRoomLighting lighting;
        [DataMember(EmitDefaultValue = false)] public HotelRoomAppearance appearance;
        [DataMember(EmitDefaultValue = false)] public string receptionistPose;
        [DataMember(EmitDefaultValue = false)] public HotelReceptionistPlacement receptionistPlacement;
        public void Validate(string expected)
        {
            if (version != "v1" || room != expected) throw new InvalidDataException("客室の状態データが不正です。保存ファイルは保持しています。");
            if (selection != null && (string.IsNullOrWhiteSpace(selection.mode) || string.IsNullOrWhiteSpace(selection.name) || selection.id < default(int)))
                throw new InvalidDataException("客室の体位データが不正です。");
            placement?.Validate();
            receptionistPlacement?.Validate();
            lighting?.Validate();
            if (gadgets != null && (gadgets.Any(g => g == null || string.IsNullOrWhiteSpace(g.guid) || string.IsNullOrWhiteSpace(g.payload))
                || gadgets.Select(g => g.guid).Distinct().Count() != gadgets.Length)) throw new InvalidDataException("ガジェットの保存データが不正です。");
        }
    }
    [DataContract] public sealed class HotelReceptionistPlacement
    {
        [DataMember] public float x, y, z, rx, ry, rz;
        public HotelReceptionistPlacement Copy() => (HotelReceptionistPlacement)MemberwiseClone();
        public void Validate()
        {
            if (new[] { x, y, z, rx, ry, rz }.Any(value => float.IsNaN(value) || float.IsInfinity(value)))
                throw new InvalidDataException("受付嬢の位置・角度には有限の数値を指定してください。");
        }
    }
    [DataContract] public sealed class HotelRoomGadget
    {
        [DataMember] public string guid, payload;
    }
    [DataContract] public sealed class HotelRoomLighting
    {
        [DataMember] public float light, emission, bloom;
        [DataMember(EmitDefaultValue = false)] public HotelRoomLightSource[] sources;
        public HotelRoomLighting Copy() => new HotelRoomLighting { light = light, emission = emission, bloom = bloom,
            sources = sources?.Select(source => source.Copy()).ToArray() };
        public void Validate()
        {
            if (new[] { light, emission, bloom }.Any(v => float.IsNaN(v) || float.IsInfinity(v) || v < default(float))) throw new InvalidDataException("照明の設定が不正です。");
            if (sources != null)
            {
                if (sources.Any(source => source == null) || sources.Select(source => source.key).Distinct().Count() != sources.Length)
                    throw new InvalidDataException("個別照明の保存データが重複または空です。");
                foreach (var source in sources) source.Validate();
            }
        }
    }
    [DataContract] public sealed class HotelRoomLightSource
    {
        [DataMember] public string key;
        [DataMember] public float r, g, b, strength;
        public HotelRoomLightSource Copy() => (HotelRoomLightSource)MemberwiseClone();
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(key) || new[] { r, g, b, strength }.Any(v => float.IsNaN(v) || float.IsInfinity(v) || v < default(float)))
                throw new InvalidDataException("個別照明の色・明るさが不正です。");
        }
    }
    [DataContract] public sealed class HotelRoomAppearance
    {
        [DataMember] public string source, female, male;
        [DataMember(EmitDefaultValue = false)] public string format;
        [DataMember] public HotelRoomPreference[] preferences;
        [DataMember(EmitDefaultValue = false)] public HotelRoomActorEffect[] effects;
        internal bool CanRestoreCard(string room) => room != "201" || format == "resolved-v2";
    }
    [DataContract] public sealed class HotelRoomPreference { [DataMember] public string key, value; }
    [DataContract] public sealed class HotelRoomActorEffect { [DataMember] public bool female; [DataMember] public string key, value; }
    public sealed class HotelRoomStateStore
    {
        private readonly string _path, _room;
        internal string FilePath => _path;
        private HotelRoomState _working;
        internal void BeginSave()
        {
            if (_working != null) throw new InvalidOperationException("客室保存が既に実行中です。");
            _working = Load();
        }
        internal void CommitSave() { if (_working != null) WriteFile(_working); }
        internal void EndSave() { _working = null; }
        public HotelRoomStateStore(string directory, string room)
        {
            if (room != "reception-suite" && !Array.Exists(HotelRoomCards.Rooms, r => r == room)) throw new ArgumentException("客室番号が不正です。");
            _room = room; _path = Path.Combine(directory, room + ".json");
        }
        public HotelRoomState Load()
        {
            if (_working != null)
                return new HotelRoomState { room = _working.room, version = _working.version, selection = _working.selection,
                    placement = _working.placement, gadgets = _working.gadgets, lighting = _working.lighting,
                    receptionistPose = _working.receptionistPose, receptionistPlacement = _working.receptionistPlacement,
                    appearance = _working.appearance == null ? null : new HotelRoomAppearance {
                        source = _working.appearance.source, female = _working.appearance.female, male = _working.appearance.male,
                        format = _working.appearance.format, preferences = _working.appearance.preferences, effects = _working.appearance.effects } };
            if (!File.Exists(_path)) return new HotelRoomState { room = _room };
            using (var stream = File.OpenRead(_path))
            {
                var data = (HotelRoomState)new DataContractJsonSerializer(typeof(HotelRoomState)).ReadObject(stream);
                if (data == null) throw new InvalidDataException("客室の状態データが空です。");
                data.Validate(_room); return data;
            }
        }
        public void SaveSelection(HotelRoomSelection value) { var data = Load(); data.selection = value; Save(data); }
        public void SavePlacement(HotelRoomPlacement value) { var data = Load(); data.placement = value; Save(data); }
        public void SaveGadgets(HotelRoomGadget[] value) { var data = Load(); data.gadgets = value; Save(data); }
        public void SaveLighting(HotelRoomLighting value) { var data = Load(); data.lighting = value; Save(data); }
        public void SaveAppearance(HotelRoomAppearance value) { var data = Load(); data.appearance = value; Save(data); }
        internal void SaveGadget(HotelRoomGadget value)
        {
            var data = Load();
            data.gadgets = (data.gadgets ?? Array.Empty<HotelRoomGadget>()).Where(g => g.guid != value.guid).Concat(new[] { value }).ToArray();
            Save(data);
        }
        internal void SaveActor(string source, bool female, string card)
        {
            var data = Load();
            var appearance = data.appearance ?? new HotelRoomAppearance();
            appearance.source = source;
            if (female) { appearance.female = card; appearance.format = "resolved-v2"; }
            else appearance.male = card;
            data.appearance = appearance; Save(data);
        }
        internal void SavePreference(string source, HotelRoomPreference value)
        {
            var data = Load(); var appearance = data.appearance ?? new HotelRoomAppearance();
            appearance.source = source;
            appearance.preferences = (appearance.preferences ?? Array.Empty<HotelRoomPreference>()).Where(p => p.key != value.key).Concat(new[] { value }).ToArray();
            data.appearance = appearance; Save(data);
        }
        internal void SaveActorEffect(string source, HotelRoomActorEffect value)
        {
            var data = Load(); var appearance = data.appearance ?? new HotelRoomAppearance();
            appearance.source = source;
            appearance.effects = (appearance.effects ?? Array.Empty<HotelRoomActorEffect>())
                .Where(p => p.female != value.female || p.key != value.key).Concat(new[] { value }).ToArray();
            data.appearance = appearance; Save(data);
        }
        public void SaveReceptionistPose(string value)
        {
            if (_room != "reception" || string.IsNullOrWhiteSpace(value)) throw new InvalidDataException("受付嬢の待機ポーズが不正です。");
            var data = Load(); data.receptionistPose = value; Save(data);
        }
        public void SaveReceptionistPlacement(HotelReceptionistPlacement value)
        {
            if (_room != "reception" || value == null) throw new InvalidDataException("受付嬢の位置・角度が不正です。");
            var data = Load(); data.receptionistPlacement = value.Copy(); Save(data);
        }
        private void Save(HotelRoomState data)
        {
            data.Validate(_room);
            if (_working != null) { _working = data; return; }
            WriteFile(data);
        }
        private void WriteFile(HotelRoomState data)
        {
            data.Validate(_room); Directory.CreateDirectory(Path.GetDirectoryName(_path));
            string temporary = _path + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                { new DataContractJsonSerializer(typeof(HotelRoomState)).WriteObject(stream, data); stream.Flush(true); }
                if (File.Exists(_path)) File.Replace(temporary, _path, _path + ".bak"); else File.Move(temporary, _path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
