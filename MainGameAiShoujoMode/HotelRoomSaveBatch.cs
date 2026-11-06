using System;
using System.Collections.Generic;
using System.Linq;

namespace MainGameAiShoujoMode
{
    // A save operation owns failures per item. Logging is lazy and never affects persistence.
    internal sealed class HotelRoomSaveBatch
    {
        private readonly Func<bool> _enabled;
        private readonly Action<string> _log;
        private readonly string _file, _trigger;
        private string _id;
        private string _diagnosticFailure;
        private Action _commit, _close;
        private Exception _openError;
        private readonly List<Action> _committed = new List<Action>();
        private readonly List<Tuple<string, string>> _pending = new List<Tuple<string, string>>();
        internal void BeginFile(Action open, Action commit, Action close)
        {
            _commit = commit; _close = close;
            try { open(); } catch (Exception ex) { _openError = ex; }
        }
        private readonly List<string> _success = new List<string>(), _failed = new List<string>(), _reasons = new List<string>();
        internal HotelRoomSaveBatch(Func<bool> enabled, Action<string> log, string file, string trigger)
        { _enabled = enabled; _log = log; _file = file; _trigger = trigger; Trace(() => "result=begin"); }
        internal bool Succeeded => !_failed.Any();
        internal bool HasNotice => !Succeeded || _diagnosticFailure != null;
        internal string FailureNotice => (Succeeded ? "保存完了・診断記録に失敗" : _success.Any() ? "一部保存失敗" : "保存失敗") + "（成功 " + _success.Count + " / 失敗 " + _failed.Count
            + "）。成功した項目は保存済み、失敗項目は以前の保存値を保持しました。\n" + string.Join("\n", _reasons)
            + (_diagnosticFailure == null ? "" : "\n診断ログを記録できません：" + _diagnosticFailure);
        internal string Status => (_failed.Any() ? (_success.Any() ? "一部保存失敗。" : "保存失敗。") : "保存完了。")
            + "成功：" + string.Join("、", _success) + (_failed.Any() ? "\n失敗（以前の保存値を保持）：" + string.Join(" / ", _reasons) : "")
            + (_diagnosticFailure == null ? "" : "\n診断ログを記録できません：" + _diagnosticFailure);
        private void Trace(Func<string> message)
        {
            if (_enabled?.Invoke() != true || _log == null) return;
            try
            {
                if (_id == null) _id = Guid.NewGuid().ToString("N");
                _log("HOTEL_ROOM_SAVE id=" + _id + " trigger=" + _trigger + " file=" + _file + " " + message());
            }
            catch (Exception ex) { _diagnosticFailure = ex.GetBaseException().Message; }
        }
        internal bool Item<T>(string item, string key, Func<T> capture, Action<T> persist, Func<T, string> summary = null, Action<T> onCommitted = null)
        {
            string stage = "capture";
            try
            {
                var value = capture();
                stage = "read-existing";
                if (_openError != null) throw _openError;
                stage = _commit == null ? "write" : "validate-stage";
                persist(value);
                Action completed = () =>
                {
                    _success.Add(item);
                    try { onCommitted?.Invoke(value); }
                    catch (Exception ex) { _diagnosticFailure = "保存後の状態更新：" + ex.GetBaseException().Message; }
                    Trace(() => "result=success stage=committed item=" + item + " key=" + key + " content=" + (summary?.Invoke(value) ?? "written"));
                };
                if (_commit == null) completed();
                else { _pending.Add(Tuple.Create(item, key)); _committed.Add(completed); }
                return true;
            }
            catch (Exception ex)
            {
                Failure(item, key, stage, ex);
                return false;
            }
        }
        private void Failure(string item, string key, string stage, Exception ex)
        {
            _failed.Add(item);
            _reasons.Add(item + "（" + stage + "）：" + ex.GetBaseException().Message);
            Trace(() => "result=failure stage=" + stage + " item=" + item + " key=" + key + " reason=" + ex.GetBaseException().GetType().Name + ":" + ex.GetBaseException().Message.Replace('\r', ' ').Replace('\n', ' '));
        }
        internal void Skip(string item, string key, string reason)
        { Trace(() => "result=skipped item=" + item + " key=" + key + " reason=" + reason + " previous=retained"); }
        internal bool Finish()
        {
            if (_commit != null)
            {
                try
                {
                    if (_pending.Any()) _commit();
                    foreach (var completed in _committed) completed();
                }
                catch (Exception ex) { foreach (var item in _pending) Failure(item.Item1, item.Item2, "write", ex); }
                finally { _close?.Invoke(); _pending.Clear(); _committed.Clear(); _commit = null; }
            }
            Trace(() => "result=complete success=[" + string.Join(",", _success) + "] failed=[" + string.Join(",", _failed) + "]");
            return Succeeded;
        }
    }
}
