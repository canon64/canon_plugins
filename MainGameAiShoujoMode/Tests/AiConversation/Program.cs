using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using MainGameAiShoujoMode.Conversation;

internal static class Program
{
    private static int _assertions;
    private static void Check(bool condition, string name)
    { if (!condition) throw new Exception(name); ++_assertions; }
    private sealed class InlineContext : SynchronizationContext
    { public override void Post(SendOrPostCallback callback, object state) => callback(state); }
    private static async Task Main(string[] args)
    {
        var settings = AiReplySettings.Load("F:/kks/work/plugins/canon_plugins/MainGameAiShoujoMode/Content");
        TurnChecks.Run(Check);
        var admission = new AiConversationAdmission("girl");
        Check(!admission.Accept(null), "preparing cannot accept");
        admission.Ready(); var first = admission.Input;
        Check(!admission.Accept("stale"), "old input rejected");
        Check(admission.Accept(first), "first accepted");
        Check(!admission.Accept(first), "double submit rejected");
        var request = admission.Request;
        var message = new AiConversationMessage { conversation_id = admission.Conversation, character_id = "girl", request_id = request };
        Check(admission.Matches(message), "correlated reply accepted");
        message.character_id = "another"; Check(!admission.Matches(message), "wrong actor rejected");
        message.character_id = "girl"; message.version = "future"; Check(!admission.Matches(message), "wrong version rejected");
        message.version = "ai-conversation-v1";
        admission.Ready(); Check(first != admission.Input, "new input token");
        Check(!admission.Matches(message), "old request cannot replay");
        Check(admission.Accept(admission.Input), "next input accepted");
        Check(request != admission.Request, "new request identity");
        admission.Close(); Check(!admission.Accept(admission.Input), "closed cannot accept");
        admission.Ready(); Check(!admission.Open, "closed cannot revive");
        if (args.Contains("--pipe"))
        {
            var name = args.FirstOrDefault(value => value.StartsWith("--pipe-name="))?.Substring("--pipe-name=".Length) ?? "kks_ai_conversation_v1";
            using (var pipe = new AiConversationPipe(new InlineContext(), settings.maxJsonBytes,
                settings.connectTimeoutMilliseconds, settings.nativePipe, name))
            {
                var ready = new TaskCompletionSource<AiConversationMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
                var burst = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var seen = new List<string>();
                pipe.Received += value => { seen.Add(value.text); ready.TrySetResult(value); if (value.text == "second") burst.TrySetResult(true); };
                var hello = new AiConversationMessage { type = "interop-burst", conversation_id = "会話", character_id = "girl", text = new string('島', settings.maxTextCharacters) };
                var reading = pipe.Connect(hello);
                var result = await Task.WhenAny(ready.Task, reading);
                if (result == reading) await reading;
                var received = await ready.Task;
                Check(received.text == hello.text && received.conversation_id == hello.conversation_id, "Python/.NET Unicode message roundtrip");
                Check(await Task.WhenAny(burst.Task, Task.Delay(settings.connectTimeoutMilliseconds)) == burst.Task, "back-to-back messages retain boundaries");
                Check(seen.Last() == "second", "second message independently decoded");
                pipe.Dispose();
                Check(await Task.WhenAny(reading, Task.Delay(settings.connectTimeoutMilliseconds)) == reading, "cancel unblocks pending native read");
                try { await reading; } catch (OperationCanceledException) { } catch (System.IO.IOException) { } catch (ObjectDisposedException) { }
            }
            using (var cancel = new CancellationTokenSource())
            using (var native = new AiConversationPipeNative(settings.nativePipe, cancel.Token, name))
            {
                native.Connect(settings.connectTimeoutMilliseconds);
                native.Write(System.Text.Encoding.UTF8.GetBytes(AiConversationJson.Write(new AiConversationMessage { type = "interop", text = new string('島', settings.maxTextCharacters) })));
                bool rejected = false;
                try { native.Read(settings.maxTextCharacters); } catch (System.IO.InvalidDataException) { rejected = true; }
                Check(rejected, "message exceeding limit is rejected after ERROR_MORE_DATA");
            }
        }
        Console.WriteLine("AI_CONVERSATION_CHECKS_PASS assertions=" + _assertions);
    }
}
