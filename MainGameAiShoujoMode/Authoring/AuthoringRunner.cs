using System;
using System.Collections.Generic;
using System.Linq;

namespace MainGameAiShoujoMode.Authoring
{
    // No clock, polling or game-state writes. Each asynchronous cue owns one completion token.
    internal sealed class AuthoringRunner
    {
        internal interface IHost
        {
            void Enter(AuthoringEvent item, Action completed, Action<Exception> failed);
            void Execute(EventNode node, Action completed, Action<Exception> failed);
            void Pause(bool paused);
            void Restore();
        }
        private readonly IHost host;
        private AuthoringProject project;
        private AuthoringEvent item;
        private object run, wait;
        private bool pumping, queued;
        private Action deferred;
        private readonly HashSet<string> completedEvents = new HashSet<string>();
        internal bool Active => run != null;
        internal bool Paused { get; private set; }
        internal EventNode Node { get; private set; }
        internal string EventId => item?.id;
        internal bool Preview { get; private set; }
        internal event Action Changed;
        internal event Action<Exception> Failed;
        internal AuthoringRunner(IHost host) { this.host = host; }
        internal void Start(AuthoringProject source, string eventId, string nodeId = null, bool preview = true)
        {
            source.Validate(); Stop(); project = AuthoringJson.Copy(source); Preview = preview; completedEvents.Clear(); run = new object();
            Begin(eventId, nodeId);
        }
        private void Begin(string id, string nodeId = null)
        {
            item = project.events.events.Single(e => e.id == id); Node = null;
            var token = run; var request = wait = new object();
            try { host.Enter(item, () => Complete(token, request, () => Go(nodeId ?? item.startNode)), ex => Error(token, ex)); }
            catch (Exception ex) { Error(token, ex); }
        }
        private void Complete(object token, object request, Action continuation)
        {
            if (!ReferenceEquals(run, token) || !ReferenceEquals(wait, request)) return;
            wait = null;
            if (Paused) deferred = continuation; else continuation();
        }
        private void Go(string id)
        {
            if (!Active) return;
            Node = string.IsNullOrEmpty(id) ? item.nodes.FirstOrDefault() : item.nodes.Single(n => n.id == id);
            queued = true; Pump();
        }
        private void Next()
        {
            if (!Active) return;
            if (Node != null && !string.IsNullOrEmpty(Node.next)) { Go(Node.next); return; }
            int index = Node == null ? -Numbers.One : item.nodes.IndexOf(Node);
            Node = index + Numbers.One < item.nodes.Count ? item.nodes[index + Numbers.One] : null; queued = true; Pump();
        }
        private void Pump()
        {
            if (pumping || Paused) return; pumping = true;
            try
            {
                int steps = default(int);
                while (Active && queued && !Paused)
                {
                    if (++steps > AuthoringDefaults.Current.maxImmediateSteps) throw new InvalidOperationException("待機のない循環があります。会話・待ち時間を入れてください。");
                    queued = false; Changed?.Invoke();
                    if (Node == null || Node.type == "end")
                    {
                        completedEvents.Add(item.id);
                        string next = item.nextEvent;
                        if (string.IsNullOrEmpty(next)) next = project.events.events.FirstOrDefault(e => e.afterEvent == item.id && !completedEvents.Contains(e.id))?.id;
                        if (string.IsNullOrEmpty(next)) { Stop(); break; }
                        Begin(next); continue;
                    }
                    var token = run; var request = wait = new object();
                    host.Execute(Node, () => Complete(token, request, Next), ex => Error(token, ex));
                }
            }
            catch (Exception ex) { Error(run, ex); }
            finally { pumping = false; }
        }
        internal void Advance()
        {
            if (!Active || Paused || Node == null || Node.type != "dialogue") return;
            wait = null; host.Pause(false); Next();
        }
        internal void Choose(string id)
        {
            if (!Active || Paused || Node?.type != "choice") return;
            var choice = Node.choices.Single(c => c.id == id); wait = null;
            if (!string.IsNullOrEmpty(choice.targetEvent)) Begin(choice.targetEvent);
            else if (!string.IsNullOrEmpty(choice.targetNode)) Go(choice.targetNode);
            else Next();
        }
        internal void SetPaused(bool value)
        {
            if (!Active || value == Paused) return; Paused = value; host.Pause(value); Changed?.Invoke();
            if (!value) { var pending = deferred; deferred = null; pending?.Invoke(); Pump(); }
        }
        internal void ApplyDraft(AuthoringProject source)
        {
            if (!Active || !Paused || Node == null) return;
            source.Validate(); var copy = AuthoringJson.Copy(source);
            var updatedEvent = copy.events.events.SingleOrDefault(e => e.id == item.id);
            var updatedNode = updatedEvent?.nodes.SingleOrDefault(n => n.id == Node.id);
            if (updatedNode == null) throw new InvalidOperationException("再生中の会話が削除されました。停止して別の会話から試してください。");
            project = copy; item = updatedEvent; Node = updatedNode; wait = null; deferred = null; queued = true;
        }
        internal void Stop()
        {
            bool active = Active; run = wait = null; deferred = null; queued = false; Paused = false; Node = null;
            if (active) host.Restore(); Changed?.Invoke();
        }
        private void Error(object token, Exception error)
        { if (!Active || !ReferenceEquals(token, run)) return; try { Stop(); } finally { Failed?.Invoke(error); } }
    }
}
