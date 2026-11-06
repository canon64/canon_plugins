using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace MainGameBlankMapAdd
{
    // Explicit JSON names keep the disk format independent of Unity serialization.
    [DataContract]
    internal sealed class AiIslandFile
    {
        [DataMember(Name = "path")] public string path { get; set; }
        [DataMember(Name = "bytes")] public long bytes { get; set; }
        [DataMember(Name = "sha256")] public string sha256 { get; set; }
    }

    [DataContract]
    internal sealed class AiIslandManifest
    {
        [DataMember(Name = "version")] public int version { get; set; }
        [DataMember(Name = "scene")] public string scene { get; set; }
        [DataMember(Name = "root")] public string root { get; set; }
        [DataMember(Name = "anchor")] public string anchor { get; set; }
        [DataMember(Name = "files")] public AiIslandFile[] files { get; set; }

        internal static AiIslandManifest Read(string json)
        {
            AiIslandManifest manifest;
            try
            {
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                    manifest = (AiIslandManifest)new DataContractJsonSerializer(typeof(AiIslandManifest)).ReadObject(stream);
            }
            catch (Exception ex) when (ex is SerializationException || ex is System.Xml.XmlException || ex is ArgumentException)
            { throw new InvalidDataException("本編島のmanifestをJSONとして読めません：" + ex.Message, ex); }
            if (manifest == null) Fail("ルート", "null");
            if (manifest.version != 1) Fail("version", manifest.version);
            if (manifest.scene != "map_00_data") Fail("scene", manifest.scene);
            if (manifest.root != "map00_Beach") Fail("root", manifest.root);
            if (manifest.anchor != "__AI_KKS_Start") Fail("anchor", manifest.anchor);
            if (manifest.files == null || manifest.files.Length != 7) Fail("files件数", manifest.files == null ? "null" : manifest.files.Length.ToString());
            for (int i = 0; i < manifest.files.Length; i++)
            {
                var file = manifest.files[i];
                if (file == null || string.IsNullOrWhiteSpace(file.path)) Fail("files[" + i + "].path", file?.path);
                if (file.bytes <= 0) Fail(file.path + " bytes", file.bytes);
                if (file.sha256 == null || file.sha256.Length != 64 || !file.sha256.All(Uri.IsHexDigit)) Fail(file.path + " sha256", file.sha256);
            }
            if (manifest.files.Select(f => f.path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != manifest.files.Length)
                Fail("files", "パス重複");
            if (manifest.files.Count(f => f.path == "map_00_data.unity3d") != 1 || manifest.files.Last().path != "map_00_data.unity3d")
                Fail("files末尾", manifest.files.Last().path);
            return manifest;
        }

        private static void Fail(string field, object actual)
        { throw new InvalidDataException("本編島のmanifest不一致：" + field + "=" + (actual ?? "null")); }
    }
}
