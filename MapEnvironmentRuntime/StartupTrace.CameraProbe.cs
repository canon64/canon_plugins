using System;
using UnityEngine;
namespace MapEnvironmentRuntime
{
    public sealed partial class StartupTrace
    {
        private Action _cancelRenderProbe;
        // Each hook is removed at its first matching event, even if the next native render crashes.
        public void ArmNextRender(string stage, Camera target, GameObject root = null)
        {
            if (!Enabled || target == null) return;
            _cancelRenderProbe?.Invoke();
            Camera.CameraCallback beforeCull = null, beforeRender = null, afterRender = null;
            beforeCull = camera => { if (camera != target) return; Camera.onPreCull -= beforeCull; CameraCheckpoint(stage + ".preCull", camera); Snapshot(stage + ".preCull.state", root); };
            beforeRender = camera =>
            {
                if (camera != target) return;
                Camera.onPreRender -= beforeRender;
                CameraCheckpoint(stage + ".preRender.begin", camera);
                RenderInventory(stage + ".preRender.renderers", camera);
                // This is the final flushed checkpoint before returning control to Unity's native render queue.
                CameraCheckpoint(stage + ".preRender.return-to-unity", camera);
            };
            afterRender = camera => { if (camera != target) return; Camera.onPostRender -= afterRender; CameraCheckpoint(stage + ".postRender", camera); };
            Camera.onPreCull += beforeCull; Camera.onPreRender += beforeRender; Camera.onPostRender += afterRender;
            Action cancel = () => { Camera.onPreCull -= beforeCull; Camera.onPreRender -= beforeRender; Camera.onPostRender -= afterRender; };
            _cancelRenderProbe = cancel; Own(cancel);
            CameraCheckpoint(stage + ".armed", target);
        }
    }
}
