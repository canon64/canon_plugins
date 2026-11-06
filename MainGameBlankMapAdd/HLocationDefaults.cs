using System;
using System.Linq;

namespace MainGameBlankMapAdd
{
    internal static class HLocationMapIdentity
    {
        internal static string Canonical(string key)
        {
            if (key == "source:hotel-room-201") return "bundle:map/scene/canon_hotel_201.unity3d\nscene:CanonPinkRoom201Map";
            if (key == "source:hotel-room-reception-suite") return "bundle:map/scene/canon_reception_suite.unity3d\nscene:CanonReceptionSuiteMap";
            return key;
        }
        internal static bool Same(string a, string b) => string.Equals(Canonical(a), Canonical(b), StringComparison.Ordinal);
    }
    internal sealed class HLocationDefaultRequest
    {
        private string _key;
        private object _root;
        private long _ticket;
        internal long Begin(string key, object root)
        {
            if (_key == key && ReferenceEquals(_root, root)) return 0;
            _key = key; _root = root; return ++_ticket;
        }
        internal bool Current(long ticket, string key, object root) => ticket != 0 && ticket == _ticket && _key == key && ReferenceEquals(_root, root);
        internal void Cancel() { ++_ticket; }
        internal void Reset() { Cancel(); _key = null; _root = null; }
    }
}
