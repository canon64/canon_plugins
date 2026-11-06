using System;
using System.IO;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace MainGameAiShoujoH.Motion
{
    // Finalization owns this graph until its ADV continuation releases the session.
    // The actor root and camera remain under the ending host's existing ownership.
    internal sealed class EndingMotionPlayback : IDisposable
    {
        private readonly Animator _animator;
        private readonly Avatar _previous, _owned;
        private readonly bool _previousEvents;
        private PlayableGraph _graph;
        private bool _disposed;
        internal EndingMotionPlayback(Animator animator, AnimationClip clip, string[] paths)
        {
            _animator = animator ?? throw new ArgumentNullException(nameof(animator));
            _previous = animator.avatar; _previousEvents = animator.fireEvents;
            try
            {
                foreach (string path in paths)
                    if (!string.IsNullOrEmpty(path) && animator.transform.Find(path) == null) throw new InvalidDataException("Ending body binding missing: " + path);
                _owned = AvatarBuilder.BuildGenericAvatar(animator.gameObject, string.Empty);
                if (_owned == null || !_owned.isValid) throw new InvalidDataException("Ending body Generic Avatar invalid");
                _owned.name = "AI ending body hierarchy"; animator.avatar = _owned;
                // H cycle/SE receivers have ended; ADV owns its own command cues.
                animator.fireEvents = false;
                _graph = PlayableGraph.Create("AiShoujo ending ADV motion");
                _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
                var player = AnimationClipPlayable.Create(_graph, clip);
                player.SetApplyFootIK(false); player.SetApplyPlayableIK(false);
                var output = AnimationPlayableOutput.Create(_graph, "Ending female", animator);
                output.SetSourcePlayable(player); output.SetWeight(1);
                _graph.Play(); _graph.Evaluate(0);
            }
            catch { Dispose(); throw; }
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            try { if (_graph.IsValid()) _graph.Destroy(); }
            finally
            {
                if (_animator != null && _animator.avatar == _owned) { _animator.avatar = _previous; _animator.fireEvents = _previousEvents; }
                if (_owned != null) UnityEngine.Object.Destroy(_owned);
            }
        }
    }
}
