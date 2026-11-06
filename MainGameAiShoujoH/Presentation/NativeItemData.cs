using System;
using System.Runtime.Serialization;
namespace MainGameAiShoujoH.Presentation
{
#pragma warning disable CS0649
    [DataContract] internal sealed class ItemParent { [DataMember] public bool direct, scale; [DataMember] public int actor; [DataMember] public string bone, self; }
    [DataContract] internal sealed class ItemDefinition { [DataMember] public int mode, motionId, kind, itemId; [DataMember] public string bundle, asset, baseBundle, baseController, controllerBundle, controller; [DataMember] public ItemParent[] parents; }
    [DataContract] internal sealed class ItemFile { [DataMember] public string source, local, sha256; [DataMember] public long bytes; }
    [DataContract] internal sealed class ItemCatalog { [DataMember] public ItemDefinition[] items; [DataMember] public ItemFile[] files; [DataMember] public string[] actorRoles; [DataMember] public bool runtimeVerified; }
#pragma warning restore CS0649
}
