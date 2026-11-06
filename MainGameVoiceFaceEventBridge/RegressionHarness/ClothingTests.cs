using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using MainGameVoiceFaceEventBridge.Infrastructure;

namespace MainGameVoiceFaceEventBridge.RegressionHarness
{
    internal static partial class Program
    {
        private static void CheckClothing()
        {
            var actor = new object(); var coordinate = new object();
            var saved = new ClothingSnapshot { CoordinateName = "私服", Token = "test-token", Actor = actor, Coordinate = coordinate, Epoch = 5, Main = 1 };
            string[] states = { "着衣", "半脱", "半脱１", "半脱２", "脱衣" };
            for (int i = 0; i < 9; i++) saved.Add(i, i + 100, i < 5, i == 0 ? "スカート柄ブラウス\"青\"" : "服" + i, states[i % 5]);
            saved.AddAccessory("MODの\"黒縁眼鏡\"", true);
            saved.AddAccessory("目隠し", false);
            var serializer = new JavaScriptSerializer();
            string json = ClothingJson.Serialize(saved);
            var envelope = serializer.Deserialize<System.Collections.Generic.Dictionary<string, object>>(json);
            var parsed = (System.Collections.Generic.Dictionary<string, object>)envelope["clothing"];
            Assert(parsed.Count == 11 && parsed["衣装"].Equals("私服") && parsed["外履き"] == null, "coordinate name plus nine parts accessories and null");
            var glasses = (System.Collections.Generic.Dictionary<string, object>)parsed["眼鏡・目隠し"];
            Assert(glasses.Count == 2 && glasses["名前"].Equals("MODの\"黒縁眼鏡\"") && glasses["状態"].Equals("装着"), "visible accessory name round trip with no slot IDs");
            var hidden = new ClothingSnapshot();
            Assert(hidden.Glasses == null, "unequipped eye item is null");
            hidden.AddAccessory("目隠し", false);
            Assert(hidden.Glasses.Name == "目隠し" && hidden.Glasses.State == "外している", "hidden eye item retains name");
            hidden.AddAccessory("黒縁眼鏡", true);
            Assert(hidden.Glasses.Name == "黒縁眼鏡" && hidden.Glasses.State == "装着", "one eye item prefers visible slot");
            Assert(!parsed.ContainsKey("アクセサリー"), "Grok receives eye item without implementation classification");
            Assert(!json.Contains("Ids") && !json.Contains("Epoch") && !json.Contains("Token"), "clothing contains no internal metadata");
            Assert(((System.Collections.Generic.Dictionary<string, object>)parsed["トップス"])["衣装"].Equals(saved.Names[0]), "quoted Japanese name round trip");
            Assert(states.All(s => json.Contains(s)), "all clothing labels preserved");
            Assert(ClothingJson.ReadMain("{\"main\":-1}\r") == -1, "query negative main selects configured default");
            Assert(ClothingJson.Serialize(null).Contains("false"), "query error envelope");
            bool missing = false;
            try { new ClothingSnapshot().Add(0, 100, true, null, "着衣"); } catch (InvalidOperationException) { missing = true; }
            Assert(missing, "missing name is failure, not unequipped");
            string masked;
            var hits = saved.Match(saved.Names[0] + "を脱ぐね", out masked);
            Assert(hits.Count == 1 && hits[0].Kind == 0 && !masked.Contains("スカート") && !masked.Contains("ブラ"), "name wins over generic part substrings");
            Assert(hits[0].Index == (saved.Names[0] + "を").Length, "action retains speech offset");
            Assert(saved.Match("服1脱ぐね", out masked).Single().Kind == 1, "no particle");
            Assert(saved.Match("服1を着るね", out masked).Count == 0, "name trigger only removes");
            saved.Names[1] = "服"; saved.Names[2] = "長い服";
            Assert(saved.Match("長い服を脱ぐね", out masked).Single().Kind == 2, "longest name wins");
            saved.Names[1] = saved.Names[2] = "同じ服";
            Assert(saved.Match("同じ服を脱ぐね", out masked).Count == 0, "ambiguous name rejected");
            Assert(saved.Match("ブラは残して同じ服を脱ぐね", out masked).Count == 0 && !masked.Contains("脱ぐね"), "ambiguous name action cannot fall back to an earlier generic part");
            Assert(saved.Match("ブラの同じ服を脱ぐね", out masked).Single().Kind == 2, "part label disambiguates");
            var collision = new ClothingSnapshot { CoordinateName = "私服" };
            collision.Names[0] = "私服";
            var collisionHits = collision.Match("私服を脱ぐね", out masked);
            Assert(collisionHits.Count == 1 && collisionHits[0].Kind == 0, "garment wins coordinate exact-name collision without a part qualifier");
            Assert(collision.Match("私服脱ぐね", out masked).Single().Kind == 0, "garment coordinate collision without particle");
            collision.Names[0] = "ブラウス";
            Assert(collision.Match("私服を脱ぐね", out masked).Count == 0 && !masked.Contains("脱ぐね"), "coordinate alone has no undress action");
            collision.CoordinateName = "ブラ";
            Assert(collision.Match("ブラを脱ぐね", out masked).Count == 0 && !masked.Contains("ブラ") && !masked.Contains("脱ぐね"), "coordinate-only mention cannot trigger legacy generic part");
            collision.CoordinateName = "スクール水着";
            collision.Names[0] = "水着";
            Assert(collision.Match("スクール水着を脱ぐね", out masked).Count == 0, "coordinate substring does not become a garment match");
            Assert(collision.Match("水着を脱ぐね", out masked).Single().Kind == 0, "garment name still works separately from longer coordinate");
            int[] ids = (int[])saved.Ids.Clone(); string[] names = (string[])saved.Names.Clone();
            Assert(saved.MatchesIdentity(5, 1, actor, coordinate, ids, names), "unchanged outfit can execute later");
            Assert(!saved.MatchesIdentity(6, 1, actor, coordinate, ids, names), "old session rejected");
            Assert(!saved.MatchesIdentity(5, 0, actor, coordinate, ids, names), "target main switch rejected");
            Assert(!saved.MatchesIdentity(5, 1, new object(), coordinate, ids, names), "actor switch rejected");
            Assert(!saved.MatchesIdentity(5, 1, actor, new object(), ids, names), "coordinate reload rejected");
            ids[0]++;
            Assert(!saved.MatchesIdentity(5, 1, actor, coordinate, ids, names), "garment changed while waiting rejected");
            ids[0]--; names[0] = "別の服";
            Assert(!saved.MatchesIdentity(5, 1, actor, coordinate, ids, names), "name changed while waiting rejected");
            CheckClothingPipe();
            CheckClothingFailureRecovery();
        }

        private static void CheckClothingPipe()
        {
            string pipeName = "kks_clothing_test_" + Guid.NewGuid().ToString("N");
            var queryEntered = new ManualResetEventSlim();
            using (var server = new ClothingQueryServer(pipeName, (line, token) =>
            {
                if (line == "pending")
                {
                    var pending = new TaskCompletionSource<string>();
                    token.Register(() => pending.TrySetCanceled());
                    queryEntered.Set();
                    return pending.Task;
                }
                return Task.FromResult("{\"衣装\":\"テスト服\"}");
            }))
            {
                server.Start();
                using (var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut))
                {
                    client.Connect(2000);
                    using (var writer = new StreamWriter(client, new UTF8Encoding(false), 1024, true))
                    using (var reader = new StreamReader(client, new UTF8Encoding(false), false, 1024, true))
                    {
                        writer.AutoFlush = true; writer.WriteLine("{}");
                        var read = reader.ReadLineAsync();
                        Assert(read.Wait(3000) && read.Result.Contains("テスト服"), "real clothing pipe UTF8 round trip");
                    }
                }
                using (var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut))
                {
                    client.Connect(2000);
                    using (var writer = new StreamWriter(client, new UTF8Encoding(false), 1024, true))
                    {
                        writer.AutoFlush = true; writer.WriteLine("pending");
                        Assert(queryEntered.Wait(2000), "pending clothing query admitted");
                        server.Dispose();
                        server.Dispose();
                    }
                }
            }
            queryEntered.Dispose();
        }

        private static void CheckClothingFailureRecovery()
        {
            string pipeName = "kks_clothing_failure_" + Guid.NewGuid().ToString("N");
            using (var failed = new ManualResetEventSlim())
            using (var server = new ClothingQueryServer(pipeName, (line, cancel) =>
            {
                if (line == "bad") throw new InvalidOperationException("衣装メタデータがありません。");
                return Task.FromResult("ok");
            }, error => failed.Set()))
            {
                server.Start();
                using (var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut))
                {
                    client.Connect(2000);
                    using (var writer = new StreamWriter(client, new UTF8Encoding(false), 1024, true))
                    { writer.AutoFlush = true; writer.WriteLine("bad"); }
                    Assert(failed.Wait(2000), "request exception reaches failure notification callback");
                }
                using (var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut))
                {
                    client.Connect(2000);
                    using (var writer = new StreamWriter(client, new UTF8Encoding(false), 1024, true))
                    using (var reader = new StreamReader(client, new UTF8Encoding(false), false, 1024, true))
                    {
                        writer.AutoFlush = true; writer.WriteLine("good");
                        var response = reader.ReadLineAsync();
                        Assert(response.Wait(2000) && response.Result == "ok", "listener survives failed request");
                    }
                }
                failed.Reset();
                using (var silent = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut))
                {
                    silent.Connect(2000);
                    Assert(failed.Wait(5000), "silent client deadline cancels synchronous read and reports failure");
                }
            }
            using (var failed = new ManualResetEventSlim())
            using (var server = new ClothingQueryServer(string.Empty, (line, cancel) => Task.FromResult("unused"), error => failed.Set()))
            {
                server.Start();
                Assert(failed.Wait(2000), "listener creation failure reaches notification callback");
            }
            var serialized = new JavaScriptSerializer().Deserialize<System.Collections.Generic.Dictionary<string, object>>(
                ClothingJson.Serialize(null, "衣装メタデータがありません。"));
            Assert(serialized["error"].Equals("衣装メタデータがありません。"), "failure reason reaches Human_2_KKS");
        }
    }
}
