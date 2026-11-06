using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace MainGameVrHandcuffs
{
    public sealed partial class Plugin
    {
        private AudioClip _attachClip;
        private readonly AudioSource[] _attachSources=new AudioSource[2];

        private void CreateAttachmentAudio()
        {
            using (var stream=typeof(Plugin).Assembly.GetManifestResourceStream("Handcuffs.AttachSound"))
            using (var reader=new BinaryReader(stream ?? throw new InvalidDataException("Missing handcuff sound"),Encoding.ASCII))
            {
                if (new string(reader.ReadChars(4))!="RIFF") throw new InvalidDataException("Handcuff sound RIFF");
                reader.ReadUInt32();
                if (new string(reader.ReadChars(4))!="WAVE") throw new InvalidDataException("Handcuff sound WAVE");
                int format=0,channels=0,rate=0,bits=0;byte[] pcm=null;
                while (stream.Position+8<=stream.Length)
                {
                    var id=new string(reader.ReadChars(4));int size=reader.ReadInt32();
                    long end=stream.Position+size+(size&1);
                    if (size<0 || end>stream.Length) throw new InvalidDataException("Handcuff sound chunk");
                    if (id=="fmt ")
                    {
                        if (size<16) throw new InvalidDataException("Handcuff sound format");
                        format=reader.ReadUInt16();channels=reader.ReadUInt16();rate=reader.ReadInt32();
                        reader.ReadInt32();reader.ReadUInt16();bits=reader.ReadUInt16();
                    }
                    else if (id=="data") pcm=reader.ReadBytes(size);
                    stream.Position=end;
                }
                if (format!=1 || channels!=1 || rate!=48000 || bits!=16 || pcm==null || pcm.Length!=82560)
                    throw new InvalidDataException("Handcuff sound must be the approved 0.86s mono PCM16 clip");
                var samples=new float[pcm.Length/2];
                for (int i=0;i<samples.Length;i++) samples[i]=BitConverter.ToInt16(pcm,i*2)/32768f;
                _attachClip=AudioClip.Create("HandcuffAttach",samples.Length,1,rate,false);
                _attachClip.SetData(samples,0);
            }
            for (int i=0;i<2;i++)
            {
                var source=_parts[i].gameObject.AddComponent<AudioSource>();
                _attachSources[i]=source;
                source.playOnAwake=false;source.loop=false;
                source.spatialBlend=1f;source.dopplerLevel=0f;
                source.rolloffMode=AudioRolloffMode.Logarithmic;
                source.minDistance=.25f;source.maxDistance=8f;source.volume=.8f;
                // The sample already contains the requested reverb.
                source.reverbZoneMix=0f;
            }
        }

        private void PlayAttachmentSound(int cuff)
        {
            if (CanAct && _attachClip!=null && _attachSources[cuff]!=null)
                _attachSources[cuff].PlayOneShot(_attachClip);
        }

        private void DestroyAttachmentAudio()
        {
            for (int i=0;i<2;i++)
            {
                if (_attachSources[i]!=null) _attachSources[i].Stop();
                _attachSources[i]=null;
            }
            if (_attachClip!=null) UnityEngine.Object.Destroy(_attachClip);
            _attachClip=null;
        }
    }
}
