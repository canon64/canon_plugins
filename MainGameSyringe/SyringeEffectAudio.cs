using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace MainGameSyringe
{
 internal static class SyringeEffectAudio
 {
  internal const float MotionSeconds=3f;
  internal static AudioClip Create(bool refill)
  {
   string name=refill?"Refill02":"Injection01";
   using(var stream=typeof(SyringeEffectAudio).Assembly.GetManifestResourceStream("MainGameSyringe.Effects."+name+".wav"))
   {
    if(stream==null)throw new InvalidDataException("Missing syringe effect: "+name);
    using(var reader=new BinaryReader(stream,Encoding.ASCII))
    {
     if(new string(reader.ReadChars(4))!="RIFF")throw new InvalidDataException("Invalid effect RIFF");
     reader.ReadUInt32();
     if(new string(reader.ReadChars(4))!="WAVE")throw new InvalidDataException("Invalid effect WAVE");
     int channels=0,rate=0,bits=0,format=0;byte[] pcm=null;
     while(stream.Position+8<=stream.Length)
     {
      string id=new string(reader.ReadChars(4));int size=reader.ReadInt32();long next=stream.Position+size+(size&1);
      if(size<0||next>stream.Length)throw new InvalidDataException("Invalid effect chunk");
      if(id=="fmt ")
      {
       format=reader.ReadUInt16();channels=reader.ReadUInt16();rate=reader.ReadInt32();
       reader.ReadInt32();reader.ReadUInt16();bits=reader.ReadUInt16();
      }
      else if(id=="data")pcm=reader.ReadBytes(size);
      stream.Position=next;
     }
     if(format!=1||channels!=1||bits!=16||rate!=48000||pcm==null||pcm.Length!=288000)
      throw new InvalidDataException("Effect must be three-second 48 kHz mono PCM16");
     var samples=new float[pcm.Length/2];
     for(int i=0;i<samples.Length;i++)samples[i]=BitConverter.ToInt16(pcm,i*2)/32768f;
     var clip=AudioClip.Create(name,samples.Length,channels,rate,false);
     clip.SetData(samples,0);return clip;
    }
   }
  }
 }
}
