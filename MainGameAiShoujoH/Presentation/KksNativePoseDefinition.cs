using System;
using System.IO;
using System.Linq;
using MainGameAiShoujoH.Motion;
using MainGameAiShoujoH.Contracts;

namespace MainGameAiShoujoH.Presentation
{
    // KKS AnimationInfo_01 id0. These are native base-layer states from both
    // khh_f_base and khh_m_base, not an AI converted-controller catalog.
    internal static class KksNativePoseDefinition
    {
        internal static KksPoseIdentity DefaultIdentity() => new KksPoseIdentity { TableBundle = "h/list/01.unity3d", TableAsset = "AnimationInfo_01", Mode = (int)HFlag.EMode.houshi, Id = default(int) };
        internal static void ValidateIdentity(KksPoseIdentity identity)
        {
            var expected = DefaultIdentity();
            if (identity == null || identity.TableBundle != expected.TableBundle || identity.TableAsset != expected.TableAsset || identity.Mode != expected.Mode || identity.Id != expected.Id)
                throw new InvalidDataException("このKKS体位の正規identityには対応していません。");
        }
        internal static string ProgressionId(KksPoseIdentity identity) { ValidateIdentity(identity); return KksOnePosePlayback.MotionId; }
        internal static readonly string[] NativeStates = {
            "Idle", "WLoop", "SLoop", "OLoop", "Stop_Idle", "M_OUT_Start", "M_OUT_Loop", "OUT_A",
            "IN_Start", "IN_Loop", "Oral_Idle_IN", "Oral_Idle", "Drink_IN", "Drink", "Drink_A", "Vomit_IN", "Vomit", "Vomit_A"
        };
        internal static string NativeState(string upperState)
        {
            // Upper AI action phases -> KKS HHoushi's actual action states.
            // Saved HHoushi.cs235/280 and317-388; no rig/clip equivalence claimed.
            if (upperState == "Orgasm_OUT") return "M_OUT_Start";
            if (upperState == "Orgasm_OUT_A") return "OUT_A";
            if (upperState == "Orgasm_IN") return "IN_Start";
            if (NativeStates.Contains(upperState)) return upperState;
            throw new InvalidDataException("KKS純正体位に対応する状態がありません: " + upperState);
        }
        internal static MotionDefinition Create()
        {
            return new MotionDefinition {
                id = KksOnePosePlayback.MotionId, name = "KKS 亀頭いじり", category = "houshi", initialState = "Idle",
                states = NativeStates.Concat(new[] { "Orgasm_OUT", "Orgasm_OUT_A", "Orgasm_IN" }).Select(state => new MotionState { name = state }).ToArray()
            };
        }
        internal static bool SupportsEnding(int posture, int pose)
        {
            // Exact upper ADV keys preserved in the existing 91-script catalog.
            // These select the documented KKS Idle compatibility policy only.
            switch (posture.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + pose.ToString(System.Globalization.CultureInfo.InvariantCulture))
            {
                case "99:72": case "99:73": case "99:74": case "99:75": case "99:76": return true;
                default: return false;
            }
        }
    }
}
