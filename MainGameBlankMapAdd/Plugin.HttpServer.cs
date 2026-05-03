using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Video;

namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        [DataContract]
        private sealed class CoordsResponseView
        {
            [DataMember(Order = 0)]
            public bool Ok = true;

            [DataMember(Order = 1)]
            public string Timestamp = string.Empty;

            [DataMember(Order = 2)]
            public TransformView Listener;

            [DataMember(Order = 3)]
            public TransformView Female;

            [DataMember(Order = 4)]
            public TransformView VideoRoom;

            [DataMember(Order = 5)]
            public TransformView VideoAudioSource;

            [DataMember(Order = 6)]
            public VideoPlayerView Video;

            [DataMember(Order = 7)]
            public List<AudioSourceView> ActiveVoiceSources = new List<AudioSourceView>();

            [DataMember(Order = 8)]
            public string Message = string.Empty;
        }

        [DataContract]
        private sealed class TransformView
        {
            [DataMember(Order = 0)]
            public Vector3View Position;

            [DataMember(Order = 1)]
            public Vector3View Rotation;
        }

        [DataContract]
        private sealed class Vector3View
        {
            [DataMember(Order = 0)]
            public float X;

            [DataMember(Order = 1)]
            public float Y;

            [DataMember(Order = 2)]
            public float Z;
        }

        [DataContract]
        private sealed class VideoPlayerView
        {
            [DataMember(Order = 0)]
            public string Url = string.Empty;

            [DataMember(Order = 1)]
            public string Mode = string.Empty;

            [DataMember(Order = 2)]
            public bool IsPlaying;

            [DataMember(Order = 3)]
            public bool IsPrepared;

            [DataMember(Order = 4)]
            public double Time;

            [DataMember(Order = 5)]
            public bool DirectMute;

            [DataMember(Order = 6)]
            public float DirectVolume;
        }

        [DataContract]
        private sealed class AudioSourceView
        {
            [DataMember(Order = 0)]
            public string Name = string.Empty;

            [DataMember(Order = 1)]
            public string Path = string.Empty;

            [DataMember(Order = 2)]
            public Vector3View Position;

            [DataMember(Order = 3)]
            public float DistanceToListener;

            [DataMember(Order = 4)]
            public float SpatialBlend;

            [DataMember(Order = 5)]
            public float PanStereo;

            [DataMember(Order = 6)]
            public float Volume;

            [DataMember(Order = 7)]
            public float MinDistance;

            [DataMember(Order = 8)]
            public float MaxDistance;
        }

        private HttpListener _httpListener;
        private Thread _httpThread;
        private volatile bool _httpRunning;

        private void StartHttpServer()
        {
            if (_settings == null || !_settings.HttpEnabled) return;
            int port = _settings.HttpPort;

            try
            {
                _httpListener = new HttpListener();
                _httpListener.Prefixes.Add($"http://127.0.0.1:{port}/");
                _httpListener.Start();
                _httpRunning = true;
                _httpThread = new Thread(HttpServerLoop)
                {
                    IsBackground = true,
                    Name = "BlankMapHttpServer"
                };
                _httpThread.Start();
                LogInfo($"[http] server started port={port}");
            }
            catch (Exception ex)
            {
                LogWarn($"[http] server start failed: {ex.Message}");
                _httpListener = null;
            }
        }

        private void StopHttpServer()
        {
            _httpRunning = false;
            try { _httpListener?.Stop(); } catch { }
            _httpListener = null;
            _httpThread = null;
        }

        private void HttpServerLoop()
        {
            while (_httpRunning)
            {
                try
                {
                    var ctx = _httpListener.GetContext();
                    ThreadPool.QueueUserWorkItem(_ => HandleHttpRequest(ctx));
                }
                catch (Exception ex)
                {
                    if (_httpRunning)
                        LogWarn($"[http] server error: {ex.Message}");
                }
            }
        }

        private void HandleHttpRequest(HttpListenerContext ctx)
        {
            var req = ctx.Request;
            var res = ctx.Response;
            try
            {
                string path = req.Url.AbsolutePath.TrimEnd('/').ToLowerInvariant();
                string body = "";
                if (req.HasEntityBody)
                {
                    using (var sr = new StreamReader(req.InputStream, Encoding.UTF8))
                        body = sr.ReadToEnd();
                }

                string msg = "ok";
                int status = 200;

                switch (path)
                {
                    case "/videoroom/coords":
                    {
                        if (!TryBuildRealtimeCoordsResponseJson(out string coordsJson, out string coordsError))
                        {
                            status = 500;
                            msg = string.IsNullOrWhiteSpace(coordsError) ? "coords failed" : coordsError;
                            break;
                        }

                        WriteJsonResponse(res, 200, coordsJson);
                        return;
                    }
                    case "/videoroom/play":
                        ParseAndQueuePlayCommand(body);
                        break;
                    case "/videoroom/next":
                        CommandQueue.Enqueue(() =>
                        {
                            if (FolderFiles.Length == 0) return;
                            int next = FolderIndex + 1;
                            if (next >= FolderFiles.Length && (_settings?.FolderPlayLoop ?? true)) next = 0;
                            if (next < FolderFiles.Length) PlayFolderEntry(next);
                        });
                        break;
                    case "/videoroom/prev":
                        CommandQueue.Enqueue(() =>
                        {
                            if (FolderFiles.Length == 0) return;
                            int prev = FolderIndex - 1;
                            if (prev < 0 && (_settings?.FolderPlayLoop ?? true)) prev = FolderFiles.Length - 1;
                            if (prev >= 0) PlayFolderEntry(prev);
                        });
                        break;
                    default:
                        status = 404;
                        msg = "not found";
                        break;
                }

                string fallbackJson = $"{{\"ok\":{(status == 200 ? "true" : "false")},\"msg\":\"{EscapeJson(msg)}\"}}";
                WriteJsonResponse(res, status, fallbackJson);
            }
            catch (Exception ex)
            {
                LogWarn($"[http] request error: {ex.Message}");
            }
            finally
            {
                try { res.Close(); } catch { }
            }
        }

        private bool TryBuildRealtimeCoordsResponseJson(out string json, out string error)
        {
            json = string.Empty;
            error = string.Empty;

            var done = new ManualResetEventSlim(false);
            CoordsResponseView view = null;
            Exception mainThreadError = null;

            CommandQueue.Enqueue(() =>
            {
                try
                {
                    view = CollectRealtimeCoordsOnMainThread();
                }
                catch (Exception ex)
                {
                    mainThreadError = ex;
                }
                finally
                {
                    done.Set();
                }
            });

            if (!done.Wait(600))
            {
                error = "coords timeout";
                return false;
            }

            if (mainThreadError != null)
            {
                error = $"coords main-thread error: {mainThreadError.Message}";
                return false;
            }

            if (view == null)
            {
                error = "coords snapshot is null";
                return false;
            }

            json = SerializeToJson(view);
            return true;
        }

        private CoordsResponseView CollectRealtimeCoordsOnMainThread()
        {
            var view = new CoordsResponseView
            {
                Ok = true,
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
            };

            EnsureFemaleCharaRef();

            AudioListener listener = UnityEngine.Object.FindObjectOfType<AudioListener>();
            if (listener != null)
                view.Listener = ToTransformView(listener.transform);

            if (_femaleChara != null)
                view.Female = ToTransformView(_femaleChara.transform);

            if (_videoRoomRoot != null)
                view.VideoRoom = ToTransformView(_videoRoomRoot.transform);

            if (_videoRoomAudioSource != null)
                view.VideoAudioSource = ToTransformView(_videoRoomAudioSource.transform);

            if (_mainVideoPlayer != null)
            {
                var video = new VideoPlayerView();
                try { video.Url = _mainVideoPlayer.url ?? string.Empty; } catch { }
                try { video.Mode = _mainVideoPlayer.audioOutputMode.ToString(); } catch { }
                try { video.IsPlaying = _mainVideoPlayer.isPlaying; } catch { }
                try { video.IsPrepared = _mainVideoPlayer.isPrepared; } catch { }
                try { video.Time = _mainVideoPlayer.time; } catch { }
                try { video.DirectMute = _mainVideoPlayer.GetDirectAudioMute((ushort)0); } catch { }
                try { video.DirectVolume = _mainVideoPlayer.GetDirectAudioVolume((ushort)0); } catch { }
                view.Video = video;
            }

            AudioSource[] sources = UnityEngine.Object.FindObjectsOfType<AudioSource>();
            Vector3 listenerPos = listener != null ? listener.transform.position : Vector3.zero;
            for (int i = 0; i < sources.Length; i++)
            {
                AudioSource s = sources[i];
                if (s == null || !s.isPlaying)
                    continue;

                string path = BuildTransformPath(s.transform);
                bool isVoice = path.IndexOf("/Voice/PlayObjectPCM/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               s.name.StartsWith("h_", StringComparison.OrdinalIgnoreCase);
                if (!isVoice)
                    continue;

                var src = new AudioSourceView
                {
                    Name = s.name ?? string.Empty,
                    Path = path ?? string.Empty,
                    Position = ToVector3View(s.transform != null ? s.transform.position : Vector3.zero),
                    DistanceToListener = listener != null
                        ? Vector3.Distance(listenerPos, s.transform.position)
                        : float.NaN,
                    SpatialBlend = s.spatialBlend,
                    PanStereo = s.panStereo,
                    Volume = s.volume,
                    MinDistance = s.minDistance,
                    MaxDistance = s.maxDistance
                };
                view.ActiveVoiceSources.Add(src);
            }

            view.Message = $"voiceSources={view.ActiveVoiceSources.Count}";
            return view;
        }

        private static TransformView ToTransformView(Transform t)
        {
            if (t == null)
                return null;

            return new TransformView
            {
                Position = ToVector3View(t.position),
                Rotation = ToVector3View(t.eulerAngles)
            };
        }

        private static Vector3View ToVector3View(Vector3 v)
        {
            return new Vector3View
            {
                X = v.x,
                Y = v.y,
                Z = v.z
            };
        }

        private static string SerializeToJson<T>(T value)
        {
            var serializer = new DataContractJsonSerializer(typeof(T));
            using (var ms = new MemoryStream())
            {
                serializer.WriteObject(ms, value);
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        private static void WriteJsonResponse(HttpListenerResponse res, int status, string json)
        {
            byte[] buf = Encoding.UTF8.GetBytes(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            res.StatusCode = status;
            res.ContentType = "application/json; charset=utf-8";
            res.ContentLength64 = buf.Length;
            res.OutputStream.Write(buf, 0, buf.Length);
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var sb = new StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: sb.Append(c); break;
                }
            }

            return sb.ToString();
        }

        private void ParseAndQueuePlayCommand(string body)
        {
            int index = -1;
            string filename = null;

            // "index": N を探す
            int idxPos = body.IndexOf("\"index\"", StringComparison.OrdinalIgnoreCase);
            if (idxPos >= 0)
            {
                int colon = body.IndexOf(':', idxPos);
                if (colon >= 0)
                {
                    string rest = body.Substring(colon + 1).TrimStart();
                    int numEnd = 0;
                    while (numEnd < rest.Length && (char.IsDigit(rest[numEnd]) || rest[numEnd] == '-'))
                        numEnd++;
                    if (numEnd > 0 && int.TryParse(rest.Substring(0, numEnd), out int parsed))
                        index = parsed;
                }
            }

            // "filename": "..." を探す
            int fnPos = body.IndexOf("\"filename\"", StringComparison.OrdinalIgnoreCase);
            if (fnPos >= 0)
            {
                int colon = body.IndexOf(':', fnPos);
                if (colon >= 0)
                {
                    int q1 = body.IndexOf('"', colon + 1);
                    if (q1 >= 0)
                    {
                        int q2 = body.IndexOf('"', q1 + 1);
                        if (q2 > q1)
                            filename = body.Substring(q1 + 1, q2 - q1 - 1);
                    }
                }
            }

            int capturedIndex = index;
            string capturedFilename = filename;
            CommandQueue.Enqueue(() =>
            {
                if (capturedIndex >= 0)
                {
                    PlayFolderEntry(capturedIndex);
                }
                else if (!string.IsNullOrEmpty(capturedFilename))
                {
                    for (int i = 0; i < FolderFiles.Length; i++)
                    {
                        if (Path.GetFileName(FolderFiles[i]).Equals(
                                capturedFilename, StringComparison.OrdinalIgnoreCase))
                        {
                            PlayFolderEntry(i);
                            return;
                        }
                    }
                    LogWarn($"[http] play: filename not found: {capturedFilename}");
                }
            });
        }
    }
}
