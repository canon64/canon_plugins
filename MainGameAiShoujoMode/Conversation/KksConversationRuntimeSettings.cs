using System;

namespace MainGameAiShoujoMode.Conversation
{
    [Serializable]
    internal sealed class KksConversationRuntimeSettings
    {
#pragma warning disable CS0649
        public string savePath;
        public string advDefaultControllerBundle, advDefaultControllerAsset, advInitialPose, advInitialExpression;
        public string advDefaultIkBundle, advDefaultIkAsset, advDefaultState;
        public string advCameraBundle, advCameraAsset;
        public string advEventCgActorRelativeMap;
        public string[] advEventCgActorRelativeAssets;
        public float advLookTargetRate, advLookTargetRotation, advLookTargetRange, advEyeTargetDistance, advNeckTargetDistance;
        public float advBgmMinimumFadeSeconds;
        public int advInitialNeckPattern;
        public int advPlayerNumber, advHeroineNumberOffset;
        public int koikatsuPointMax;
        public int crossFadeDepthBits;
        public int schema, zero, one, maxNormalStage, percentMax, preferenceDistanceMax, attributeFavorIndex;
        public int aggressivePreferenceAxis, diligencePreferenceAxis, kindnessPreferenceAxis;
        public int poseBundleIndex, poseControllerIndex, poseStateIndex, poseIkBundleIndex, poseIkAssetIndex;
        public int preferenceCorrectionIndex, favorCorrectionIndex, lewdnessCorrectionIndex;
        public float attributeFavorMultiplier, audioLeadSeconds, audioMinimumPitch;
#pragma warning restore CS0649
    }
}
