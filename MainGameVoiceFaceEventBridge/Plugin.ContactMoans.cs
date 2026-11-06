using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using MainGameVoiceFaceEventBridge.Audio;
using UnityEngine;

namespace MainGameVoiceFaceEventBridge
{
    internal sealed partial class Plugin
    {
        private sealed class ContactPool
        {
            internal int Personality;
            internal readonly List<AudioClip> Clips=new List<AudioClip>();
            internal int Last=-1;
        }
        private readonly Dictionary<ChaControl,ContactPool> _contactPools=new Dictionary<ChaControl,ContactPool>();
        private readonly System.Random _contactRandom=new System.Random();
        private int _contactGeneration;
        private void BeginContactMoans()
        {
            EndContactMoans();
            foreach(var actor in SessionGate.Females)EnsureContactPool(actor);
        }
        private void EndContactMoans(){_contactGeneration++;_contactPools.Clear();}
        private bool TrySelectContactMoan(ChaControl actor,out AudioClip clip)
        {
            clip=null;
            var pool=EnsureContactPool(actor);
            if(pool==null||pool.Clips.Count==0)return false; // Never queue a contact while loading.
            int next=_contactRandom.Next(pool.Clips.Count);
            if(pool.Clips.Count>1&&next==pool.Last)next=(next+1+_contactRandom.Next(pool.Clips.Count-1))%pool.Clips.Count;
            pool.Last=next;clip=pool.Clips[next];return clip!=null;
        }
        private ContactPool EnsureContactPool(ChaControl actor)
        {
            if(!SessionGate.Allowed||actor==null)return null;
            int personality=actor.fileParam.personality;
            if(_contactPools.TryGetValue(actor,out var current)&&current.Personality==personality)return current;
            int index=-1;for(int i=0;i<SessionGate.Females.Count;i++)if(SessionGate.Females[i]==actor){index=i;break;}
            if(index<0)return null;
            var entries=new List<Tuple<string,string>>();var keys=new HashSet<string>(StringComparer.Ordinal);
            Action<string,string,string> add=(bundle,name,word)=>
            {
                if(string.IsNullOrEmpty(bundle)||!ContactCuePolicy.IsContactAsset(name)||!ContactCuePolicy.IsMoanText(word))return;
                bundle=string.Format(CultureInfo.InvariantCulture,bundle,personality);
                name=string.Format(CultureInfo.InvariantCulture,name,personality);
                if(keys.Add(bundle+"|"+name))entries.Add(Tuple.Create(bundle,name));
            };
            var voice=SessionGate.Voice;
            if(voice!=null&&voice.dicVoiceIntos!=null&&index<voice.dicVoiceIntos.Length&&voice.dicVoiceIntos[index]!=null)
                foreach(var groups in voice.dicVoiceIntos[index].Values)
                    foreach(var group in groups.Values)
                        foreach(var info in group.Values)
                            if(!string.IsNullOrWhiteSpace(info.word))add(info.pathAsset,info.nameFile,info.word);
            if(voice!=null&&voice.dicShortBreathIntos!=null&&index<voice.dicShortBreathIntos.Length&&voice.dicShortBreathIntos[index]!=null)
                foreach(var group in voice.dicShortBreathIntos[index].Values)
                    foreach(var info in group.Values)add(info.pathAsset,info.nameFile,info.word);
            if(voice!=null&&voice.dicBreathVoiceIntos!=null&&index<voice.dicBreathVoiceIntos.Length&&voice.dicBreathVoiceIntos[index]!=null)
                foreach(var groups in voice.dicBreathVoiceIntos[index].Values)
                    foreach(var group in groups.Values)
                        foreach(var info in group.Values)add(info.pathAsset,info.nameFile,info.word);
            if(entries.Count==0)ReadContactTables(personality,add);
            if(entries.Count==0)return null;
            for(int i=entries.Count-1;i>0;i--){int j=_contactRandom.Next(i+1);var swap=entries[i];entries[i]=entries[j];entries[j]=swap;}
            var pool=new ContactPool{Personality=personality};_contactPools[actor]=pool;
            _=LoadContactMoans(actor,pool,entries,_contactGeneration);return pool;
        }
        private static void ReadContactTables(int personality,Action<string,string,string> add)
        {
            // Same native tables and naming as HVoiceCtrl.Init, also usable before its setup
            // or when VMD playback does not create a native voice controller.
            string code=personality==-13?"c39":"c"+personality.ToString("00",CultureInfo.InvariantCulture);
            var bundles=HFlag.lstABName!=null&&HFlag.lstABName.Count>0?HFlag.lstABName:CommonLib.GetAssetBundleNameListFromPath("h/list/",false);
            foreach(string bundle in bundles)
            {
                string shortName="personality_shortbreath_"+code+"_00",breathName="personality_breath_"+code,voiceName="personality_voice_"+code;
                if(AssetBundleCheck.IsFile(bundle,voiceName))
                {
                    try
                    {
                        var table=CommonLib.LoadAsset<VoiceAllData>(bundle,voiceName,false,"",false);
                        if(table!=null)foreach(var mode in table.param)foreach(var row in mode.data)foreach(var info in row.info)
                            if(info!=null&&!string.IsNullOrWhiteSpace(info.word))add(info.pathAsset,info.nameFile,info.word);
                    }
                    finally{AssetBundleManager.UnloadAssetBundle(bundle,false);}
                }
                if(AssetBundleCheck.IsFile(bundle,shortName))
                {
                    try
                    {
                        var table=CommonLib.LoadAsset<ShortBreathData>(bundle,shortName,false,"",false);
                        if(table!=null)foreach(var row in table.param)foreach(var info in row.infos)
                            if(info!=null)add(info.pathAsset,info.nameFile,info.word);
                    }
                    finally{AssetBundleManager.UnloadAssetBundle(bundle,false);}
                }
                if(AssetBundleCheck.IsFile(bundle,breathName))
                {
                    try
                    {
                        var table=CommonLib.LoadAsset<BreathAllData>(bundle,breathName,false,"",false);
                        if(table!=null)foreach(var mode in table.param)foreach(var row in mode.data)foreach(var info in row.info)
                            if(info!=null)add(info.pathAsset,info.nameFile,info.word);
                    }
                    finally{AssetBundleManager.UnloadAssetBundle(bundle,false);}
                }
            }
        }
        private async Task LoadContactMoans(ChaControl actor,ContactPool pool,List<Tuple<string,string>> entries,int generation)
        {
            foreach(var entry in entries)
            {
                if(!ContactPoolCurrent(actor,pool,generation))return;
                AudioClip clip;
                try{clip=await new AssetBundleData(entry.Item1,entry.Item2).GetAssetAsync<AudioClip>();}
                catch{continue;}
                finally{AssetBundleManager.UnloadAssetBundle(entry.Item1,false);}
                if(!ContactPoolCurrent(actor,pool,generation))return;
                if(clip!=null&&ContactCuePolicy.IsShortMoan(clip.length)&&!pool.Clips.Contains(clip))pool.Clips.Add(clip);
                if(pool.Clips.Count>=64)return;
            }
        }
        private bool ContactPoolCurrent(ChaControl actor,ContactPool pool,int generation)=>
            generation==_contactGeneration&&SessionGate.Allowed&&actor!=null&&actor.fileParam.personality==pool.Personality&&
            _contactPools.TryGetValue(actor,out var current)&&ReferenceEquals(current,pool);
    }
}
