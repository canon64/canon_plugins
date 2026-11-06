using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace MainGameAiShoujoMode
{
    internal static class HotelRoomActorEffects
    {
        private static readonly string[] Properties = { "TearLevel", "DroolLevel", "SalivaLevel", "SweatLevel", "BlushLevel", "BukkakeLevel", "AnalBukkakeLevel", "CumInNoseLevel", "ButtLevel", "PussyJuiceLevel", "BloodLevel" };
        private static readonly Type Controller = FindType("KK_SkinEffects.SkinEffectsController");
        private static readonly Type Provider = FindType("MainGameHScenePoseInjector.Plugin");
        private static Type FindType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(name, false)).FirstOrDefault(type => type != null);
        private static string Capture(ChaControl actor, string key)
        {
            if (key == "controls")
            {
                var method = Provider?.GetMethod("CaptureHotelCharacterEffects", BindingFlags.Public | BindingFlags.Static);
                if (method == null) throw new MissingMethodException("肌効果プラグインに保存APIがありません。");
                return (string)method.Invoke(null, new object[] { actor });
            }
            var controller = Controller == null ? null : actor.GetComponent(Controller);
            if (controller == null) throw new InvalidOperationException("肌効果MODのキャラコントローラーがありません。");
            var property = Controller.GetProperty(key);
            if (property?.CanRead != true || !property.CanWrite) throw new MissingMemberException("肌効果プロパティを読み書きできません：" + key);
            return Convert.ToString(property.GetValue(controller, null), CultureInfo.InvariantCulture);
        }
        private static void Apply(ChaControl actor, string key, string value)
        {
            if (key == "controls")
            {
                var method = Provider?.GetMethod("RestoreHotelCharacterEffects", BindingFlags.Public | BindingFlags.Static);
                if (method == null) throw new MissingMethodException("肌効果プラグインに復元APIがありません。");
                method.Invoke(null, new object[] { actor, value }); return;
            }
            var controller = Controller == null ? null : actor.GetComponent(Controller);
            if (controller == null) throw new InvalidOperationException("肌効果MODのキャラコントローラーがありません。");
            var property = Controller.GetProperty(key);
            if (property?.CanWrite != true) throw new MissingMemberException(key);
            property.SetValue(controller, Convert.ChangeType(value, property.PropertyType, CultureInfo.InvariantCulture), null);
        }
        internal static void Save(AiModeHSession session, HotelRoomStateStore store, HotelRoomSaveBatch batch, string source)
        {
            foreach (var actor in session.RoomActors())
            {
                bool female = actor.sex != default(byte);
                var keys = (Provider == null ? Array.Empty<string>() : new[] { "controls" })
                    .Concat(Controller != null && actor.GetComponent(Controller) != null ? Properties : Array.Empty<string>());
                foreach (string key in keys)
                    batch.Item((female ? "女性肌効果:" : "男性肌効果:") + key, Key(female, key),
                        () => new HotelRoomActorEffect { female = female, key = key, value = Capture(actor, key) },
                        value => store.SaveActorEffect(source, value), value => value.value);
            }
        }
        internal static void Restore(AiModeHSession session, HotelRoomAppearance appearance, HotelRoomRestoreBatch batch)
        {
            // Textures first, then manual ownership/desired values, which can include MOD channels.
            foreach (var saved in (appearance?.effects ?? Array.Empty<HotelRoomActorEffect>()).OrderBy(value => value?.key == "controls"))
            {
                if (saved == null) { batch.Failure("肌効果", "appearance.effects", new InvalidOperationException("肌効果の保存項目がnullです。")); continue; }
                ChaControl actor = null;
                batch.Item((saved.female ? "女性肌効果:" : "男性肌効果:") + saved.key, Key(saved.female, saved.key), "ChaControl/skin effect owner", () =>
                {
                    actor = session.RoomActors().FirstOrDefault(a => (a.sex != default(byte)) == saved.female);
                    if (actor == null) throw new InvalidOperationException("肌効果の復元対象キャラがありません。");
                    return saved.value;
                }, value => Apply(actor, saved.key, value), value => Capture(actor, saved.key) == value,
                value => value, () => Capture(actor, saved.key), () =>
                {
                    string before = Capture(actor, saved.key); return () => Apply(actor, saved.key, before);
                });
            }
        }
        private static string Key(bool female, string key) => "appearance.effects[female=" + female + ",key=" + key + "]";
    }
}
