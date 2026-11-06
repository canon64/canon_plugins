using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using MainGameAiShoujoMode;

using var settings = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "settings.json")));
int Expected(string key) => settings.RootElement.GetProperty(key).GetInt32();
var directory = Path.GetFullPath(args.First());
var catalog = FurReceptionDialogueCatalog.Load(directory);
if (catalog.lines.Length != Expected("lines") || catalog.menus.Length != Expected("menus")) throw new Exception("Incomplete import");
foreach (var line in catalog.lines)
{
    if (catalog.GetLine(line.id) != line) throw new Exception("Lookup mismatch");
    using var stream = File.OpenRead(Path.Combine(directory, "voices", line.audio));
    if (!Convert.ToHexString(SHA256.HashData(stream)).Equals(line.audioSha256, StringComparison.OrdinalIgnoreCase))
        throw new Exception("Audio changed: " + line.id);
    try { catalog.RequireConnectedLine(line.id); throw new Exception("Unconnected line was executable"); }
    catch (FurReceptionNotConnectedException ex) { if (ex.ItemId != line.id) throw; }
}
foreach (var menu in catalog.menus)
{
    try { catalog.RequireConnectedMenu(menu.id); throw new Exception("Unconnected menu was executable"); }
    catch (FurReceptionNotConnectedException ex) { if (ex.ItemId != menu.id) throw; }
}
try { catalog.GetLine("missing"); throw new Exception("Unknown line accepted"); }
catch (KeyNotFoundException) { }
try { catalog.RequireConnectedMenu("missing"); throw new Exception("Unknown menu accepted"); }
catch (KeyNotFoundException) { }
if (catalog.lines.Count(x => x.textBasis == "original_scenario") != Expected("originalTexts")) throw new Exception("Original text coverage changed");
if (catalog.lines.Count(x => x.textBasis == "whisper_large_v3_unreviewed") != Expected("transcriptions")) throw new Exception("Transcription coverage changed");
Console.WriteLine("FUR_DIALOGUE_PASS: 65 audio hashes, 23 original texts, 42 transcriptions, 71 unconnected rejections, unknown IDs rejected");
