using System;
using System.IO;
using UnityEngine;

namespace MapEnvironmentRuntime
{
    internal sealed class SandboxTerrainShaderAsset : IDisposable
    {
        private AssetBundle _bundle;
        internal Shader Shader { get; private set; }
        internal SandboxTerrainShaderAsset()
        {
            try
            {
                using (var stream = typeof(SandboxTerrainShaderAsset).Assembly.GetManifestResourceStream("MapEnvironmentRuntime.NativeCtsTerrainShader"))
                using (var bytes = new MemoryStream())
                {
                    if (stream == null) throw new InvalidDataException("The native terrain mesh shader asset is missing from MapEnvironmentRuntime.");
                    stream.CopyTo(bytes);
                    _bundle = AssetBundle.LoadFromMemory(bytes.ToArray());
                }
                if (_bundle == null) throw new InvalidDataException("The terrain mesh shader bundle could not be loaded.");
                Shader = _bundle.LoadAsset<Shader>("assets/canon/native_walking_terrain.shader");
                if (Shader == null || !Shader.isSupported) throw new InvalidOperationException("The installed CTS terrain mesh shader is not supported by the current renderer.");
            }
            catch { Dispose(); throw; }
        }
        public void Dispose()
        {
            if (_bundle != null) _bundle.Unload(false);
            _bundle = null; Shader = null;
        }
    }
}
