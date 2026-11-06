using System;
using System.Collections.Generic;
using System.Linq;

namespace MainGameNotifications
{
    public enum NotificationSeverity { Information, Warning, Failure }

    public sealed class Notification
    {
        public string OwnerGuid { get; }
        public string Id { get; }
        public string Title { get; }
        public string Body { get; }
        public NotificationSeverity Severity { get; }
        public DateTime UpdatedUtc { get; }
        internal Notification(string owner, string id, string title, string body, NotificationSeverity severity)
        { OwnerGuid = owner; Id = id; Title = title; Body = body; Severity = severity; UpdatedUtc = DateTime.UtcNow; }
    }

    // No expiry, scene references, diagnostics switch, or session cleanup: failures survive mode changes.
    internal sealed class NotificationStore
    {
        private readonly List<Notification> _items = new List<Notification>();
        internal event Action Changed;
        internal string Publish(string owner, string title, string body, NotificationSeverity severity, string id)
        {
            ValidateOwner(owner);
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("通知の見出しが必要です。", nameof(title));
            if (!Enum.IsDefined(typeof(NotificationSeverity), severity)) throw new ArgumentOutOfRangeException(nameof(severity));
            id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id;
            int index = _items.FindIndex(n => n.OwnerGuid == owner && n.Id == id);
            // A successful later operation must not erase a failure the user has not acknowledged.
            while (index >= 0 && _items[index].Severity == NotificationSeverity.Failure && severity != NotificationSeverity.Failure)
            {
                id += "/status";
                index = _items.FindIndex(n => n.OwnerGuid == owner && n.Id == id);
            }
            if (index >= 0)
            {
                var old = _items[index];
                if (old.Title == title && old.Body == (body ?? "") && old.Severity == severity) return id;
                _items.RemoveAt(index);
            }
            _items.Add(new Notification(owner, id, title, body ?? "", severity));
            Changed?.Invoke();
            return id;
        }
        internal Notification[] Snapshot() => _items.AsEnumerable().Reverse().ToArray();
        internal bool Dismiss(string owner, string id)
        {
            ValidateOwner(owner);
            int count = _items.RemoveAll(n => n.OwnerGuid == owner && n.Id == id);
            if (count > 0) Changed?.Invoke();
            return count > 0;
        }
        internal int ClearOwner(string owner, bool includeFailures)
        {
            ValidateOwner(owner);
            int count = _items.RemoveAll(n => n.OwnerGuid == owner && (includeFailures || n.Severity != NotificationSeverity.Failure));
            if (count > 0) Changed?.Invoke();
            return count;
        }
        internal void Clear() { _items.Clear(); Changed?.Invoke(); }
        private static void ValidateOwner(string owner)
        { if (string.IsNullOrWhiteSpace(owner)) throw new ArgumentException("通知元のプラグイン GUID が必要です。", nameof(owner)); }
    }
}
