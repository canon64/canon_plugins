using System;
using System.Reflection;
using HarmonyLib;

namespace MainGameBlankMapAdd
{
    // UIから切り離したksPlug時間設定への接続。対象は起動した通常版／VR版だけ。
    internal sealed class KsPlugTimeControl
    {
        private readonly string _settingsTypeName;
        private FieldInfo _enabled;
        private Action _save;
        internal string Edition { get; }

        internal KsPlugTimeControl(bool vr)
        {
            Edition = vr ? "VR版" : "通常版";
            _settingsTypeName = (vr ? "ksPlugVR" : "ksPlug") + ".Settings";
        }

        private void Resolve()
        {
            if (_enabled != null && _save != null) return;
            var type = AccessTools.TypeByName(_settingsTypeName);
            if (type == null) throw new InvalidOperationException(Edition + "のksPlugが見つかりません。");
            var field = AccessTools.Field(type, "TimePassesInH");
            var save = AccessTools.DeclaredMethod(type, "Save", Type.EmptyTypes);
            if (field == null || !field.IsStatic || field.FieldType != typeof(bool)
                || save == null || !save.IsStatic || save.ReturnType != typeof(void))
                throw new InvalidOperationException("ksPlugの時間設定・保存APIを確認できません。");
            var saveAction = (Action)Delegate.CreateDelegate(typeof(Action), save);
            _enabled = field;
            _save = saveAction;
        }

        internal bool TryRead(out bool enabled, out string error)
        {
            try
            {
                Resolve();
                enabled = (bool)_enabled.GetValue(null);
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                enabled = false;
                error = ex.GetBaseException().Message;
                return false;
            }
        }

        internal bool TrySet(bool enabled, out string error)
        {
            if (!TryRead(out bool original, out error)) return false;
            try
            {
                _enabled.SetValue(null, enabled);
                _save(); // ksPlug自身の保存処理を使い、ネイティブ設定画面と同期する。
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.GetBaseException().Message;
                try { _enabled.SetValue(null, original); }
                catch (Exception rollback) { error += " / 元の値への復元にも失敗：" + rollback.GetBaseException().Message; }
                return false;
            }
        }
    }
}
