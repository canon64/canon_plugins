using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MainGameAiShoujoMode.Conversation;

internal static class VoiceGroupChecks
{
    private sealed class Clip { internal string Channel, Name; }
    private sealed class Ticket : IDisposable
    {
        internal bool Disposed;
        internal Action Release;
        public void Dispose() { Require(!Disposed, "Voice group lease disposed twice"); Disposed = true; Release?.Invoke(); }
    }
    private sealed class Channel
    {
        internal Clip Clip;
        internal Action Complete;
        internal Action<Exception> Failed;
        internal readonly Ticket Ticket = new Ticket();
    }
    internal static void Run(string directory, KksNormalConversationRules.Settings n)
    {
        Console.WriteLine("Start parallel voice channel and sequential group checks");
        Clip C(string channel, string name) => new Clip { Channel = channel, Name = name };
        var channels = new List<Channel>(); bool synchronous = false; Action<Channel> starting = null;
        using var sequence = new KksVoiceGroupSequence<Clip>(clip => clip.Channel, (clip, completed, failed) =>
        {
            var channel = new Channel { Clip = clip, Complete = completed, Failed = failed }; channels.Add(channel);
            starting?.Invoke(channel); if (synchronous) completed(); return channel.Ticket;
        }, n.zero);
        int started = n.zero, done = n.zero, errors = n.zero, cleaned = n.zero;
        var first = C("A", "first"); var second = C("B", "second"); var third = C("A", "third");
        var lease = sequence.Play(new[] { new[] { first, second }, new[] { third } }, () => started++, () => done++, _ => errors++, () => cleaned++);
        Require(channels.Count == n.one + n.one && started == n.one, "Voice channels in one group were serialized");
        channels.First().Complete(); channels.First().Complete(); channels.First().Failed(new IOException("late"));
        Require(done == n.zero && channels.Count == n.one + n.one && errors == n.zero, "Group finished before every surviving channel completed");
        channels.Last().Complete();
        Require(channels.Last().Clip == third && started == n.one + n.one && channels.Take(n.one + n.one).All(channel => channel.Ticket.Disposed), "Next group failed to start after channel barrier");
        channels.Last().Complete();
        Require(done == n.one && cleaned == n.one && channels.All(channel => channel.Ticket.Disposed), "Natural series completion leaked channels or cleanup");
        lease.Dispose(); Require(cleaned == n.one, "Finished lease cleaned twice");

        channels.Clear(); started = n.zero; done = n.zero;
        lease = sequence.Play(new[] { new[] { first, third, second } }, () => started++, () => done++, _ => errors++, () => cleaned++);
        Require(channels.Select(channel => channel.Clip).SequenceEqual(new[] { third, second }), "Same-channel replacement did not keep the last voice in source order");
        lease.Dispose(); Require(channels.All(channel => channel.Ticket.Disposed) && done == n.zero, "Canceled series completed or retained channels");
        foreach (var channel in channels) { channel.Complete(); channel.Failed(new IOException("stale")); }
        Require(done == n.zero && errors == n.zero, "Canceled voice callback entered another group");

        channels.Clear(); synchronous = true; started = n.zero; done = n.zero;
        sequence.Play(new[] { Array.Empty<Clip>(), new[] { first, second }, Array.Empty<Clip>() }, () => started++, () => done++, _ => errors++, () => cleaned++);
        Require(started == n.one + n.one + n.one && done == n.one && channels.All(channel => channel.Ticket.Disposed), "Empty or synchronous voice groups lost their start event or lease");

        synchronous = false; channels.Clear(); done = n.zero; errors = n.zero;
        starting = channel => channel.Failed(new IOException("inline load failure"));
        sequence.Play(new[] { new[] { first, second } }, () => started++, () => done++, _ => errors++, () => cleaned++);
        Require(channels.Count == n.one && channels.Single().Ticket.Disposed && errors == n.one && done == n.zero, "Inline channel failure leaked or started later channels");
        starting = null;

        channels.Clear(); done = n.zero; errors = n.zero;
        var oldLease = sequence.Play(new[] { new[] { first } }, null, () => done++, _ => errors++, null);
        var oldChannel = channels.Single();
        sequence.Play(new[] { new[] { second } }, null, () => done++, _ => errors++, null);
        oldLease.Dispose(); oldChannel.Complete(); oldChannel.Failed(new IOException("old scope"));
        Require(!channels.Last().Ticket.Disposed && done == n.zero && errors == n.zero, "An old playback lease stopped its replacement");
        channels.Last().Complete(); Require(done == n.one, "Replacement failed after stale lease disposal");

        channels.Clear(); done = n.zero; started = n.zero;
        starting = _ => sequence.Stop();
        sequence.Play(new[] { new[] { first, second } }, () => started++, () => done++, _ => errors++, null);
        Require(channels.Count == n.one && channels.Single().Ticket.Disposed && done == n.zero && started == n.zero,
            "Cancellation during channel creation leaked the returned lease or began later channels");
        starting = null;

        channels.Clear(); errors = n.zero;
        lease = sequence.Play(new[] { new[] { first, second } }, () => started++, () => done++, _ => errors++, () => cleaned++);
        channels.First().Ticket.Release = () => throw new IOException("channel cleanup failure");
        try { lease.Dispose(); throw new Exception("Cleanup error was swallowed"); } catch (AggregateException) { }
        Require(channels.All(channel => channel.Ticket.Disposed), "Cleanup failure skipped later channels");

        channels.Clear(); synchronous = true; done = n.zero; errors = n.zero; bool replaced = false;
        sequence.Play(new[] { new[] { first } }, () =>
        {
            if (replaced) return; replaced = true;
            sequence.Play(new[] { new[] { second } }, null, () => done++, _ => errors++, null);
        }, () => throw new Exception("Old series completed"), _ => errors++, null);
        Require(done == n.one && errors == n.zero && channels.All(channel => channel.Ticket.Disposed), "Replacement from a group-start callback stalled or leaked a channel");
        errors = n.zero;
        sequence.Play(new[] { new[] { first } }, null, () => throw new IOException("completion callback failure"), _ => errors++, null);
        Require(errors == n.one, "Series completion failure was swallowed");

        started = n.zero; done = n.zero;
        var many = Enumerable.Repeat(Array.Empty<Clip>(), n.requiredNicknameEvent * n.temperamentCount).ToArray();
        sequence.Play(many, () => started++, () => done++, exception => throw exception, null);
        Require(started == many.Length && done == n.one, "Long synchronous voice series recursed or dropped groups");
        File.WriteAllText(Path.Combine(directory, "voice-group-checks.json"), JsonSerializer.Serialize(new
        {
            scope = "Production channel/group scheduler with controlled completions, not native audio playback",
            parallelBarrier = true, sequentialGroups = true, replacementChannels = true, emptyAndSynchronousGroups = true,
            cancellationAndStaleEvents = true, inlineFailures = true, cleanupFailures = true, reentrantReplacement = true, completionErrors = true
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("PASS voice groups parallel barrier sequential groups replacement cancellation failures");
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
