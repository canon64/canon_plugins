using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MainGameAiShoujoMode.Conversation
{
    internal static class AiReplySbv2
    {
        internal static Uri VoiceUri(AiReplySettings settings, string text)
        {
            var values = new[]
            {
                Tuple.Create("text", text), Tuple.Create("model_name", settings.model), Tuple.Create("style", settings.style),
                Tuple.Create("speaker_id", settings.speakerId.ToString(CultureInfo.InvariantCulture)),
                Tuple.Create("style_weight", settings.styleWeight.ToString(CultureInfo.InvariantCulture)),
                Tuple.Create("sdp_ratio", settings.sdpRatio.ToString(CultureInfo.InvariantCulture)),
                Tuple.Create("noise", settings.noise.ToString(CultureInfo.InvariantCulture)),
                Tuple.Create("noisew", settings.noiseW.ToString(CultureInfo.InvariantCulture)),
                Tuple.Create("length", settings.length.ToString(CultureInfo.InvariantCulture)),
                Tuple.Create("language", "JP"), Tuple.Create("auto_split", "false")
            };
            return new Uri(settings.serverUrl.TrimEnd('/') + "/voice?" + string.Join("&", values.Select(v => Uri.EscapeDataString(v.Item1) + "=" + Uri.EscapeDataString(v.Item2))));
        }
        internal static async Task<byte[]> Generate(AiReplySettings settings, string text, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            var request = (HttpWebRequest)WebRequest.Create(VoiceUri(settings, text));
            request.Method = WebRequestMethods.Http.Post; request.ContentLength = default(long);
            request.Proxy = null;
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            using (timeout.Token.Register(request.Abort))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(settings.timeoutSeconds));
                using (var response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false))
                using (var input = response.GetResponseStream())
                using (var output = new MemoryStream())
                {
                    // A fixed bound is allocated from settings, never from an untrusted Content-Length.
                    var buffer = new byte[Math.Min(settings.maxWaveBytes, ushort.MaxValue)];
                    while (true)
                    {
                        var count = await input.ReadAsync(buffer, default(int), buffer.Length, timeout.Token).ConfigureAwait(false);
                        if (count == default(int)) break;
                        if (output.Length + count > settings.maxWaveBytes) throw new InvalidDataException("SBV2音声がサイズ上限を超えました。");
                        output.Write(buffer, default(int), count);
                    }
                    return output.ToArray();
                }
            }
        }
    }
    internal sealed class AiReplyWave
    {
        internal int Channels, Frequency;
        internal float[] Samples;
        internal static AiReplyWave Decode(byte[] bytes, AiReplySettings settings)
        {
            if (bytes == null || bytes.Length > settings.maxWaveBytes) throw new InvalidDataException("SBV2のWAVサイズが不正です。");
            using (var stream = new MemoryStream(bytes))
            using (var reader = new BinaryReader(stream, Encoding.ASCII))
            {
                if (new string(reader.ReadChars("RIFF".Length)) != "RIFF") throw new InvalidDataException("SBV2応答がRIFFではありません。");
                var declared = reader.ReadUInt32();
                if (declared != bytes.Length - "RIFF".Length - sizeof(uint) || new string(reader.ReadChars("WAVE".Length)) != "WAVE")
                    throw new InvalidDataException("SBV2応答が完全なWAVではありません。");
                var format = default(ushort); var channels = default(ushort); var frequency = default(int); var bits = default(ushort);
                byte[] data = null;
                while (stream.Position < stream.Length)
                {
                    var kind = new string(reader.ReadChars("data".Length)); var size = reader.ReadUInt32();
                    var end = checked(stream.Position + size);
                    if (end > stream.Length) throw new InvalidDataException("WAVのデータが途中で切れています。");
                    if (kind == "fmt ")
                    {
                        format = reader.ReadUInt16(); channels = reader.ReadUInt16(); frequency = reader.ReadInt32();
                        reader.ReadInt32(); reader.ReadUInt16(); bits = reader.ReadUInt16();
                        if (stream.Position > end) throw new InvalidDataException("WAVのfmtが不正です。");
                    }
                    else if (kind == "data") data = reader.ReadBytes(checked((int)size));
                    stream.Position = checked(end + size % sizeof(short));
                    if (stream.Position > stream.Length) throw new InvalidDataException("WAVのパディングが不足しています。");
                }
                if (format != settings.one || channels <= settings.zero || channels > settings.maxChannels || frequency <= settings.zero
                    || bits != sizeof(short) * settings.bitsPerByte || data == null || data.Length == settings.zero
                    || data.Length % (channels * sizeof(short)) != settings.zero)
                    throw new InvalidDataException("SBV2音声は完全な16bit PCM WAVである必要があります。");
                var result = new AiReplyWave { Channels = channels, Frequency = frequency, Samples = new float[data.Length / sizeof(short)] };
                for (var index = settings.zero; index < result.Samples.Length; index++)
                    result.Samples[index] = BitConverter.ToInt16(data, index * sizeof(short)) / ((float)short.MaxValue + settings.one);
                return result;
            }
        }
    }
}
