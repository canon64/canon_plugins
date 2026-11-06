using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;

namespace MainGameAiShoujoMode.Authoring
{
    internal static class AuthoringJson
    {
        internal static string Write<T>(T value) { using (var stream = new MemoryStream()) { new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value); return Encoding.UTF8.GetString(stream.ToArray()); } }
        internal static T Read<T>(string json) { using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json))) return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream); }
        internal static T Copy<T>(T value) => Read<T>(Write(value));
    }
    internal sealed class AuthoringStore
    {
        internal readonly string DirectoryPath;
        private string CharactersPath => Path.Combine(DirectoryPath, "characters.json");
        private string EventsPath => Path.Combine(DirectoryPath, "events.json");
        private string Journal => Path.Combine(DirectoryPath, "pending-save.json");
        private string baselineCharacters, baselineEvents;
        internal AuthoringStore(string directory) { DirectoryPath = Path.GetFullPath(directory); }
        internal AuthoringProject Load()
        {
            Directory.CreateDirectory(DirectoryPath);
            using (var guard = new FileStream(Path.Combine(DirectoryPath, ".save-lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
            Recover();
            var p = new AuthoringProject();
            if (File.Exists(CharactersPath)) p.characters = AuthoringJson.Read<CharacterDocument>(File.ReadAllText(CharactersPath, Encoding.UTF8));
            if (File.Exists(EventsPath)) p.events = AuthoringJson.Read<EventDocument>(File.ReadAllText(EventsPath, Encoding.UTF8));
            p.Validate(); baselineCharacters = Hash(CharactersPath); baselineEvents = Hash(EventsPath); return p;
            }
        }
        internal void Save(AuthoringProject project)
        {
            project.Validate(); Directory.CreateDirectory(DirectoryPath);
            using (var guard = new FileStream(Path.Combine(DirectoryPath, ".save-lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                if (File.Exists(Journal)) throw new IOException("未完了の保存があります。再読込で復旧してください。");
                if (baselineCharacters != Hash(CharactersPath) || baselineEvents != Hash(EventsPath)) throw new IOException("JSONが外部で更新されました。書出しで下書きを退避し、再読込してください。");
                var backup = Path.Combine(DirectoryPath, "Backups", DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffff") + "-" + Numbers.Id()); Directory.CreateDirectory(backup);
                // Empty string records an originally missing document; recovery removes only that target.
                var record = new Dictionary<string, string>();
                foreach (var target in new[] { CharactersPath, EventsPath })
                { var saved = File.Exists(target) ? Path.Combine(backup, Path.GetFileName(target)) : ""; if (saved.Length != default(int)) File.Copy(target, saved); record.Add(target, saved); }
                Replace(Journal, AuthoringJson.Write(record));
                try { Replace(CharactersPath, AuthoringJson.Write(project.characters)); Replace(EventsPath, AuthoringJson.Write(project.events)); File.Delete(Journal); }
                catch { Recover(); throw; }
                baselineCharacters = Hash(CharactersPath); baselineEvents = Hash(EventsPath);
            }
        }
        private void Recover()
        {
            if (!File.Exists(Journal)) return;
            var record = AuthoringJson.Read<Dictionary<string, string>>(File.ReadAllText(Journal, Encoding.UTF8));
            foreach (var target in new[] { CharactersPath, EventsPath })
            {
                if (!record.TryGetValue(target, out var backup)) throw new InvalidDataException("保存復旧情報が不正です。");
                if (backup.Length != default(int) && (!Path.GetFullPath(backup).StartsWith(Path.Combine(DirectoryPath, "Backups") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(backup))) throw new InvalidDataException("保存前のバックアップがありません。");
            }
            foreach (var target in new[] { CharactersPath, EventsPath }) { var backup = record[target]; if (backup.Length == default(int)) { if (File.Exists(target)) File.Delete(target); } else Replace(target, File.ReadAllText(backup, Encoding.UTF8)); }
            File.Delete(Journal);
        }
        internal static void Replace(string target, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(target)));
            var temporary = target + "." + Numbers.Id() + ".tmp";
            try { File.WriteAllText(temporary, text, new UTF8Encoding(false)); if (File.Exists(target)) File.Replace(temporary, target, null); else File.Move(temporary, target); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        private static string Hash(string path) { if (!File.Exists(path)) return "missing"; using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path))); }
        internal string Resolve(string path) => Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(DirectoryPath, path));
    }
    internal sealed class AuthoringDraft
    {
        internal AuthoringProject Project { get; private set; }
        private readonly Stack<string> undo = new Stack<string>(), redo = new Stack<string>();
        private string saved;
        internal bool Dirty { get; private set; }
        internal AuthoringDraft(AuthoringProject project) { Reset(project); }
        internal void Reset(AuthoringProject p) { p.Validate(); Project = AuthoringJson.Copy(p); undo.Clear(); redo.Clear(); MarkSaved(); }
        internal void MarkSaved() { saved = AuthoringJson.Write(Project); Dirty = false; }
        internal void Change(Action<AuthoringProject> edit)
        {
            var before = AuthoringJson.Write(Project);
            try { edit(Project); } catch { Project = AuthoringJson.Read<AuthoringProject>(before); throw; }
            var after = AuthoringJson.Write(Project); if (before == after) return; undo.Push(before); redo.Clear(); Dirty = after != saved;
        }
        internal void Undo() { if (undo.Count == default(int)) return; redo.Push(AuthoringJson.Write(Project)); var value = undo.Pop(); Project = AuthoringJson.Read<AuthoringProject>(value); Dirty = value != saved; }
        internal void Redo() { if (redo.Count == default(int)) return; undo.Push(AuthoringJson.Write(Project)); var value = redo.Pop(); Project = AuthoringJson.Read<AuthoringProject>(value); Dirty = value != saved; }
        internal void Discard() { Project = AuthoringJson.Read<AuthoringProject>(saved); undo.Clear(); redo.Clear(); Dirty = false; }
    }
}
