using UnityEngine;

namespace CanonPlugins.Shared
{
    internal sealed class DedicatedVrButton : IDisposable
    {
        internal static Valve.VR.SteamVR_Action_Boolean Current;
        internal Valve.VR.SteamVR_Action_Boolean Action { get; }
        internal DedicatedVrButton(string name) { if(name!="handcuffs")throw new Exception("wrong B action set"); Action=Current=new(); }
        public void Dispose() { }
    }
}

namespace UnityEngine
{
    public class Object
    {
        public bool Destroyed;
        public static void Destroy(Object item) { item.Destroyed=true; }
    }
    public class AudioClip : Object
    {
        public float[] Samples;
        public int Rate;
        public static AudioClip Create(string name,int samples,int channels,int frequency,bool stream)
            =>new(){Samples=new float[samples],Rate=frequency};
        public bool SetData(float[] samples,int offset) { samples.CopyTo(Samples,offset);return true; }
    }
    public enum AudioRolloffMode {Logarithmic}
    public class AudioSource
    {
        public bool playOnAwake,loop,Stopped;
        public float spatialBlend,dopplerLevel,minDistance,maxDistance,volume,reverbZoneMix;
        public AudioRolloffMode rolloffMode;
        public int Plays;
        public void PlayOneShot(AudioClip clip) { Plays++;Stopped=false; }
        public void Stop() { Stopped=true; }
    }
}
namespace MainGameVrHandcuffs
{
    public sealed partial class Plugin
    {
        public void InitAudio()=>CreateAttachmentAudio();
        public void ClearAudio()=>DestroyAttachmentAudio();
        public AudioSource SoundSource(int i)=>_attachSources[i];
        public AudioClip SoundClip=>_attachClip;
    }
}
