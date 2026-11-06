using System;
using System.Collections.Generic;
using BepInEx.Configuration;

namespace MainGameCameraLookControl
{
    internal sealed class FloatSetting
    {
        internal readonly string Group, Label, Help;
        internal readonly float Minimum, Maximum, Default;
        internal readonly ConfigEntry<float> Entry;

        internal FloatSetting(ConfigFile file, string group, string key, string label, string help,
            float value, float minimum, float maximum)
        {
            Group = group; Label = label; Help = help;
            Default = value; Minimum = minimum; Maximum = maximum;
            Entry = file.Bind(group, key, value, new ConfigDescription(help, new AcceptableValueRange<float>(minimum, maximum)));
        }

        internal float Value
        {
            get => LookPolicy.Sanitize(Entry.Value, Default, Minimum, Maximum);
            set => Entry.Value = LookPolicy.Sanitize(value, Default, Minimum, Maximum);
        }
    }

    internal sealed class LookSettings
    {
        internal readonly List<FloatSetting> Rows = new List<FloatSetting>();
        internal readonly ConfigEntry<bool> Enabled, WindowVisible, Diagnostics;
        private readonly FloatSetting[] _neck, _head;
        private readonly FloatSetting _pitchGain, _yawGain, _upBias, _rightBias, _speed;
        private readonly FloatSetting _upField, _downField, _leftField, _rightField;

        internal LookSettings(ConfigFile file, Action openWindow)
        {
            Enabled = file.Bind("General", "Enabled", true, "女性キャラが標準のカメラ注視を行っている間だけ、首・頭の曲げ方を補正します。注視を強制的にONにはしません。");
            WindowVisible = file.Bind("UI", "Settings window", false, new ConfigDescription("設定窓を開きます。全場面共通。", null,
                new ConfigurationManager.ConfigurationManagerAttributes { CustomDrawer = entry => openWindow() }));
            Diagnostics = file.Bind("Diagnostics", "Log state changes", false, "対象・パターンの変化だけをログ出力します。通常はOFF。");
            _neck = BoneRows(file, "Neck", "首", 10f);
            _head = BoneRows(file, "Head", "頭", 20f);
            _pitchGain = Add(file, "Aim", "Pitch gain", "X 上下の追従倍率", "0～2倍。1で標準、0で上下の注視角度を0にします。", 1f, 0f, 2f);
            _yawGain = Add(file, "Aim", "Yaw gain", "Y 左右の追従倍率", "0～2倍。1で標準。例えば30°に対して0.5倍なら15°を要求します。", 1f, 0f, 2f);
            _upBias = Add(file, "Aim", "Up bias (deg)", "X 上向き補正 °", "±45°。正数で上を見る要求角度を増やします。実際の回転は各骨の可動域で制限されます。", 0f, -45f, 45f);
            _rightBias = Add(file, "Aim", "Right bias (deg)", "Y 右向き補正 °", "±45°。正数でキャラ基準の右を見る要求角度を増やします。", 0f, -45f, 45f);
            _speed = Add(file, "Aim", "Speed multiplier", "追従速度倍率", "0.1～5倍。標準の補間速度に掛けます。1で標準、2で速く、0.5でゆっくり追います。", 1f, 0.1f, 5f);
            _upField = Add(file, "Field", "Up extra (deg)", "上方の追従範囲追加 °", "±180°。対象が上へ移った時に注視をやめる境界を広げます。初期値+30°。骨の可動域とは別です。", 30f, -180f, 180f);
            _downField = Add(file, "Field", "Down extra (deg)", "下方の追従範囲追加 °", "±180°。0で下方の標準範囲を維持します。実効値0～180°。", 0f, -180f, 180f);
            _leftField = Add(file, "Field", "Left extra (deg)", "左方の追従範囲追加 °", "±180°。0で左方の標準範囲を維持します。実効値0～180°。", 0f, -180f, 180f);
            _rightField = Add(file, "Field", "Right extra (deg)", "右方の追従範囲追加 °", "±180°。0で右方の標準範囲を維持します。実効値0～180°。", 0f, -180f, 180f);
        }

        private FloatSetting Add(ConfigFile file, string group, string key, string label, string help, float value, float min, float max)
        {
            var row = new FloatSetting(file, group, key, label, help, value, min, max);
            Rows.Add(row);
            return row;
        }

        private FloatSetting[] BoneRows(ConfigFile file, string group, string label, float upDefault)
        {
            return new[]
            {
                Add(file, group, "Up extra (deg)", label + " X 上向き追加 °", "±90°。正数で上向き可動域を広げ、負数で狭めます。例: 標準20°に+10°なら30°。実効値0～100°。", upDefault, -90f, 90f),
                Add(file, group, "Down extra (deg)", label + " X 下向き追加 °", "±90°。0で標準。正数で下向き可動域を拡張します。実効値0～100°。", 0f, -90f, 90f),
                Add(file, group, "Left extra (deg)", label + " Y 左向き追加 °", "±90°。0で標準。正数で左向き可動域を拡張します。実効値0～100°。", 0f, -90f, 90f),
                Add(file, group, "Right extra (deg)", label + " Y 右向き追加 °", "±90°。0で標準。正数で右向き可動域を拡張します。実効値0～100°。", 0f, -90f, 90f),
                Add(file, group, "Roll (deg)", label + " Z 固定傾き °", "±60°。0で追加なし。正数はキャラ自身の左へ傾けます。首の傾きは頭へ引き継ぎ、頭は注視方向を維持します。", 0f, -60f, 60f),
                Add(file, group, "Roll from yaw", label + " Z 左右注視との連動", "-1～1。注視の左右角度×この値をZへ加えます。例: 右30°で0.25なら+7.5°。固定値との合計を±60°へ制限します。", 0f, -1f, 1f)
            };
        }

        private static BonePolicy Bone(FloatSetting[] rows) => new BonePolicy
        {
            Up = rows[0].Value, Down = rows[1].Value, Left = rows[2].Value,
            Right = rows[3].Value, Roll = rows[4].Value, RollFromYaw = rows[5].Value
        };

        internal LookPolicy Snapshot() => new LookPolicy
        {
            Neck = Bone(_neck), Head = Bone(_head), PitchGain = _pitchGain.Value, YawGain = _yawGain.Value,
            UpBias = _upBias.Value, RightBias = _rightBias.Value, Speed = _speed.Value,
            UpField = _upField.Value, DownField = _downField.Value, LeftField = _leftField.Value, RightField = _rightField.Value
        };

        internal void Normalize() { foreach (var row in Rows) row.Value = row.Value; }
        internal void Reset() { foreach (var row in Rows) row.Value = row.Default; }
        internal void Standard() { foreach (var row in Rows) row.Value = row == _pitchGain || row == _yawGain || row == _speed ? 1f : 0f; }
    }
}

namespace ConfigurationManager
{
    internal sealed class ConfigurationManagerAttributes
    {
        public Action<ConfigEntryBase> CustomDrawer;
    }
}
