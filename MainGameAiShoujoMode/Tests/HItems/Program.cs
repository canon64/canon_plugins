using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MainGameAiShoujoMode;

public sealed class Gadget
{
    public int Primary, Deleted;
    public void PlaybackBarPrimaryAction() { Primary++; }
    public void PlaybackBarDelete() { Deleted++; }
    public string GetPlaybackBarActionHint() => "次はギズモ表示";
}
public sealed class Syringe
{
    public int Primary, Deleted;
    public void PrimaryAction() { Primary++; }
    public void DeleteSyringe() { Deleted++; }
    public string GetPrimaryActionHint() => "刺入→投与→抜く→補充";
}
internal static class Program
{
    static void Check(bool pass, string reason) { if (!pass) throw new Exception(reason); }
    static void Main()
    {
        Console.WriteLine("H item command boundary checks started");
        var values = JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixture.json")));
        int zero = values["zero"], one = values["one"], two = values["two"];
        bool active = true, allowed = true; int resolves = zero;
        var clients = new Dictionary<string, object>();
        foreach (var item in HItemCommands.Items) clients.Add(item.Guid, item.Label == "注射器" ? (object)new Syringe() : new Gadget());
        var commands = new HItemCommands(() => active, guid => allowed,
            guid => { resolves++; return clients.TryGetValue(guid, out var value) ? value : null; });
        for (int i = zero; i < HItemCommands.Items.Length; i++)
        {
            var item = HItemCommands.Items[i];
            commands.Invoke(i, false); commands.Invoke(i, false); commands.Invoke(i, true);
            if (clients[item.Guid] is Gadget gadget) Check(gadget.Primary == two && gadget.Deleted == one, item.Label + " uses Free H primary/delete API");
            else { var syringe = (Syringe)clients[item.Guid]; Check(syringe.Primary == two && syringe.Deleted == one, "syringe uses its dedicated API"); }
        }
        Check(clients.Count == values["count"], "all eight unique items");
        allowed = false; int before = resolves;
        Check(commands.Invoke(zero, false).Contains("許可"), "denied action is explained");
        Check(resolves == before, "denied action never resolves plugin");
        commands.Invoke(zero, true); Check(resolves == before + one, "cleanup works after revocation");
        active = false; before = resolves;
        commands.Invoke(zero, false); commands.Invoke(zero, true);
        Check(resolves == before, "old H UI cannot act after ending");
        active = true; allowed = true;
        clients.Remove(HItemCommands.Items[zero].Guid);
        Check(commands.Invoke(zero, false).Contains("読み込まれていません"), "missing plugin is visible");
        clients[HItemCommands.Items[zero].Guid] = new object();
        Check(commands.Invoke(zero, false).Contains("API"), "outdated plugin is visible");
        bool ready = false, menuFailed = false;
        HStartupCompletion.Complete(() => ready = true,
            () => throw new InvalidOperationException("native item UI failed"),
            error => { menuFailed = error.Message.Contains("item UI"); });
        Check(ready && menuFailed, "item UI failure preserves successful native H readiness");
        bool readyFailureEscaped = false, itemAttempted = false;
        try
        {
            HStartupCompletion.Complete(() => throw new InvalidOperationException("native startup failed"),
                () => itemAttempted = true, error => menuFailed = true);
        }
        catch (InvalidOperationException) { readyFailureEscaped = true; }
        Check(readyFailureEscaped && !itemAttempted, "native startup failure still reaches the native failure boundary");
        HStartupCompletion.Complete(() => ready = true, null, error => throw error);
        Console.WriteLine("PASS: eight routes, repeated primary, delete, syringe, denial, revoked cleanup, end, missing API/plugin");
    }
}
