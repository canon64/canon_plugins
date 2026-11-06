using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MainGameHLocationPlacement
{
    internal sealed class SaveOperation
    {
        internal string Owner, PreviousIdentity;
        internal string TargetIdentity;
        internal Exception CaptureFailure;
        internal int Index, Loop;
        internal PlaceOrigin Source;
        internal bool Completed;
    }
    internal sealed partial class PlacementSession
    {
        private readonly Stack<SaveOperation> _saves = new Stack<SaveOperation>();
        internal SaveOperation StartComposerSave(object target, int loop, bool append)
        {
            int index = append ? Bridge.ComposerPresets.Count : Bridge.ComposerPresets.IndexOf(target);
            if (index < 0) return null;
            RefreshDestination();
            SavedPreset before = append || target == null ? null : Bridge.ComposerSnapshot(target);
            string previous = before == null ? null : (!string.IsNullOrEmpty(Bridge.ComposerPath) ? _store.Find(Bridge.ComposerPath, before)?.identity : null) ?? before.Identity;
            var operation = new SaveOperation { Owner = PresetDocuments.Composer, Index = index, Loop = loop,
                PreviousIdentity = previous, Source = Capture() };
            _saves.Push(operation); return operation;
        }
        internal SaveOperation StartHipSave(string overwriteId)
        {
            // The native entry loads presets and resolves the character after this prefix.
            // Bind by stable ID once the actual snapshot has been constructed, never by list index.
            var operation = new SaveOperation { Owner = PresetDocuments.Hip, TargetIdentity = overwriteId, PreviousIdentity = overwriteId };
            _saves.Push(operation); return operation;
        }
        internal void HipSnapshotCreated(object snapshot)
        {
            if (!Valid || snapshot == null || _saves.Count == 0 || _saves.Peek().Owner != PresetDocuments.Hip) return;
            var operation = _saves.Peek();
            try
            {
                operation.Source = CaptureHip();
                operation.Source.poseKey = R.Get<int>(snapshot, "postureMode") + "/" + R.Get<int>(snapshot, "postureId");
                operation.Source.Validate();
                if (operation.TargetIdentity == null) operation.TargetIdentity = R.Get<string>(snapshot, "id");
            }
            catch (Exception ex) { operation.CaptureFailure = ex; Report(ex); }
        }
        internal void EndSave(SaveOperation operation, Exception exception)
        {
            if (operation == null) return;
            if (_saves.Count == 0 || !ReferenceEquals(_saves.Peek(), operation)) { _saves.Clear(); Report(new InvalidOperationException("保存処理の対応が一致しません。")); return; }
            _saves.Pop();
            if (exception != null) Report(exception);
            else if (operation.Completed)
            {
                _plugin.Message(operation.Owner + "の保存元を記録しました。");
                if (operation.Owner == PresetDocuments.Composer) RefreshComposerAfterSave();
            }
            else if (operation.Owner == PresetDocuments.Hip && operation.TargetIdentity != null)
                _plugin.Message("HipHijackの保存元は記録されていません。保存未完了または取得失敗です。");
        }
        internal OriginStore.Pending PrepareSave(string path, string json, string owner)
        {
            if (!Valid || _saves.Count == 0) return null;
            var operation = _saves.Peek();
            if (operation.Owner != owner) return null;
            string expected = owner == PresetDocuments.Composer ? Bridge.ComposerPath : Bridge.HipPath;
            if (OriginStore.Normalize(path) != OriginStore.Normalize(expected)) return null;
            IList current = owner == PresetDocuments.Composer ? Bridge.ComposerPresets : Bridge.HipPresets;
            if (owner == PresetDocuments.Composer && (operation.Index >= current.Count || current[operation.Index] == null)) throw new InvalidOperationException("保存対象のプリセットがありません。");
            if (owner == PresetDocuments.Hip && (operation.Source == null || operation.CaptureFailure != null || string.IsNullOrEmpty(operation.TargetIdentity)))
                throw new InvalidOperationException("HipHijackの保存元を取得できないため保存を停止しました。", operation.CaptureFailure);
            string identity = owner == PresetDocuments.Composer ? Bridge.ComposerSnapshot(current[operation.Index]).Identity : operation.TargetIdentity;
            var matches = PresetDocuments.Collection(owner, json).Where(p => p.Identity == identity).ToList();
            if (matches.Count != 1) throw new InvalidOperationException("保存対象を一意に識別できません。");
            var preset = matches[0];
            Trace("save.prepare", () => "owner=" + owner + " preset=" + preset.Name + " id=" + preset.Identity + " source={" + SourceText(operation.Source) + "}");
            if (_plugin.Diagnostics) foreach (var part in preset.Parts)
                Trace("save.part", () => "owner=" + owner + " preset=" + preset.Name + " part=" + part.Name + " fingerprint=" + part.Fingerprint + " saved=" + string.Join("|", part.Elements));
            var changed = preset.Parts.Where(p => owner == PresetDocuments.Hip || operation.Loop == 0 ||
                p.Name == (operation.Loop == 1 ? "loop1Json" : "loop2Json") || !R.Get<bool>(current[operation.Index], "useLoopSplit")).Select(p => p.Name);
            return _store.Prepare(path, preset, operation.Source, changed, operation.PreviousIdentity,
                owner == PresetDocuments.Composer ? (Func<SavedPart, PlaceOrigin>)(part => ComposerSaveSource(part, operation.Source)) : null);
        }
        internal void SaveCompleted()
        { if (_saves.Count > 0) { _saves.Peek().Completed = true; Trace("save.committed", () => "owner=" + _saves.Peek().Owner + " id=" + _saves.Peek().TargetIdentity); } }
        internal void WriteHip(string path, string json, Encoding encoding)
        {
            using (var pending = PrepareSave(path, json, PresetDocuments.Hip))
            {
                File.WriteAllText(path, json, encoding);
                if (pending != null) { pending.Commit(); SaveCompleted(); }
            }
        }
    }
}
