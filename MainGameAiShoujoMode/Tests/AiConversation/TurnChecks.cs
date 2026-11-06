using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Encodings.Web;
using MainGameAiShoujoMode.Conversation;

internal static class TurnChecks
{
    internal static void Run(Action<bool,string> check)
    {
        var names=new[]{"タマゴ","ゆでタマゴ","疑似餌","M","S"};
        check(!AiConversationItemOffers.MentionedNames("ありがとうございました",names).Any(),"ordinary thanks never enumerates inventory");
        check(AiConversationItemOffers.MentionedNames("ゆでタマゴをあげる",names).SequenceEqual(new[]{"ゆでタマゴ"}),"longer exact item mention does not offer another egg");
        check(AiConversationItemOffers.MentionedNames("タマゴとゆでタマゴを渡す",names).OrderBy(n=>n).SequenceEqual(new[]{"タマゴ","ゆでタマゴ"}.OrderBy(n=>n)),"two explicit item names remain distinct");
        check(!AiConversationItemOffers.MentionedNames("Thanks Mister",names).Any(),"short ASCII item names cannot match inside ordinary words");
        var action=new AiReplyAction{Action="talk",Label="long static explanation"};
        var wire=AiConversationItemOffers.ForWire(new[]{action});
        check(wire.Single().Label==null&&action.Label!=null,"static action explanation is not repeated or erased from local model");
        var file=@"C:\Users\youzo\.codex\attachments\8fd6fd78-d8f1-41a1-bd83-3a75d18d4d3f\貼り付けたテキスト.txt";
        if(!File.Exists(file))return;
        using var document=JsonDocument.Parse(File.ReadAllText(file));var turn=document.RootElement;
        var all=turn.GetProperty("available_actions").EnumerateArray().ToArray();
        string Name(JsonElement a){var label=a.GetProperty("label").GetString();var begin=label.IndexOf('「')+Math.Sign(int.MaxValue);return label.Substring(begin,label.IndexOf('」')-begin);}
        var inventoryNames=all.Where(a=>a.GetProperty("action").GetString()=="receive_item").Select(Name).ToArray();
        var mentioned=AiConversationItemOffers.MentionedNames(turn.GetProperty("player_message").GetString(),inventoryNames);
        var compact=all.Where(a=>a.GetProperty("action").GetString()!="receive_item"||mentioned.Contains(Name(a))).Select(a=>new AiReplyAction
        {Action=a.GetProperty("action").GetString(),Target=a.GetProperty("target_id").ValueKind==JsonValueKind.Null?null:a.GetProperty("target_id").GetString(),Label=a.TryGetProperty("label",out var label)?label.GetString():null}).ToArray();
        var bytes=Encoding.UTF8.GetByteCount(AiConversationJson.Write(AiConversationItemOffers.ForWire(compact)));
        check(!compact.Any(a=>a.Action=="receive_item"),"actual pasted request excludes all unsolicited receive_item entries");
        check(bytes<new FileInfo(file).Length/compact.Length,"actual pasted action list stays a small fraction of original request");
        var compactTurn=JsonNode.Parse(File.ReadAllText(file));compactTurn["available_actions"]=JsonNode.Parse(AiConversationJson.Write(AiConversationItemOffers.ForWire(compact)));
        var json=compactTurn.ToJsonString(new JsonSerializerOptions{Encoder=JavaScriptEncoder.UnsafeRelaxedJsonEscaping});
        var output=Path.Combine(Environment.CurrentDirectory,"_diagnostics","ai-conversation-turn");Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"compact-turn.json"),json,new UTF8Encoding(false));
        Console.WriteLine("AI_TURN_REGRESSION_PASS originalActions="+all.Length+" compactActions="+compact.Length+" actionBytes="+bytes+" requestBytes="+Encoding.UTF8.GetByteCount(json));
    }
}
