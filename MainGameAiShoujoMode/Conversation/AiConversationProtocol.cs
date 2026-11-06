using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MainGameAiShoujoMode.Conversation
{
#pragma warning disable CS0649
    [DataContract] internal sealed class AiConversationMessage
    {
        [DataMember] public string version = "ai-conversation-v1";
        [DataMember] public string type, conversation_id, character_id, request_id, input_id;
        [DataMember] public string text, payload, audio_path, error, name, instructions, reference;
    }
    internal static class AiConversationJson
    {
        internal static string Write<T>(T value)
        {
            using (var stream = new MemoryStream())
            { new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value); return Encoding.UTF8.GetString(stream.ToArray()); }
        }
        internal static T Read<T>(string text)
        { using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(text))) return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream); }
    }
    // Admission is owned by the game thread. Every input window and request has a new token.
    internal sealed class AiConversationAdmission
    {
        internal readonly string Conversation = Guid.NewGuid().ToString("N"), Character;
        internal string Input { get; private set; }
        internal string Request { get; private set; }
        internal string State { get; private set; } = "preparing";
        internal bool Open => State != "closed";
        internal AiConversationAdmission(string character) { Character = character; }
        internal void Ready() { if (!Open) return; Request = null; Input = Guid.NewGuid().ToString("N"); State = "input"; }
        internal bool Accept(string input)
        {
            if (State != "input" || input != Input) return false;
            State = "sending"; Input = null; Request = Guid.NewGuid().ToString("N"); return true;
        }
        internal bool Matches(AiConversationMessage message) => Open && message.version == "ai-conversation-v1"
            && message.conversation_id == Conversation && message.character_id == Character
            && Request != null && message.request_id == Request;
        internal void Move(string state) { if (Open) State = state; }
        internal void Close() { State = "closed"; Input = null; Request = null; }
    }
    // Python AF_PIPE uses one complete JSON message per WriteFile, without stream length framing.
    internal sealed class AiConversationPipe : IDisposable
    {
        private readonly AiConversationPipeNative _pipe;
        private readonly SemaphoreSlim _write = new SemaphoreSlim(Math.Sign(int.MaxValue));
        private readonly CancellationTokenSource _stop = new CancellationTokenSource();
        private readonly SynchronizationContext _main;
        private readonly int _limit, _timeout;
        internal event Action<AiConversationMessage> Received;
        internal event Action<string> Disconnected;
        internal AiConversationPipe(SynchronizationContext main, int limit, int timeout, AiPipeNativeSettings native, string name = "kks_ai_conversation_v1")
        { _main = main; _limit = limit; _timeout = timeout; _pipe = new AiConversationPipeNative(native, _stop.Token, name); }
        internal Task Connect(AiConversationMessage hello) => Task.Run(() =>
        { _pipe.Connect(_timeout); Write(hello); Read(); });
        internal Task Send(AiConversationMessage message) => Task.Run(() => Write(message));
        private void Write(AiConversationMessage message)
        {
            var bytes = Encoding.UTF8.GetBytes(AiConversationJson.Write(message));
            if (bytes.Length > _limit) throw new InvalidDataException("会話データが上限を超えています。");
            _write.Wait(_stop.Token);
            try { _pipe.Write(bytes); }
            finally { _write.Release(); }
        }
        private void Read()
        {
            while (!_stop.IsCancellationRequested)
            {
                var decoded = AiConversationJson.Read<AiConversationMessage>(Encoding.UTF8.GetString(_pipe.Read(_limit)));
                if (decoded.version != "ai-conversation-v1") throw new InvalidDataException("会話通信の版が一致しません。");
                _main.Post(_ => { if (!_stop.IsCancellationRequested) Received?.Invoke(decoded); }, null);
            }
        }
        internal void ReportFailure(Exception error)
        { _main.Post(_ => { if (!_stop.IsCancellationRequested) Disconnected?.Invoke(error.GetBaseException().Message); }, null); }
        public void Dispose() { if (_stop.IsCancellationRequested) return; _stop.Cancel(); _pipe.Dispose(); }
    }
#pragma warning restore CS0649
}
