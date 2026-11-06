using System;
using System.IO;

namespace MainGameStartupLogReset
{
    internal sealed class CleanupResult
    {
        internal int Deleted;
        internal int Failed;
        internal int Linked;
    }

    internal static class LogCleaner
    {
        // Explicit owners: never recurse through the entire BepInEx/plugins tree.
        internal static readonly string[] PluginFolders =
        {
            "AstralMode", "LiquidEffectPreview", "MainGameAdvIkBridge", "MainGameAgentCamera",
            "MainGameAibuPoseClones", "MainGameAlbumAspect", "MainGameAllPoseMap", "MainGameAutoHVoice",
            "MainGameBeatSyncSpeed", "MainGameBlankMapAdd", "MainGameCameraControl", "MainGameCharacterAfterimage",
            "MainGameCharacterMorphBridge", "MainGameCircularDisplacement", "MainGameClothColliderCards",
            "MainGameClothesLoadProbe", "MainGameClubLights", "MainGameCoordinateAccessoryRestore",
            "MainGameCoordinateCardSwap", "MainGameDisplacementMap", "MainGameDollMode", "MainGameFaceSweatControl",
            "MainGameFreeHMasturbationMenu", "MainGameGlow", "MainGameHandySync", "MainGameHHandPoseProbe",
            "MainGameHScenePoseInjector", "MainGameKaleidoscope", "MainGameLiveLookCamera", "MainGameLogRelay",
            "MainGameMapOverlayProbe", "MainGameMilkSpray", "MainGameObjectComposer", "MainGamePortraitCapture",
            "MainGamePoseCatalogCapture", "MainGamePregnancyPlusBridge", "MainGameRhythmSync",
            "MainGameSemanticPoseBridge", "MainGameSpeedLimitBreak", "MainGameStudioItemSummon",
            "MainGameSubCameraDisplayProbe", "MainGameSubtitleCore", "MainGameSubtitleEventBridge",
            "MainGameTransformGizmo", "MainGameUiHelp", "MainGameUiInputCapture", "MainGameUnifiedUi",
            "MainGameVMDPlay", "MainGameVoiceBreastMorph", "MainGameVoiceFaceEventBridge", "MainGameVoiceImpactBoost",
            "MainGameVrAfterimageLab", "MainGameVrDildo", "MainGameVrHipIkVoiceImpactBoost", "MainGameVrNeckChain",
            "MainGameVrScreenEffectProbe", "MainGameVrVoiceGate", "MainGirlHipHijack", "MainGirlShoulderIkStabilizer",
            "SimpleAfterimage", "SimpleAfterimage_VR", "StudioCharaStateLogger", "StudioFacePresetTool",
            "UnityResourceDiagnostics", "CoordCatalog"
        };

        internal static CleanupResult Clean(string bepInExRoot, Action<string, string> failure, Action<string> linked)
        {
            var run = new Cleanup(bepInExRoot, failure, linked);
            run.Execute();
            return run.Result;
        }

        private sealed class Cleanup
        {
            private readonly string _root;
            private readonly string _rootPrefix;
            private readonly Action<string, string> _failure;
            private readonly Action<string> _linked;
            internal readonly CleanupResult Result = new CleanupResult();

            internal Cleanup(string root, Action<string, string> failure, Action<string> linked)
            {
                _root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                _rootPrefix = _root + Path.DirectorySeparatorChar;
                _failure = failure;
                _linked = linked;
            }

            internal void Execute()
            {
                // Check ancestors too: an otherwise ordinary directory below a junction
                // would still access files outside the configured installation.
                if (!SafeAncestors(_root)) return;
                string plugins = Path.Combine(_root, "plugins");
                string canon = Path.Combine(plugins, "canon_plugins");
                if (SafeDirectory(plugins) && SafeDirectory(canon))
                {
                    foreach (string owner in PluginFolders)
                        Visit(Path.Combine(canon, owner), owner, "");

                    // LogRelay can route GUID owners into com/log and unknown_owner/log.
                    foreach (string route in new[] { "com", "unknown_owner" })
                    {
                        string parent = Path.Combine(canon, route);
                        if (SafeDirectory(parent)) Visit(Path.Combine(parent, "log"), route, "");
                    }
                }
                Visit(Path.Combine(_root, "clothes-load-probe"), "@clothes-load-probe", "");
            }

            private bool SafeAncestors(string path)
            {
                string parent = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(parent) && !SafeAncestors(parent)) return false;
                return SafeDirectory(path, checkBoundary: false);
            }

            private bool SafeDirectory(string path, bool checkBoundary = true)
            {
                if (checkBoundary && !WithinRoot(path)) return false;
                FileAttributes? attrs = Attributes(path);
                return attrs.HasValue && (attrs.Value & FileAttributes.Directory) != 0;
            }

            private bool WithinRoot(string path)
            {
                string full = Path.GetFullPath(path);
                if (full.Equals(_root, StringComparison.OrdinalIgnoreCase)
                    || full.StartsWith(_rootPrefix, StringComparison.OrdinalIgnoreCase)) return true;
                Failed(path, "Path is outside the BepInEx directory");
                return false;
            }

            private FileAttributes? Attributes(string path)
            {
                try
                {
                    FileAttributes attrs = File.GetAttributes(path);
                    if ((attrs & FileAttributes.ReparsePoint) != 0)
                    {
                        Result.Linked++;
                        _linked?.Invoke(path);
                        return null;
                    }
                    return attrs;
                }
                catch (FileNotFoundException) { return null; }
                catch (DirectoryNotFoundException) { return null; }
                catch (Exception ex) { Failed(path, ex.Message); return null; }
            }

            private void Visit(string directory, string owner, string relative)
            {
                if (!SafeDirectory(directory)) return;
                string[] files;
                try { files = Directory.GetFiles(directory); }
                catch (Exception ex) { Failed(directory, ex.Message); files = new string[0]; }
                foreach (string path in files)
                {
                    string name = Path.GetFileName(path);
                    if (!IsLog(owner, relative + name)) continue;
                    if (!WithinRoot(path) || !Attributes(path).HasValue) continue;
                    try { File.Delete(path); Result.Deleted++; }
                    catch (Exception ex) { Failed(path, ex.Message); }
                }
                string[] children;
                try { children = Directory.GetDirectories(directory); }
                catch (Exception ex) { Failed(directory, ex.Message); return; }
                foreach (string child in children)
                    Visit(child, owner, relative + Path.GetFileName(child) + "/");
            }

            private void Failed(string path, string reason)
            {
                Result.Failed++;
                _failure?.Invoke(path, reason);
            }
        }

        internal static bool IsLog(string owner, string relativePath)
        {
            string name = Path.GetFileName(relativePath);
            if (owner == "@clothes-load-probe")
                return name.StartsWith("trace-", StringComparison.OrdinalIgnoreCase)
                    && name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);

            if (name.EndsWith(".log", StringComparison.OrdinalIgnoreCase)
                || name.IndexOf(".log.", StringComparison.OrdinalIgnoreCase) >= 0) return true;

            if ((owner == "MainGameBlankMapAdd" || owner == "MainGameStudioItemSummon")
                && FileOrGeneration(relativePath, "_logs/info.txt")) return true;
            if (owner == "MainGameBlankMapAdd" && relativePath.Equals("webcam_devices.txt", StringComparison.OrdinalIgnoreCase)) return true;
            if (owner == "UnityResourceDiagnostics"
                && (FileOrGeneration(relativePath, "_logs/resource_diagnostics.txt")
                    || FileOrGeneration(relativePath, "_logs/bake_mesh_trace.txt"))) return true;
            if (owner == "MainGameVrScreenEffectProbe" && FileOrGeneration(name, "MainGameVrScreenEffectProbe.txt")) return true;
            if (owner == "MainGameMilkSpray" && relativePath.StartsWith("_logs/", StringComparison.OrdinalIgnoreCase)
                && name.StartsWith("milk-performance", StringComparison.OrdinalIgnoreCase)
                && name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)) return true;
            return owner == "MainGameVoiceFaceEventBridge"
                && relativePath.StartsWith("receive-timing/", StringComparison.OrdinalIgnoreCase)
                && name.StartsWith("receive-", StringComparison.OrdinalIgnoreCase)
                && name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);
        }

        private static bool FileOrGeneration(string name, string file)
        {
            if (name.Equals(file, StringComparison.OrdinalIgnoreCase)) return true;
            if (!name.StartsWith(file + ".", StringComparison.OrdinalIgnoreCase)) return false;
            string suffix = name.Substring(file.Length + 1);
            if (suffix.Length == 0) return false;
            foreach (char c in suffix) if (c < '0' || c > '9') return false;
            return true;
        }
    }
}
