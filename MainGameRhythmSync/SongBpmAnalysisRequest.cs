using System;

namespace MainGameRhythmSync
{
    // Main-thread request identity. Saved values and manual corrections always win over old workers.
    internal sealed class SongBpmAnalysisRequest
    {
        private int _generation;
        internal string Path { get; private set; }
        internal bool Running { get; private set; }
        internal int Start(string path) { Path = path; Running = true; return ++_generation; }
        internal void Cancel() { ++_generation; Running = false; }
        internal bool Matches(int generation, string path) => Running && generation == _generation && string.Equals(Path, path, StringComparison.OrdinalIgnoreCase);
        internal bool CanApply(int generation, string path, string currentPath, bool active, bool explicitRequest, bool registered) =>
            active && Matches(generation, path) && string.Equals(path, currentPath, StringComparison.OrdinalIgnoreCase) && (explicitRequest || !registered);
    }
}
