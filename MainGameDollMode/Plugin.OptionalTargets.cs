using SessionGate = CanonPlugins.Local.SessionGate;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using UnityEngine;

namespace MainGameDollMode
{
    public partial class Plugin
    {
        // AssemblyLoad may run off the Unity thread. Only increment a version there.
        private int _optionalAssemblyVersion;
        private int _resolvedOptionalAssemblyVersion = -1;
        private string _optionalKeywordFingerprint;
        private Type[] _optionalFixationalTypes = Type.EmptyTypes;
        private Type[] _optionalAhegaoTypes = Type.EmptyTypes;
        private readonly Dictionary<Assembly, Type[]> _optionalAssemblyTypes = new Dictionary<Assembly, Type[]>();

        private void OnOptionalAssemblyLoaded(object sender, AssemblyLoadEventArgs args)
            => Interlocked.Increment(ref _optionalAssemblyVersion);

        private void EnsureOptionalTargetTypes()
        {
if(SessionGate.Allowed && _resolvedOptionalAssemblyVersion>=0)return;

            List<string> keywords = GetAhegaoKeywordsCached();
            int version = Volatile.Read(ref _optionalAssemblyVersion);
            if (_resolvedOptionalAssemblyVersion == version &&
                _optionalKeywordFingerprint == _cachedAhegaoKeywordFingerprint) return;

            var fixational = new HashSet<Type>();
            var ahegao = new HashSet<Type>();
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!_optionalAssemblyTypes.TryGetValue(assembly, out Type[] types))
                {
                    // Retry a partially resolvable assembly after another assembly loads.
                    try { types = assembly.GetTypes(); _optionalAssemblyTypes[assembly] = types; }
                    catch (ReflectionTypeLoadException ex) { types = ex.Types; }
                    catch (NotSupportedException) { continue; }
                    catch (Exception ex) { LogWarn("[optional-types] " + assembly.FullName + ": " + ex.Message); continue; }
                }
                string assemblyName = assembly.GetName().Name ?? string.Empty;
                foreach (Type type in types)
                {
                    if (type == null || type.IsAbstract || type.ContainsGenericParameters ||
                        !typeof(MonoBehaviour).IsAssignableFrom(type)) continue;
                    string name = type.FullName ?? type.Name;
                    if (name.IndexOf("FixationalEyeMovement", StringComparison.OrdinalIgnoreCase) >= 0)
                        fixational.Add(type);
                    foreach (string keyword in keywords)
                    {
                        if (name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0 &&
                            assemblyName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        ahegao.Add(type);
                        break;
                    }
                }
            }
            _optionalFixationalTypes = new Type[fixational.Count];
            fixational.CopyTo(_optionalFixationalTypes);
            _optionalAhegaoTypes = new Type[ahegao.Count];
            ahegao.CopyTo(_optionalAhegaoTypes);
            _resolvedOptionalAssemblyVersion = version;
            _optionalKeywordFingerprint = _cachedAhegaoKeywordFingerprint;
            _nextFixationalRescanAt = _nextAhegaoRescanAt = 0f;
            if (_harmony != null)
            {
                DollModeGuardHooks.EnsureFixationalBlocks(_harmony, _optionalFixationalTypes);
                DollModeGuardHooks.EnsureMainGameAhegaoBlock(_harmony);
            }
        }
    }
}
