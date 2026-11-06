using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading.Tasks;
using MainGameDevelopmentBridge;

public sealed class Fixture { private int Value = 7; public int Add(int amount) => Value += amount; public string Add(string text) => text; }
internal static class Program
{
    static readonly BridgeJson Json = new BridgeJson();
    static void Check(bool success, string label) { if (!success) throw new Exception(label); Console.WriteLine("PASS " + label); }
    static Dictionary<string, object> Request(string text) => Json.Deserialize<Dictionary<string, object>>(text);
    static void Main()
    {
        Check(DevelopmentStartup.SelectMode(new[] { "KoikatsuSunshine.exe" }) == "none", "ordinary launch stays at title");
        Check(DevelopmentStartup.SelectMode(new[] { "KoikatsuSunshine.exe", "--kks-development-mode=ai" }) == "ai", "explicit AI development launch");
        Check(DevelopmentStartup.SelectMode(new[] { "--kks-development-mode=hotel" }) == "hotel", "explicit hotel development launch");
        Check(DevelopmentStartup.SelectMode(new[] { "--kks-development-mode=none" }) == "none", "explicit title launch");
        Check(DevelopmentStartup.SelectMode(new[] { "--kks-development-mode=bad" }) == "none", "invalid mode cannot enter");
        Check(DevelopmentStartup.SelectMode(new[] { "--kks-development-mode=" }) == "none", "empty mode cannot enter");
        Check(DevelopmentStartup.SelectMode(new[] { "prefix--kks-development-mode=ai", "AutoMode=ai" }) == "none", "unrelated arguments and legacy setting cannot enter");
        Check(DevelopmentStartup.SelectMode(new[] { "--kks-development-mode=ai", "--kks-development-mode=hotel" }) == "none", "duplicate startup arguments cannot enter");
        var commands = new ReflectionCommands();
        var unavailable = Request(Json.Serialize(commands.Execute(Request("{\"op\":\"notifications\"}"))));
        Check(!(bool)unavailable["available"] && (string)unavailable["reason"] == "service-not-initialized", "notifications distinguish unavailable from empty");
        MainGameNotifications.NotificationApi.Initialize(new MainGameNotifications.NotificationStore());
        var empty = Request(Json.Serialize(commands.Execute(Request("{\"op\":\"notifications\"}"))));
        Check((bool)empty["available"] && (int)empty["count"] == 0, "notifications empty service");
        MainGameNotifications.NotificationApi.Publish("test.owner", "復元エラー", "注射器：照合失敗\n成功分は適用済み", MainGameNotifications.NotificationSeverity.Failure, "test-id");
        var report = Request(Json.Serialize(commands.Execute(Request("{\"op\":\"notifications\"}"))));
        var notification = (Dictionary<string, object>)((object[])report["notifications"])[0];
        Check((int)report["count"] == 1 && (string)notification["body"] == "注射器：照合失敗\n成功分は適用済み", "notifications return complete Japanese body inline");
        Check((string)notification["ownerGuid"] == "test.owner" && (string)notification["id"] == "test-id" && (string)notification["severity"] == "Failure" && (string)notification["title"] == "復元エラー", "notifications include source and severity");
        Check(DateTime.Parse((string)notification["updatedUtc"], null, System.Globalization.DateTimeStyles.RoundtripKind).Kind == DateTimeKind.Utc, "notification time preserves UTC");
        Check(MainGameNotifications.NotificationApi.GetNotifications().Length == 1, "reading notifications does not dismiss failures");
        MainGameNotifications.NotificationApi.Shutdown();
        var created = Request(Json.Serialize(commands.Execute(Request("{\"op\":\"new\",\"type\":\"Fixture\"}"))));
        string handle = (string)created["handle"];
        var request = Request("{\"op\":\"get\",\"member\":\"Value\"}");request["handle"]=handle;
        Check((int)commands.Execute(request)==7,"private field read");
        request["op"]="set";request["value"]=19;commands.Execute(request);
        request=Request("{\"op\":\"invoke\",\"member\":\"Add\",\"args\":[4],\"parameterTypes\":[\"System.Int32\"]}");request["handle"]=handle;
        Check((int)commands.Execute(request)==23,"exact overload selection and mutation");
        request.Remove("parameterTypes");bool rejected=false;try{commands.Execute(request);}catch(System.Reflection.AmbiguousMatchException){rejected=true;}
        Check(rejected,"ambiguous invocation rejected");
        commands.Clear();rejected=false;try{commands.Resolve(handle);}catch(KeyNotFoundException){rejected=true;}
        Check(rejected,"stop invalidates object handles");
        var pipeName="KKS.Bridge.Test."+Guid.NewGuid().ToString("N");
        using(var server=new PipeServer(pipeName,r=>Task.FromResult<object>(r["op"])))
        {
            using(var client=new NamedPipeClientStream(".",pipeName,PipeDirection.InOut))
            {
                client.Connect(5000);
                using(var writer=new StreamWriter(client,new UTF8Encoding(false),4096,true){AutoFlush=true})
                using(var reader=new StreamReader(client,Encoding.UTF8,false,4096,true))
                {
                    writer.WriteLine("{\"op\":\"hello\"}");var response=Request(reader.ReadLine());
                    Check((bool)response["ok"] && (string)response["result"]=="hello","actual named pipe request and response");
                }
            }
            server.Dispose();Check(server.Completion.Wait(5000),"stopping listener releases blocked connection");
        }
        Console.WriteLine("DEVELOPMENT_BRIDGE_TESTS_PASS");
        var mailbox=Path.Combine(Path.GetTempPath(),"KksBridge-"+Guid.NewGuid().ToString("N"));
        using(var server=new MailboxServer(mailbox,r=>Task.FromResult<object>(r["op"])))
        {
            var input=Path.Combine(mailbox,"in","command.json");var output=Path.Combine(mailbox,"out","command.json");
            File.WriteAllText(input+".tmp","{\"op\":\"日本語テスト\"}");File.Move(input+".tmp",input);
            Check(System.Threading.SpinWait.SpinUntil(()=>File.Exists(output),5000),"mailbox notification receives atomic command");
            Check((string)((Dictionary<string,object>)Request(File.ReadAllText(output)))["result"]=="日本語テスト","mailbox UTF8 response");
        }
        Directory.Delete(mailbox,true);
    }
}
