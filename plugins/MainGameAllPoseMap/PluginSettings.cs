using System.Runtime.Serialization;

namespace MainGameAllPoseMap
{
    [DataContract]
    internal sealed class PluginSettings
    {
        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            if (AddedMapNo <= 0) AddedMapNo = 900;
            if (SourceMapNo < 0) SourceMapNo = 1;
            if (AddedThumbnailID <= 0) AddedThumbnailID = 9901;
            if (string.IsNullOrWhiteSpace(AddedMapName)) AddedMapName = "all_pose_map_900";
            if (string.IsNullOrWhiteSpace(AddedDisplayName)) AddedDisplayName = "All Pose Map";
            if (string.IsNullOrWhiteSpace(VirtualPointAnchorName)) VirtualPointAnchorName = "h_free";
            if (VirtualPointsPerRing <= 0) VirtualPointsPerRing = 12;
            if (VirtualPointRadius < 0.1f) VirtualPointRadius = 1.5f;
            if (VirtualPointRingStep < 0f) VirtualPointRingStep = 0f;
            if (string.IsNullOrWhiteSpace(CategoriesOverrideCsv)) CategoriesOverrideCsv = string.Empty;
        }

        [DataMember(Order = 0)]
        public bool Enabled = true;

        [DataMember(Order = 1)]
        public int AddedMapNo = 900;

        [DataMember(Order = 2)]
        public int SourceMapNo = 1;

        [DataMember(Order = 3)]
        public string AddedMapName = "all_pose_map_900";

        [DataMember(Order = 4)]
        public string AddedDisplayName = "All Pose Map";

        [DataMember(Order = 5)]
        public int AddedSort = 900;

        [DataMember(Order = 6)]
        public bool ForceIsGate = true;

        [DataMember(Order = 7)]
        public bool ForceIsFreeH = true;

        [DataMember(Order = 8)]
        public bool ForceIsH = false;

        [DataMember(Order = 9)]
        public int AddedThumbnailID = 9901;

        [DataMember(Order = 10)]
        public bool EnableAllPoseInFreeH = true;

        [DataMember(Order = 11)]
        public bool BypassFreeHProgressLocks = true;

        [DataMember(Order = 12)]
        public bool DisableSpecialMapJump = true;

        [DataMember(Order = 13)]
        public bool EnableVirtualPoints = true;

        [DataMember(Order = 14)]
        public bool KeepOriginalClosePoints = false;

        [DataMember(Order = 15)]
        public string VirtualPointAnchorName = "h_free";

        [DataMember(Order = 16)]
        public int VirtualPointsPerRing = 12;

        [DataMember(Order = 17)]
        public float VirtualPointRadius = 1.5f;

        [DataMember(Order = 18)]
        public float VirtualPointRingStep = 0.8f;

        [DataMember(Order = 19)]
        public float VirtualPointHeightOffset = 0f;

        [DataMember(Order = 20)]
        public float VirtualPointVerticalStep = 0.2f;

        [DataMember(Order = 21)]
        public string CategoriesOverrideCsv = "";

        [DataMember(Order = 22)]
        public bool VerboseLog = true;
    }
}
