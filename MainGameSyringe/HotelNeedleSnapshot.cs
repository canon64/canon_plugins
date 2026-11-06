using System;
using CanonPlugins.Shared;
using UnityEngine;

namespace MainGameSyringe
{
    [Serializable] internal sealed class HotelNeedleSnapshot
    {
        public bool spent;
        public int actor = -1;
        public string bone;
        public Vector3 surface;
        public GadgetRoomPose localPose;
        public string localPoseRecord;

        internal string Encode()
        {
            // Unity can omit nested plugin-defined classes; a JSON string survives that boundary.
            if (localPose != null) { localPoseRecord = JsonUtility.ToJson(localPose); localPose = null; }
            return JsonUtility.ToJson(this);
        }
        internal static HotelNeedleSnapshot Decode(string json)
        {
            var state = JsonUtility.FromJson<HotelNeedleSnapshot>(json);
            if (state == null) throw new ArgumentException("注射器の刺入状態が空です。");
            if (!string.IsNullOrEmpty(state.localPoseRecord))
                state.localPose = JsonUtility.FromJson<GadgetRoomPose>(state.localPoseRecord)
                    ?? throw new ArgumentException("注射器の刺入先に対する配置が不正です。");
            state.localPose?.Validate();
            return state;
        }
    }
}
