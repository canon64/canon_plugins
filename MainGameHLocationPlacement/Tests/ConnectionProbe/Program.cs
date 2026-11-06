using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace MainGameHLocationPlacement
{
    internal static class ConnectionProbe
    {
        private static int Main(string[] args)
        {
            Console.WriteLine("Checking installed Composer placement connection without starting KKS.");
            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
            try { Run(args.Length == 0 ? "F:/kks/BepInEx/plugins/canon_plugins/MainGameObjectComposer/MainGameObjectComposer.dll" : args.Single()); Console.WriteLine("PASS: Composer runtime connection contract"); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        private static Assembly Resolve(object sender, ResolveEventArgs args)
        {
            string name = new AssemblyName(args.Name).Name + ".dll";
            foreach (string directory in new[] { "F:/kks/KoikatsuSunshine_Data/Managed", "F:/kks/BepInEx/core" })
            {
                string path = Path.Combine(directory, name);
                if (File.Exists(path)) return Assembly.LoadFrom(path);
            }
            var plugins = Directory.GetFiles("F:/kks/BepInEx/plugins", name, SearchOption.AllDirectories);
            return plugins.Length == 1 ? Assembly.LoadFrom(plugins.Single()) : null;
        }
        private static void Run(string path)
        {
            var composer = Assembly.LoadFrom(path);
            ComposerContract.Check(composer.GetType("MainGameObjectComposer.Plugin", true));
        }
    }
}
