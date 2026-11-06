using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace MainGameAiShoujoMode.Life
{
    [DataContract] internal sealed class AiLifeRuntimeSettings
    {
        [DataMember] public string savePath { get; set; }
        [DataMember] public string worldKey { get; set; }
        [DataMember] public string sourceProfileVersion { get; set; }
        [DataMember] public AiLifeRules rules { get; set; }
        [DataMember] public string itemCatalogFile { get; set; }
        [DataMember] public int schema { get; set; }
        [DataMember] public int itemSlotMax { get; set; }
        [DataMember] public int playerInitialCapacity { get; set; }
        [DataMember] public int? agentInventoryCapacity { get; set; }
        [DataMember] public int storageCapacity { get; set; }
        [DataMember] public int pantryCapacity { get; set; }
        [DataContract] private sealed class FileSettings
        { [DataMember] public AiLifeRuntimeSettings aiLifeRuntime { get; set; } }
        // SandboxHost and SandboxSession serialize Parameters back to parameters.json.
        // Their schema owns map settings only; life configuration must survive outside that file.
        internal static AiLifeRuntimeSettings LoadContent(string contentDirectory)
            => Load(Path.Combine(contentDirectory, "ai-life-runtime.json"));
        internal static AiLifeRuntimeSettings Load(string path)
        {
            using (var stream = File.OpenRead(path))
            {
                var document = (FileSettings)new DataContractJsonSerializer(typeof(FileSettings)).ReadObject(stream);
                var settings = document?.aiLifeRuntime ?? throw new InvalidDataException("AI life runtime settings are missing.");
                settings.Validate(); return settings;
            }
        }
        internal void Validate()
        {
            if (string.IsNullOrWhiteSpace(savePath) || !Path.IsPathRooted(savePath) || string.IsNullOrWhiteSpace(worldKey)
                || string.IsNullOrWhiteSpace(sourceProfileVersion) || schema <= default(int) || itemSlotMax <= default(int)
                || playerInitialCapacity <= default(int) || agentInventoryCapacity < default(int)
                || storageCapacity <= default(int) || pantryCapacity <= default(int)) throw new InvalidDataException("Invalid AI life runtime settings.");
        }
    }
}
