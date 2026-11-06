using System;
using System.IO;
using System.Runtime.Serialization;

namespace MainGameAiShoujoMode.Authoring
{
    [DataContract] internal sealed class AuthoringDefaults
    {
#pragma warning disable CS0649 // Assigned by the bundled JSON contract.
        [DataMember] public int one, two, three, schemaVersion, maxImmediateSteps, guiDepth, maxExpression, maxPersonality;
        [DataMember] public float defaultFieldOfView, maxFieldOfView, maxWaitSeconds, maxCameraSeconds, minPitch, maxPitch;
#pragma warning restore CS0649
        internal static readonly AuthoringDefaults Current = Load();
        private static AuthoringDefaults Load()
        {
            using (var stream = typeof(AuthoringDefaults).Assembly.GetManifestResourceStream("MainGameAiShoujoMode.Authoring.Data.defaults.json"))
            { if (stream == null) throw new FileNotFoundException("イベント既定値がありません。"); using (var reader = new StreamReader(stream)) return AuthoringJson.Read<AuthoringDefaults>(reader.ReadToEnd()); }
        }
    }
}
