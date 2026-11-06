using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;

namespace MainGameSamabakeMotion
{
    [DataContract] public sealed class NativeCatalog
    {
        [DataMember(IsRequired = true)] public int version;
        [DataMember(IsRequired = true)] public NativePose[] poses;
        [DataMember] public string[] skipped;
        public static NativeCatalog Read(string path)
        {
            NativeCatalog value;
            using (var stream = File.OpenRead(path)) value = (NativeCatalog)new DataContractJsonSerializer(typeof(NativeCatalog)).ReadObject(stream);
            if (value == null || value.version != 1 || value.poses == null || value.poses.Length == 0) throw new InvalidDataException("通常Hの体位一覧がありません");
            var ids = new HashSet<int>();
            foreach (var pose in value.poses)
            {
                if (pose == null || !ids.Add(pose.id) || pose.id < 190000000 || pose.mode != "aibu" || pose.operation != "kiss" || pose.name == null || !pose.name.StartsWith("サマバケ・", StringComparison.Ordinal)) throw new InvalidDataException("通常Hの登録定義が不正です");
                if (pose.female == null || pose.male == null || pose.states == null || pose.states.Length == 0) throw new InvalidDataException("男女または状態対応が不足しています");
                string[] required = { "Idle", "K_Touch", "K_Loop", "Orgasm_Start", "Orgasm_Loop", "Orgasm_A" };
                if (pose.states.Distinct().Count() != pose.states.Length || required.Any(s => !pose.states.Contains(s))) throw new InvalidDataException("KKSの進行に必要な状態が不足しています");
                foreach (var actor in new[] { pose.female, pose.male })
                    if (actor.file == null || Path.GetFileName(actor.file) != actor.file || actor.file.Contains(":") || string.IsNullOrEmpty(actor.controller) || actor.sha256 == null || actor.sha256.Length != 64) throw new InvalidDataException("再生アセット定義が不正です");
            }
            return value;
        }
        public static void CheckAsset(string directory, NativeActor actor)
        {
            using (var stream = File.OpenRead(Path.Combine(directory, "NativeMotions", actor.file)))
            using (var sha = SHA256.Create())
                if (BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() != actor.sha256) throw new InvalidDataException("検査済みアセットと一致しません: " + actor.file);
        }
    }
    [DataContract] public sealed class NativePose
    {
        [DataMember(IsRequired = true)] public int id;
        [DataMember(IsRequired = true)] public int templateId;
        [DataMember(IsRequired = true)] public string name;
        [DataMember(IsRequired = true)] public string mode;
        [DataMember(IsRequired = true)] public string operation;
        [DataMember(IsRequired = true)] public string[] states;
        [DataMember(IsRequired = true)] public NativeActor female;
        [DataMember(IsRequired = true)] public NativeActor male;
    }
    [DataContract] public sealed class NativeActor
    {
        [DataMember(IsRequired = true)] public string file;
        [DataMember(IsRequired = true)] public string controller;
        [DataMember(IsRequired = true)] public string sha256;
    }
}
