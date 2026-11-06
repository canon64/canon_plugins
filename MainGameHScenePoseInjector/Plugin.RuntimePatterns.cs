using System;
using System.Collections.Generic;

namespace MainGameHScenePoseInjector
{
    public sealed partial class Plugin
    {
        private void ReloadExpressionPatterns()
        {
            if (!CanonPlugins.Shared.PluginSessionGate.Allowed) return;
            _patternListLoadAttempted = false;
            EnsurePatternsLoaded();
        }

        private void MergeRuntimePatternLists(Dictionary<int, PatternEntry> eyebrows,
            Dictionary<int, PatternEntry> eyes, Dictionary<int, PatternEntry> mouth)
        {
            // Character controllers and the registered eye table already include MOD additions.
            // Read once at H start, or on the explicit reload button; never from a frame callback.
            foreach (ChaControl actor in ResolveFemales())
            {
                try
                {
                    AddControllerPatterns(eyebrows, actor.eyebrowCtrl);
                    AddControllerPatterns(mouth, actor.mouthCtrl);
                    var registeredEyes = actor.lstCtrl?.GetCategoryInfo(ChaListDefine.CategoryNo.cha_eyeset);
                    if (registeredEyes == null) continue;
                    foreach (var entry in registeredEyes)
                    {
                        if (entry.Value == null) continue;
                        ExpressionPatternCatalog.AddRegistered(eyes, entry.Key,
                            entry.Value.GetInfo(ChaListDefine.KeyType.Name));
                    }
                }
                catch (Exception ex) { LogWarn("runtime expression catalog: " + ex.Message); }
            }
        }

        private static void AddControllerPatterns(Dictionary<int, PatternEntry> table, FBSBase controller)
        {
            var targets = controller?.FBSTarget;
            if (targets == null || targets.Length == 0) return;
            int count = int.MaxValue;
            foreach (var target in targets)
            {
                if (target?.PtnSet == null) return;
                count = Math.Min(count, target.PtnSet.Length);
            }
            ExpressionPatternCatalog.AddControllerPatterns(table, count, id =>
            {
                foreach (var target in targets)
                {
                    var pattern = target.PtnSet[id];
                    var mesh = target.GetSkinnedMeshRenderer()?.sharedMesh;
                    if (pattern == null || mesh == null) continue;
                    int shape = pattern.Close;
                    if (shape < 0 || shape >= mesh.blendShapeCount) shape = pattern.Open;
                    if (shape < 0 || shape >= mesh.blendShapeCount) continue;
                    return "追加表情 " + id + " / " + mesh.GetBlendShapeName(shape);
                }
                return null;
            });
        }
    }
}
