using System;
using System.IO;
using System.Text;

namespace MainGameHCharacterMaker
{
    internal static class CardSaveIo
    {
        internal static string DirectoryFor(string gameRoot, byte sex)
        {
            if (sex != 0 && sex != 1) throw new ArgumentOutOfRangeException(nameof(sex));
            return Path.Combine(Path.GetFullPath(gameRoot), "UserData", "chara", sex == 0 ? "male" : "female", "freeH");
        }
        internal static string FileName(string characterName, DateTime utc, string uniqueId)
        {
            var name = new StringBuilder();
            foreach (char c in characterName ?? "")
            {
                if (name.Length >= 60) break;
                name.Append(c < 32 || "<>:\"/\\|?*".IndexOf(c) >= 0 ? '_' : c);
            }
            string safe = name.ToString().Trim(' ', '.');
            if (safe.Length == 0) safe = "キャラ";
            return "freeH_" + utc.ToUniversalTime().AddHours(9).ToString("yyyyMMdd_HHmmssfff",
                System.Globalization.CultureInfo.InvariantCulture) + "_" + safe + "_" + uniqueId + ".png";
        }
        internal static string SaveNew(string directory, string fileName, Func<bool> permitted,
            Action<BinaryWriter> write, Action<string> validate)
        {
            if (Path.GetFileName(fileName) != fileName || !fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("保存するカード名が不正です。", nameof(fileName));
            Require(permitted);
            Directory.CreateDirectory(directory);
            string target = Path.Combine(directory, fileName);
            string temporary = Path.Combine(directory, ".freeH_" + Guid.NewGuid().ToString("N") + ".partial");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new BinaryWriter(stream))
                {
                    Require(permitted);
                    write(writer); writer.Flush(); stream.Flush(true);
                }
                Require(permitted);
                validate(temporary);
                Require(permitted);
                File.Move(temporary, target); // Never overwrite another card, including on a name collision.
                return target;
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        private static void Require(Func<bool> permitted)
        { if (!permitted()) throw new InvalidOperationException("Hの編集許可または保存対象が終了・変更されました。"); }
    }
}
