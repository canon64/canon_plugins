using System;
using System.Collections.Generic;
using System.Linq;

namespace MainGameAiShoujoMode
{
    internal sealed class HotelRoomRestoreBatch
    {
        private readonly Func<bool> _enabled;
        private readonly Action<string> _log;
        private readonly string _file;
        private string _id, _logFailure;
        private readonly List<string> _success = new List<string>(), _failed = new List<string>(), _reasons = new List<string>();
        internal HotelRoomRestoreBatch(Func<bool> enabled, Action<string> log, string file)
        { _enabled = enabled; _log = log; _file = file; Observe("begin", () => "read-source=" + file); }
        internal bool Succeeded => !_failed.Any();
        internal bool HasNotice => !Succeeded || _logFailure != null;
        internal string Status => (_failed.Any() ? (_success.Any() ? "復元が一部失敗" : "復元が全て失敗") : "復元完了")
            + "\n適用済み：" + string.Join("、", _success) + "\n失敗：" + string.Join("\n", _reasons)
            + (_logFailure == null ? "" : "\n診断記録失敗：" + _logFailure);
        internal void Observe(string stage, Func<string> details)
        {
            if (_enabled?.Invoke() != true || _log == null) return;
            try
            {
                if (_id == null) _id = Guid.NewGuid().ToString("N");
                _log("HOTEL_ROOM_RESTORE id=" + _id + " file=" + _file + " stage=" + stage + " " + details());
            }
            catch (Exception ex) { _logFailure = ex.GetBaseException().Message; }
        }
        internal bool Item<T>(string item, string key, string target, Func<T> read, Action<T> apply,
            Func<T, bool> verify = null, Func<T, string> summary = null, Func<string> actual = null, Func<Action> checkpoint = null)
        {
            string stage = "read-validate"; Action rollback = null;
            try
            {
                var value = read();
                Observe("read", () => "item=" + item + " key=" + key + " target=" + target + " saved=" + (summary?.Invoke(value) ?? "present"));
                stage = "checkpoint"; rollback = checkpoint?.Invoke();
                stage = "apply"; apply(value);
                stage = "verify";
                if (verify != null && !verify(value)) throw new InvalidOperationException("適用後の値が保存値と一致しません。");
                _success.Add(item);
                Observe("applied", () => "result=success item=" + item + " key=" + key + " target=" + target + " actual=" + (actual?.Invoke() ?? summary?.Invoke(value) ?? "applied"));
                return true;
            }
            catch (Exception ex)
            {
                string reason = ex.GetBaseException().Message;
                if (rollback != null)
                    try { rollback(); }
                    catch (Exception restoreError) { reason += " / 元の状態への復帰にも失敗：" + restoreError.GetBaseException().Message; }
                _failed.Add(item); _reasons.Add(item + "（" + stage + "）：" + reason);
                Observe(stage, () => "result=failure item=" + item + " key=" + key + " target=" + target + " reason=" + reason.Replace('\n', ' ').Replace('\r', ' ')
                    + " actual=" + SafeActual(actual));
                return false;
            }
        }
        private static string SafeActual(Func<string> actual)
        { try { return actual?.Invoke() ?? "unavailable"; } catch (Exception ex) { return "unavailable:" + ex.GetBaseException().Message; } }
        internal void Failure(string item, string key, Exception ex)
        { Item<bool>(item, key, "unapplied", () => throw ex, _ => { }); }
        internal bool Finish()
        {
            Observe("complete", () => "result=" + (Succeeded ? "success" : _success.Any() ? "partial-failure" : "failure")
                + " success=[" + string.Join(",", _success) + "] failed=[" + string.Join(",", _failed) + "]");
            return Succeeded;
        }
    }
}
