using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace MainGameAiShoujoMode.Conversation
{
#pragma warning disable CS0649 // Serializer-populated contracts.
    [DataContract] internal sealed class AiReplyResponse
    {
        [DataMember(IsRequired = true)] public string text;
        [DataMember(IsRequired = true)] public string emotion;
        [DataMember(IsRequired = true)] public string action;
        [DataMember(IsRequired = true)] public string target_id;
        internal static AiReplyResponse Parse(string json, AiReplySettings settings)
        {
            if (string.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json) > settings.maxJsonBytes)
                throw new InvalidDataException("AI返答JSONが空か、サイズ上限を超えています。");
            AiReplyResponse reply;
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                reply = (AiReplyResponse)new DataContractJsonSerializer(typeof(AiReplyResponse)).ReadObject(stream);
            if (reply == null || string.IsNullOrWhiteSpace(reply.text) || reply.text.Length > settings.maxTextCharacters)
                throw new InvalidDataException("AI返答の台詞が空か、文字数上限を超えています。");
            if (!settings.emotions.Any(item => item.key == reply.emotion)) throw new InvalidDataException("未対応の感情です：" + reply.emotion);
            if (!new[] { "talk", "follow", "normal", "rest", "eat", "h", "receive_item" }.Contains(reply.action)) throw new InvalidDataException("未対応の行動です：" + reply.action);
            return reply;
        }
    }
    [DataContract] internal sealed class AiReplyEmotion
    {
        [DataMember] public string key, expression;
    }
    [DataContract] internal sealed class AiReplySettings
    {
        [DataMember] public string responsePath, serverUrl, model, style;
        [DataMember] public int speakerId, zero, one, maxJsonBytes, maxTextCharacters, maxWaveBytes, maxChannels, bitsPerByte, connectTimeoutMilliseconds;
        [DataMember] public float timeoutSeconds, styleWeight, sdpRatio, noise, noiseW, length;
        [DataMember] public AiReplyEmotion[] emotions;
        [DataMember] public AiPipeNativeSettings nativePipe;
        internal void Validate()
        {
            if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                || string.IsNullOrWhiteSpace(model) || string.IsNullOrWhiteSpace(responsePath) || !Path.IsPathRooted(responsePath)
                || string.IsNullOrWhiteSpace(style) || zero != default(int) || one != Math.Sign(int.MaxValue)
                || maxJsonBytes <= zero || maxTextCharacters <= zero || maxWaveBytes <= zero || maxChannels <= zero || bitsPerByte <= zero || connectTimeoutMilliseconds <= zero
                || timeoutSeconds <= zero || nativePipe == null || nativePipe.access == default(uint) || nativePipe.openExisting == default(uint)
                || nativePipe.overlapped == default(uint) || nativePipe.readMode == default(uint) || nativePipe.pendingError <= zero || nativePipe.moreDataError <= zero
                || emotions == null || emotions.Any(e => e == null || string.IsNullOrWhiteSpace(e.key) || string.IsNullOrWhiteSpace(e.expression))
                || emotions.Select(e => e.key).Distinct(StringComparer.Ordinal).Count() != emotions.Length
                || new[] { timeoutSeconds, styleWeight, sdpRatio, noise, noiseW, length }.Any(f => float.IsNaN(f) || float.IsInfinity(f)))
                throw new InvalidDataException("AI返答の音声・表情設定が不正です。");
        }
        internal static AiReplySettings Load(string content)
        {
            using (var stream = File.OpenRead(Path.Combine(content, "ai-reply-runtime.json")))
            {
                var result = (AiReplySettings)new DataContractJsonSerializer(typeof(AiReplySettings)).ReadObject(stream);
                result.Validate(); return result;
            }
        }
    }
    [DataContract] internal sealed class AiReplyAction
    {
        [DataMember(Name = "action")] internal string Action;
        [DataMember(Name = "target_id")] internal string Target;
        [DataMember(Name = "label",EmitDefaultValue=false)] internal string Label;
        internal string Inventory;
        internal int Category, Item;
        internal bool Matches(AiReplyResponse response) => Action == response.action && Target == response.target_id;
    }
#pragma warning restore CS0649
}
