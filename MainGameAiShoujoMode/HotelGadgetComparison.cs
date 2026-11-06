using System;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace MainGameAiShoujoMode
{
    internal static class HotelGadgetComparison
    {
        internal static bool Equivalent(string expected, string actual, Func<double, double, bool> close, bool legacyNeedlePose = false, bool alignedNeedle = false, bool stateVerified = false)
        {
            if (expected == actual) return true;
            try
            {
                var left = Parse(expected); var right = Parse(actual);
                bool snapshot = left.Element("schema") != null && right.Element("schema") != null;
                bool hidden = snapshot && (string)left.Element("visible") == "false" && (string)right.Element("visible") == "false";
                bool attached = legacyNeedlePose && HasAttachedNeedlePose(left, !alignedNeedle) && HasAttachedNeedlePose(right, !alignedNeedle);
                if (attached || (snapshot && (alignedNeedle || hidden)))
                {
                    // An attached needle follows its bone, so room-relative coordinates change with
                    // animation. Verify the persisted bone-local pose and attachment fields instead.
                    left.Elements("poses").Remove(); right.Elements("poses").Remove();
                    left.Elements("poseRecords").Remove(); right.Elements("poseRecords").Remove();
                    left.Elements("points").Remove(); right.Elements("points").Remove();
                    left.Elements("tipPosition").Remove(); right.Elements("tipPosition").Remove();
                }
                if (snapshot && (stateVerified || hidden)) { left.Elements("runtime").Remove(); right.Elements("runtime").Remove(); }
                if (hidden)
                    foreach (string name in new[] { "collarAttached", "rootAttached", "tipPinned" })
                    { left.Elements(name).Remove(); right.Elements(name).Remove(); }
                return Compare(left, right, close, legacyNeedlePose, attached && alignedNeedle);
            }
            catch { return false; }
        }
        private static bool HasAttachedNeedlePose(XElement snapshot, bool requireLocalPose)
        {
            if ((string)snapshot.Element("visible") != "true") return false;
            string runtime = (string)snapshot.Element("runtime");
            if (string.IsNullOrEmpty(runtime)) return false;
            var state = Parse(runtime);
            return int.TryParse((string)state.Element("actor"), out int actor) && actor >= default(int)
                && !string.IsNullOrEmpty((string)state.Element("bone"))
                && (!requireLocalPose || !string.IsNullOrEmpty((string)state.Element("localPoseRecord")));
        }
        private static XElement Parse(string json)
        {
            using (var reader = JsonReaderWriterFactory.CreateJsonReader(Encoding.UTF8.GetBytes(json), XmlDictionaryReaderQuotas.Max))
                return XElement.Load(reader);
        }
        private static bool Compare(XElement left, XElement right, Func<double, double, bool> close, bool legacyNeedlePose, bool alignedNeedle)
        {
            string kind = (string)left.Attribute("type");
            if (kind != (string)right.Attribute("type")) return false;
            if (kind == "object" || kind == "array")
            {
                if (kind == "object" && left.Name == "rotation" && new[] { "x", "y", "z", "w" }.All(name => left.Element(name) != null && right.Element(name) != null))
                {
                    var aRotation = new[] { "x", "y", "z", "w" }.Select(name => double.Parse(left.Element(name).Value, CultureInfo.InvariantCulture)).ToArray();
                    var bRotation = new[] { "x", "y", "z", "w" }.Select(name => double.Parse(right.Element(name).Value, CultureInfo.InvariantCulture)).ToArray();
                    return aRotation.Zip(bRotation, close).All(same => same) || aRotation.Zip(bRotation, (a,b) => close(a,-b)).All(same => same);
                }
                var a = kind == "object" ? left.Elements().OrderBy(e => e.Name.ToString()).ToArray() : left.Elements().ToArray();
                var b = kind == "object" ? right.Elements().OrderBy(e => e.Name.ToString()).ToArray() : right.Elements().ToArray();
                // Placement verification measured the needle tip at the saved surface + depth after
                // one-shot alignment. Preserve checks for angle, scale, attachment, liquid and settings.
                if (alignedNeedle && kind == "object" && new[] { "position", "rotation", "scale", "worldScale" }.All(name => left.Element(name) != null && right.Element(name) != null))
                { a = a.Where(e => e.Name != "position").ToArray(); b = b.Where(e => e.Name != "position").ToArray(); }
                // Legacy syringe records omitted the bone-local pose. The room pose and all original
                // attachment fields must still match; only the newly captured local pose is additional.
                if (legacyNeedlePose && kind == "object" && new[] { "actor", "bone", "surface", "spent" }.All(name => left.Element(name) != null)
                    && left.Element("localPoseRecord") == null && left.Element("localPose") == null)
                    b = b.Where(e => e.Name != "localPoseRecord" && e.Name != "localPose").ToArray();
                return a.Length == b.Length && a.Zip(b, (x, y) => x.Name == y.Name && Compare(x, y, close, legacyNeedlePose, alignedNeedle)).All(value => value);
            }
            if (kind == "number") return close(double.Parse(left.Value, CultureInfo.InvariantCulture), double.Parse(right.Value, CultureInfo.InvariantCulture));
            if (left.Value == right.Value) return true;
            if (kind == "string" && ((left.Value.StartsWith("{") && right.Value.StartsWith("{")) || (left.Value.StartsWith("[") && right.Value.StartsWith("["))))
                return Compare(Parse(left.Value), Parse(right.Value), close, legacyNeedlePose, alignedNeedle);
            return false;
        }
    }
}
