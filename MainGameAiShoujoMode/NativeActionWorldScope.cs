using System;
using System.Reflection;
using HarmonyLib;
using SaveData;

namespace MainGameAiShoujoMode
{
    // The native Action scene needs WorldData. Its temporary world must never become a game save.
    // The scope is acquired by the mode owner, before native Action initialization, and released on exit.
    internal sealed class NativeActionWorldScope : IDisposable
    {
        private static NativeActionWorldScope _active;
        private readonly WorldData _previous, _owned;
        private readonly bool _autoSave;
        private readonly Harmony _patches;
        private readonly MethodInfo _setWorld;
        private bool _disposed;
        internal static bool Owns(WorldData world) => _active != null && !_active._disposed && ReferenceEquals(_active._owned, world);

        internal NativeActionWorldScope(WorldData world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (_active != null) throw new InvalidOperationException("A native walking world is already owned.");
            _previous = Manager.Game.saveData;
            if (ReferenceEquals(_previous, world)) throw new InvalidOperationException("Native walking requires a separate temporary world.");
            _owned = world; _autoSave = Manager.Game.isAutoSavePossible;
            _setWorld = AccessTools.PropertySetter(typeof(Manager.Game), "saveData")
                ?? throw new MissingMethodException("Manager.Game.saveData setter");
            _patches = new Harmony("canon.MainGameAiShoujoMode.NativeActionWorld");
            _active = this;
            try
            {
                var prefix = new HarmonyMethod(typeof(NativeActionWorldScope), nameof(BeforePersist));
                Patch("Save", new[] { typeof(string) }, prefix);
                Patch("Save", new[] { typeof(string), typeof(string) }, prefix);
                Patch("AutoSave", new[] { typeof(int), typeof(int) }, prefix);
                Patch("Delete", new[] { typeof(string) }, prefix);
                _setWorld.Invoke(null, new object[] { world });
                Manager.Game.isAutoSavePossible = false;
            }
            catch
            {
                if (ReferenceEquals(Manager.Game.saveData, _owned))
                {
                    _setWorld.Invoke(null, new object[] { _previous });
                    Manager.Game.isAutoSavePossible = _autoSave;
                }
                _active = null;
                _patches.UnpatchSelf();
                throw;
            }
        }
        private void Patch(string name, Type[] parameters, HarmonyMethod prefix)
        {
            var method = AccessTools.Method(typeof(WorldData), name, parameters)
                ?? throw new MissingMethodException(typeof(WorldData).FullName, name);
            _patches.Patch(method, prefix: prefix);
        }
        private static bool BeforePersist(WorldData __instance) => !Owns(__instance);
        public void Dispose()
        {
            if (_disposed) return;
            if (ReferenceEquals(Manager.Game.saveData, _owned))
            {
                _setWorld.Invoke(null, new object[] { _previous });
                Manager.Game.isAutoSavePossible = _autoSave;
            }
            _disposed = true;
            if (ReferenceEquals(_active, this)) _active = null;
            _patches.UnpatchSelf();
        }
    }
}
