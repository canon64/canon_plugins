using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using MainGameAiShoujoMode.Conversation;

internal static class Program
{
    private const string Managed = "F:/kks/KoikatsuSunshine_Data/Managed";
    private static int Main(string[] args)
    {
        Console.WriteLine("Start installed KKS managed media-constructor checks; no Unity scene or native engine APIs");
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            var path = Path.Combine(Managed, name.Name + ".dll");
            return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
        };
        return Run(args.Single());
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Run(string directory)
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "runtime-manifest.json")));
        var nativePath = Path.Combine(Managed, "Assembly-CSharp.dll");
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(nativePath))).ToLowerInvariant();
        Require(hash == manifest.RootElement.GetProperty("sourceAssemblySha256").GetString(), "Installed KKS assembly changed");
        var settings = manifest.RootElement.GetProperty("runtime"); var zero = settings.GetProperty("zero").GetInt32(); var one = settings.GetProperty("one").GetInt32();
        BgmPlaybackChecks.Run(directory, zero, one);
        SceneFadeChecks.Run(directory, zero, one);
        CameraHostChecks.Run(directory, zero, one);
        EventCgHostChecks.Run(directory, zero, one);
        EventCgSceneChecks.Run(directory, zero, one);
        EventCgPlacementChecks.Run(directory, settings, zero, one);
        PointHostChecks.Run(directory, settings.GetProperty("schema").GetInt32(), zero, one, settings.GetProperty("koikatsuPointMax").GetInt32());
        CrossFadeChecks.Run(directory, zero, one);
        RenderPresetChecks.Run(directory, zero, one);
        var parser = new KksAdvMediaArguments(zero, one);
        KksAdvScript.Row Row(string command, params string[] cells) => new KksAdvScript.Row(zero, zero, true, command, cells);
        string Identity(string value) => value;
        Require(!parser.Voices(Row("Voice"), Identity).Any() && !parser.Motions(Row("Motion"), Identity, null).Any()
            && !parser.Expressions(Row("Expression"), Identity, null).Any(), "Default current-character index became an empty media operation");
        var explicitVoice = parser.Voices(Row("Voice", "actor", "bundle", "clip", "personality", "pitch"), value =>
            value == "actor" || value == "personality" ? one.ToString() : value == "pitch" ? one.ToString() : value).Single();
        Require(explicitVoice.no == one && explicitVoice.personality == one && explicitVoice.pitch == one
            && explicitVoice.usePitch && explicitVoice.usePersonality, "Explicit voice actor/personality/pitch or whole-variable replacement was lost");
        var appended = parser.Voices(Row("Voice", zero.ToString(), "first", "clipA", one.ToString(), one.ToString(),
            one.ToString(), "second", "clipB"), Identity);
        Require(appended.Count == one + one && appended.Last().asset == "clipB" && !appended.Last().usePitch, "Packed voice records were consumed at the wrong width");
        var shortAlias = new[] { "state", "controller-bundle", "controller", "ik-bundle", "ik" };
        var aliased = parser.Motions(Row("Motion", zero.ToString(), "alias", one.ToString(), "other-state"), Identity,
            name => name == "alias" ? shortAlias : null);
        Require(aliased.Count == one + one && aliased.First().stateName == "state" && aliased.First().assetName == "controller"
            && aliased.First().ikAssetName == "ik" && aliased.Last().stateName == "other-state", "Motion alias expansion lost the following actor record");
        var template = new Manager.Game.Expression { eyebrowOpen = one };
        var named = parser.Expressions(Row("Expression", zero.ToString(), "named", one.ToString(), "named"), Identity,
            (actor, name) => name == "named" ? template : null);
        Require(named.Count == one + one && named.First().no == zero && named.Last().no == one && named.Last().eyebrowOpen == one,
            "Named expression copies were consumed as literal numeric fields");

        using var graph = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "scenario-graph.json")));
        var needed = graph.RootElement.GetProperty("nodes").EnumerateArray().Select(node =>
            (Bundle: node.GetProperty("bundle").GetString(), Asset: node.GetProperty("asset").GetString())).ToHashSet();
        int voices = zero, motions = zero, expressions = zero, literalExpressions = zero, namedExpressions = zero;
        foreach (var bundle in needed.Select(key => key.Bundle).Distinct())
        {
            var sourcePath = Path.Combine(directory, "bundles", bundle.Replace('/', '_') + ".json");
            using var source = JsonDocument.Parse(File.ReadAllText(sourcePath));
            foreach (var asset in source.RootElement.EnumerateArray())
            {
                if (!needed.Contains((bundle, asset.GetProperty("name").GetString()))) continue;
                foreach (var raw in asset.GetProperty("data").GetProperty("list").EnumerateArray())
                {
                    var command = raw.GetProperty("command").GetString();
                    var cells = raw.GetProperty("_args").EnumerateArray().Select(cell => cell.GetString()).ToArray();
                    var row = Row(command, cells);
                    if (command == "Voice")
                    {
                        var voice = parser.Voices(row, Identity).Single();
                        using var input = cells.AsEnumerable().GetEnumerator();
                        input.MoveNext(); var actor = int.Parse(input.Current); input.MoveNext(); var audioBundle = input.Current; input.MoveNext(); var clip = input.Current;
                        Require(voice.no == actor && voice.bundle == audioBundle && voice.asset == clip && !voice.usePitch && !voice.usePersonality,
                            "Installed voice arguments changed"); voices++;
                    }
                    if (command == "Motion" || command == "CharaMotion")
                    {
                        var motion = parser.Motions(row, Identity, null).Single();
                        using var input = cells.AsEnumerable().GetEnumerator();
                        input.MoveNext(); var actor = int.Parse(input.Current); input.MoveNext(); var state = input.Current;
                        Require(motion.no == actor && motion.stateName == state, "Installed motion actor/state changed");
                        if (input.MoveNext())
                        {
                            Require(motion.assetBundleName == input.Current, "Motion bundle changed"); input.MoveNext();
                            Require(motion.assetName == input.Current, "Motion controller changed"); input.MoveNext();
                            Require(motion.ikAssetBundleName == input.Current, "Motion IK bundle changed"); input.MoveNext();
                            Require(motion.ikAssetName == input.Current, "Motion IK name changed");
                        }
                        motions++;
                    }
                    if (command == "Expression" || command == "CharaExpression")
                    {
                        var literal = cells.Length == new ADV.Commands.Base.Expression().ArgsLabel.Length;
                        var expression = parser.Expressions(row, Identity, (actor, name) => literal ? null : template).Single();
                        Require(expression.no == int.Parse(cells[zero]), "Expression actor changed");
                        if (literal)
                        {
                            var cursor = zero; var direct = new ADV.Commands.Base.Expression.Data(cells, ref cursor);
                            Require(expression.eyebrow.ptn == direct.eyebrow.ptn && expression.eyes.ptn == direct.eyes.ptn && expression.mouth.ptn == direct.mouth.ptn
                                && expression.eyebrowOpen == direct.eyebrowOpen && expression.eyesOpen == direct.eyesOpen && expression.mouthOpen == direct.mouthOpen
                                && expression.eyesLook == direct.eyesLook && expression.hohoAkaRate == direct.hohoAkaRate && expression.isHighlight == direct.isHighlight
                                && expression.tearsLv == direct.tearsLv && expression.isBlink == direct.isBlink, "Literal expression fields differ from original constructor");
                            literalExpressions++;
                        }
                        else namedExpressions++;
                        expressions++;
                    }
                }
            }
        }
        Require(voices > zero && motions > zero && literalExpressions > zero && namedExpressions > zero, "Missing substantive native constructor fixtures");
        File.WriteAllText(Path.Combine(directory, "adv-native-media-argument-checks.json"), JsonSerializer.Serialize(new
        {
            scope = "Installed KKS managed data constructors and production argument decoder; no scene, ChaControl, audio or Animator execution",
            sourceAssemblySha256 = hash, voices, motions, expressions, literalExpressions, namedExpressions,
            namedLookup = "Synthetic expression template; installed named-expression assets are not resolved by this test",
            packedRecords = true, motionAliases = true, nativeDefaults = true, wholeVariableReplacement = true
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"PASS native media arguments voices={voices} motions={motions} expressions={expressions} literal={literalExpressions} named={namedExpressions}");
        MediaHostChecks.Run(directory, zero, one);
        return zero;
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
