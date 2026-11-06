using System;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using MainGameVoiceFaceEventBridge.Infrastructure;

namespace MainGameVoiceFaceEventBridge.RegressionHarness
{
    internal static partial class Program
    {
        // Transport fixture for Python -> PowerShell -> production C# pipe tests, never a game process.
        private static int RunClothingFixture(string name)
        {
            var saved = new ClothingSnapshot { Token = "fixture-only", CoordinateName = "私服" };
            for (int i = 0; i < 9; i++) saved.Add(i, i + 100, i == 0, "MODの\"青い服\"", "半脱２");
            saved.AddAccessory("黒縁眼鏡", true);
            saved.AddAccessory("目隠し", false);
            using (var server = new ClothingQueryServer(name, (line, cancel) => Task.FromResult(ClothingJson.Serialize(saved))))
            {
                server.Start();
                Console.WriteLine("READY"); Console.Out.Flush();
                Console.ReadLine();
            }
            return 0;
        }
    }
}
