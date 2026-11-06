namespace MainGameDisplacementMap
{
    internal sealed class UvUpdateState
    {
        private object _map;
        private int _generation, _radialRevision;
        private float _strength, _scrollX, _scrollY;
        private bool _valid;

        // Every base UV is in [0,1]. Beyond either end the clamped coordinates
        // are identical for the whole grid, however far time advances.
        private static float EffectiveScroll(float value) => value >= 1f ? 1f : value <= -1f ? -1f : value;

        internal bool Matches(bool managed, object map, int generation, int radialRevision,
            float strength, float scrollX, float scrollY)
        {
            return managed && _valid && ReferenceEquals(_map, map) && _generation == generation &&
                _radialRevision == radialRevision && _strength == strength &&
                _scrollX == EffectiveScroll(scrollX) && _scrollY == EffectiveScroll(scrollY);
        }

        internal void Remember(object map, int generation, int radialRevision, float strength, float scrollX, float scrollY)
        {
            _map = map;
            _generation = generation;
            _radialRevision = radialRevision;
            _strength = strength;
            _scrollX = EffectiveScroll(scrollX);
            _scrollY = EffectiveScroll(scrollY);
            _valid = true;
        }

        internal void Reset() { _valid = false; _map = null; }
    }
}
