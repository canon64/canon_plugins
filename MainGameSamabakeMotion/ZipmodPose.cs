using AnimationLoader;
using System.Collections.Generic;
using System.Linq;

namespace MainGameSamabakeMotion
{
    // Registration belongs exclusively to AnimationLoader. Never insert list entries here.
    internal static class ZipmodPose
    {
        internal const string Guid = "canon.samabake.native.kiss";
        internal const int StudioId = 192610300;
        internal const string FemaleController = "svs_native_aib00_female";
        internal const string MaleController = "svs_native_aib00_male";
        internal const string FemalePath = "anim_imports/canon/samabake/kiss/aib_female_00_native.unity3d";
        internal const string MalePath = "anim_imports/canon/samabake/kiss/aib_male_00_native.unity3d";
        internal static readonly string[] States = { "Idle", "K_Touch", "K_Loop", "Orgasm_Start", "Orgasm_Loop", "Orgasm_A" };
        private static Dictionary<string, PlaybackPose> _definitions = new[] { Legacy() }.ToDictionary(p => p.guid + ":" + p.studioId);
        private static PlaybackPose Legacy() => new PlaybackPose
        {
            guid = Guid, studioId = StudioId, mode = 0, states = States, touchAreas = new[] { 0 },
            actors = new[] {
                new ActorDefinition {slot="female0",partner="male0",path=FemalePath,controller=FemaleController},
                new ActorDefinition {slot="male0",partner="female0",path=MalePath,controller=MaleController}}
        };
        internal static void Load(string path) => _definitions = MotionDefinitions.Read(path).ToDictionary(p => p.guid + ":" + p.studioId);
        internal static PlaybackPose Find(HSceneProc.AnimationListInfo entry)
        {
            if (entry == null || SwapAnim.SwapAnimationMapping == null || !SwapAnim.SwapAnimationMapping.TryGetValue(entry, out var value)) return null;
            return _definitions.TryGetValue(value.Guid + ":" + value.StudioId, out var pose) && pose.Matches(value) ? pose : null;
        }

        internal static bool Matches(SwapAnimationInfo value) => value != null && value.Guid == Guid
            && value.StudioId == StudioId && (int)value.Mode == 0
            && value.ControllerFemale == FemaleController && value.ControllerMale == MaleController
            && value.PathFemale == FemalePath && value.PathMale == MalePath;

        internal static bool IsImported(HSceneProc.AnimationListInfo entry) => Find(entry) != null;
    }
}
