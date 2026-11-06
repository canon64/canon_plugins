using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using MainGameDevelopmentBridge;
using UnityEngine;

public sealed class AudioProbe : IDevelopmentCommand
{
    public string Name => "audio-state";
    private Action _restore;
    private static object Vector(Vector3 value) => new { value.x, value.y, value.z };
    private static object Pose(Transform value)
    {
        if (value == null) return null;
        string path = value.name;
        for (var parent = value.parent; parent != null; parent = parent.parent) path = parent.name + "/" + path;
        return new { id = value.GetInstanceID(), path, position = Vector(value.position), rotation = Vector(value.eulerAngles),
            right = Vector(value.right), up = Vector(value.up), forward = Vector(value.forward), active = value.gameObject.activeInHierarchy };
    }
    private static object Relative(Transform basis, Transform value)
    {
        if (basis == null || value == null) return null;
        var delta = value.position - basis.position;
        return new { right_m = Vector3.Dot(delta, basis.right), up_m = Vector3.Dot(delta, basis.up),
            forward_m = Vector3.Dot(delta, basis.forward), distance_m = delta.magnitude };
    }
    private static AudioListener ManagedListener()
    {
        var property = typeof(Manager.Sound).GetProperty("audioListener", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        var result = property?.GetValue(null, null) as AudioListener;
        if (result != null) return result;
        foreach (var sound in Resources.FindObjectsOfTypeAll<Manager.Sound>())
            for (var type = sound.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField("_audioListener", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                result = field?.GetValue(sound) as AudioListener;
                if (result != null) return result;
            }
        return null;
    }
    private void MoveCamera(CameraControl_Ver2 control, Dictionary<string, object> arguments)
    {
        if (control == null || control.thisCmaera == null) throw new InvalidOperationException("No native H camera");
        if (!arguments.TryGetValue("dx", out var raw)) throw new ArgumentException("camera-left requires finite world-space dx");
        float dx = Convert.ToSingle(raw, CultureInfo.InvariantCulture);
        if (float.IsNaN(dx) || float.IsInfinity(dx)) throw new ArgumentException("dx must be finite");
        if (_restore != null) throw new InvalidOperationException("Restore the existing camera test first");
        var before = control.GetCameraData();
        var camera = control.thisCmaera;
        var position = camera.transform.position; var rotation = camera.transform.rotation;
        var controllerPosition = control.transform.position; var controllerRotation = control.transform.rotation;
        bool limit = control.isLimitPos;
        _restore = () => {
            if (control != null) { control.isLimitPos = limit; control.SetCameraData(before); control.transform.SetPositionAndRotation(controllerPosition, controllerRotation); }
            if (camera != null) camera.transform.SetPositionAndRotation(position, rotation);
        };
        try
        {
            var data = control.GetCameraData();
            Vector3 delta = Vector3.right * dx;
            var focus = control.transBase != null ? control.transBase.TransformPoint(data.Pos) : data.Pos;
            data.Pos = control.transBase != null ? control.transBase.InverseTransformPoint(focus + delta) : focus + delta;
            control.isLimitPos = false;
            control.SetCameraData(data);
            // Set the controller state as well as its current render pose. Native Update owns later frames.
            control.transform.position = controllerPosition + delta;
            camera.transform.position = position + delta;
        }
        catch { Restore(); throw; }
    }
    private void Restore() { var action = _restore; _restore = null; action?.Invoke(); }
    private sealed class Metrics
    {
        public double rms, peak, energy;
        public double? clippedFraction;
    }
    private static Metrics Measure(float[] values, float? threshold)
    {
        var result = new Metrics(); int clipped = default(int);
        foreach (float value in values)
        {
            result.energy += (double)value * value; result.peak = Math.Max(result.peak, Math.Abs(value));
            if (threshold.HasValue && Math.Abs(value) >= threshold.Value) clipped++;
        }
        result.rms = Math.Sqrt(result.energy / values.Length);
        result.clippedFraction = threshold.HasValue ? (double?)clipped / values.Length : null;
        return result;
    }
    private static float? ClipThreshold(Dictionary<string, object> arguments)
    {
        if (!arguments.TryGetValue("clipThreshold", out var raw)) return null;
        float threshold = Convert.ToSingle(raw, CultureInfo.InvariantCulture);
        if (float.IsNaN(threshold) || float.IsInfinity(threshold) || threshold <= default(float)) throw new ArgumentException("clipThreshold must be finite and positive");
        return threshold;
    }
    private static object Pcm(AudioSource source, Dictionary<string, object> arguments)
    {
        if (!arguments.TryGetValue("pcmSamples", out var raw) || source.clip == null) return null;
        int requested = Convert.ToInt32(raw, CultureInfo.InvariantCulture);
        if (requested <= default(int)) throw new ArgumentException("pcmSamples must be positive");
        var clip = source.clip;
        int offset = arguments.TryGetValue("pcmOffset", out var start) ? Convert.ToInt32(start, CultureInfo.InvariantCulture) : source.timeSamples;
        if (offset < default(int) || offset >= clip.samples) return new { success = false, reason = "PCM offset outside clip", offset };
        int count = Math.Min(requested, clip.samples - offset);
        var data = new float[checked(count * clip.channels)];
        if (!clip.GetData(data, offset)) return new { success = false, reason = "AudioClip.GetData unavailable; compressed/streaming clip may require decompression", offset };
        var threshold = ClipThreshold(arguments);
        return new { success = true, offset, frames = count, clip.channels, all = Measure(data, threshold),
            channelMetrics = Enumerable.Range(default(int), clip.channels).Select(channel => new {
                channel, metrics = Measure(Enumerable.Range(default(int), count).Select(frame => data[frame * clip.channels + channel]).ToArray(), threshold)
            }).ToArray() };
    }
    private static object Output(Dictionary<string, object> arguments)
    {
        if (!arguments.TryGetValue("outputSamples", out var raw)) return null;
        int count = Convert.ToInt32(raw, CultureInfo.InvariantCulture);
        if (count == default(int)) return null;
        int one = Convert.ToInt32(true);
        if (count < default(int) || (count & (count - one)) != default(int)) throw new ArgumentException("outputSamples must be a positive power of two");
        var left = new float[count]; var right = new float[count];
        AudioListener.GetOutputData(left, default(int));
        AudioListener.GetOutputData(right, one);
        var threshold = ClipThreshold(arguments);
        var leftMetrics = Measure(left, threshold); var rightMetrics = Measure(right, threshold);
        return new {
            samples = count, frequency = AudioSettings.outputSampleRate, speakerMode = AudioSettings.speakerMode.ToString(),
            left = leftMetrics, right = rightMetrics, clipThreshold = threshold,
            leftRightEnergyRatio = rightMetrics.energy > default(double) ? (double?)(leftMetrics.energy / rightMetrics.energy) : null,
            note = "One requested output buffer per channel; aggregate output includes all playing audio"
        };
    }
    public object Execute(Dictionary<string, object> arguments, DevelopmentContext context)
    {
        var scenes = Resources.FindObjectsOfTypeAll<HSceneProc>().Where(scene => scene.gameObject.scene.IsValid() && scene.gameObject.activeInHierarchy).ToArray();
        var scene = scenes.SingleOrDefault();
        var control = scene != null && scene.flags != null ? scene.flags.ctrlCamera : null;
        string mode = ReflectionCommands.Text(arguments, "mode", "snapshot");
        if (mode == "restore") Restore();
        else if (mode == "camera-left") MoveCamera(control, arguments);
        else if (mode != "snapshot") throw new ArgumentException("mode must be snapshot, camera-left, or restore");
        var camera = control != null ? control.thisCmaera : null;
        var listener = Manager.Sound.Listener;
        var managedListener = ManagedListener();
        var listeners = Resources.FindObjectsOfTypeAll<AudioListener>().Where(value => value.gameObject.scene.IsValid()).ToArray();
        var mouths = scene != null && scene.flags != null ? scene.flags.transVoiceMouth : null;
        return new {
            utc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture), frame = Time.frameCount, mode, cameraTestActive = _restore != null,
            output = Output(arguments),
            soundListener = Pose(listener), nativeCameraController = Pose(control != null ? control.transform : null),
            managerAudioListener = managedListener != null ? new { managedListener.enabled, active = managedListener.gameObject.activeInHierarchy, pose = Pose(managedListener.transform) } : null,
            nativeRenderCamera = Pose(camera != null ? camera.transform : null), mainCamera = Pose(Camera.main != null ? Camera.main.transform : null),
            mouths = mouths != null ? mouths.Select(Pose).ToArray() : Array.Empty<object>(),
            listeners = listeners.Select(item => new { item.enabled, active = item.gameObject.activeInHierarchy, pose = Pose(item.transform),
                equalsSoundListener = item.transform == listener, attachedCamera = item.GetComponent<Camera>() != null }).ToArray(),
            cameras = Resources.FindObjectsOfTypeAll<Camera>().Where(item => item.gameObject.scene.IsValid()).Select(item => new { item.enabled,
                active = item.gameObject.activeInHierarchy, item.depth, item.cullingMask, target = item.targetTexture != null ? item.targetTexture.name : null, pose = Pose(item.transform) }).ToArray(),
            sources = Resources.FindObjectsOfTypeAll<AudioSource>().Where(item => item.gameObject.scene.IsValid()).Select(source => {
                var voice = source.GetComponent<Voice_Component>(); var mouth = voice != null ? voice.voiceTrans : null; var clip = source.clip;
                return new {
                    reference = context.Reflection.Pack(source), pose = Pose(source.transform), source.isPlaying, source.enabled, active = source.gameObject.activeInHierarchy,
                    nativeVoice = voice != null, voiceTransform = Pose(mouth), source.panStereo, source.spatialBlend, source.spatialize, source.spatializePostEffects,
                    source.volume, source.pitch, source.mute, source.bypassEffects, source.bypassListenerEffects, source.bypassReverbZones, source.reverbZoneMix,
                    source.minDistance, source.maxDistance, rolloff = source.rolloffMode.ToString(), source.time, source.timeSamples,
                    mixer = source.outputAudioMixerGroup != null ? source.outputAudioMixerGroup.name : null,
                    clip = clip != null ? new { reference = context.Reflection.Pack(clip), clip.name, clip.channels, clip.frequency, clip.samples, clip.length, state = clip.loadState.ToString() } : null,
                    pcm = source.isPlaying ? Pcm(source, arguments) : null,
                    fromSoundListener = Relative(listener, source.transform), fromNativeCamera = Relative(camera != null ? camera.transform : null, source.transform),
                    mouthFromNativeCamera = Relative(camera != null ? camera.transform : null, mouth),
                    fromListeners = listeners.Select(item => new { id = item.GetInstanceID(), item.enabled, active = item.gameObject.activeInHierarchy, relative = Relative(item.transform, source.transform) }).ToArray()
                };
            }).ToArray()
        };
    }
    public void Dispose() => Restore();
}
