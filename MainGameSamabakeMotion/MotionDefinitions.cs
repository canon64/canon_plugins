using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using AnimationLoader;

namespace MainGameSamabakeMotion
{
    [DataContract] internal sealed class MotionDefinitions
    {
        [DataMember] public int version = 0;
        [DataMember] public PlaybackPose[] poses = null;
        internal static PlaybackPose[] Read(string path)
        {
            MotionDefinitions value;
            using (var stream = File.OpenRead(path)) value = (MotionDefinitions)new DataContractJsonSerializer(typeof(MotionDefinitions)).ReadObject(stream);
            if (value == null || value.version != 2 || value.poses == null || value.poses.Length == 0) throw new InvalidDataException("サマバケ通常H定義がありません");
            var ids = new HashSet<string>();
            foreach (var pose in value.poses)
            {
                if (pose == null || string.IsNullOrEmpty(pose.guid) || !pose.guid.StartsWith("canon.samabake.", StringComparison.Ordinal)
                    || !ids.Add(pose.guid + ":" + pose.studioId) || pose.mode < 0 || pose.mode > 7
                    || pose.states == null || pose.states.Length == 0 || !pose.states.Contains("Idle")
                    || pose.actors == null || pose.actors.Length == 0) throw new InvalidDataException("サマバケ通常H定義が不正です");
                var slots = new HashSet<string>();
                foreach (var actor in pose.actors)
                    if (actor == null || !slots.Add(actor.slot) || !new[] { "female0", "female1", "male0" }.Contains(actor.slot)
                        || string.IsNullOrEmpty(actor.controller) || string.IsNullOrEmpty(actor.path)
                        || !actor.path.StartsWith("anim_imports/canon/samabake/", StringComparison.Ordinal)
                        || actor.path.Contains("..") || actor.path.Contains(":")) throw new InvalidDataException("人物またはアセット参照が不正です");
                if (pose.actors.Any(a => !slots.Contains(a.partner))) throw new InvalidDataException("相手の人物が定義されていません");
                string[] required = pose.mode == 5 ? new[] { "female0", "female1" }
                    : pose.mode >= 6 ? new[] { "female0", "female1", "male0" }
                    : pose.mode == 3 ? new[] { "female0" } : new[] { "female0", "male0" };
                if (!slots.SetEquals(required) || pose.states.Distinct().Count() != pose.states.Length) throw new InvalidDataException("通常Hの人物構成または状態が不正です");
                foreach (var actor in pose.actors) actor.contact?.Validate(pose);
                if (pose.actors.Any(a => a.contact != null) && pose.actors.Any(a => a.contact == null)) throw new InvalidDataException("人物の接触補正が揃っていません");
            }
            return value.poses;
        }
    }
    [DataContract] internal sealed class PlaybackPose
    {
        [DataMember] public string guid;
        [DataMember] public int studioId;
        [DataMember] public int mode;
        [DataMember] public string name = null;
        [DataMember] public string[] states;
        [DataMember] public ActorDefinition[] actors;
        [DataMember] public int[] touchAreas;
        internal bool Matches(SwapAnimationInfo value)
        {
            if (value == null || guid != value.Guid || studioId != value.StudioId || mode != (int)value.Mode) return false;
            foreach (var actor in actors)
            {
                string path = actor.slot == "male0" ? value.PathMale : actor.slot == "female1" ? value.PathFemale1 : value.PathFemale;
                string controller = actor.slot == "male0" ? value.ControllerMale : actor.slot == "female1" ? value.ControllerFemale1 : value.ControllerFemale;
                if (path != actor.path || controller != actor.controller) return false;
            }
            return true;
        }
    }
    [DataContract] internal sealed class ActorDefinition
    {
        [DataMember] public string slot;
        [DataMember] public string partner;
        [DataMember] public string path;
        [DataMember] public string controller;
        [DataMember] public string[] correctiveStates = null;
        [DataMember] public ContactDefinition contact = null;
    }
}
