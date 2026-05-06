using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Valve.VR;

namespace MainGirlHipHijack
{
    /// <summary>
    /// SteamVRデバイス姿勢取得の共通レイヤー。
    /// トラッカー追従・男性HMD追従から使う。
    /// </summary>
    public sealed partial class Plugin
    {
        private static readonly TrackedDevicePose_t[] _vrPoses =
            new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
        private static readonly TrackedDevicePose_t[] _vrGamePoses =
            new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];

        // デバイスシリアル → デバイスインデックスのキャッシュ
        private readonly Dictionary<string, uint> _vrSerialToIndex =
            new Dictionary<string, uint>();

        // デバイスインデックス → シリアルのキャッシュ
        private readonly Dictionary<uint, string> _vrIndexToSerial =
            new Dictionary<uint, string>();

        private float _vrDeviceScanNextTime;
        private const float VRDeviceScanInterval = 3f;

        // ── デバイス列挙 ─────────────────────────────────────────────────────

        /// <summary>
        /// 接続中のGenericTracker一覧を返す（serial, index）。
        /// </summary>
        internal List<(string serial, uint index)> GetConnectedTrackers()
        {
            var result = new List<(string, uint)>();
            var system = OpenVR.System;
            if (system == null) return result;

            for (uint i = 0; i < OpenVR.k_unMaxTrackedDeviceCount; i++)
            {
                if (system.GetTrackedDeviceClass(i) != ETrackedDeviceClass.GenericTracker)
                    continue;
                if (!system.IsTrackedDeviceConnected(i))
                    continue;
                string serial = GetDeviceSerial(system, i);
                result.Add((serial, i));
            }
            return result;
        }

        /// <summary>
        /// HMDのデバイスインデックスを返す。未接続なら uint.MaxValue。
        /// </summary>
        internal uint GetHMDDeviceIndex()
        {
            var system = OpenVR.System;
            if (system == null) return uint.MaxValue;

            for (uint i = 0; i < OpenVR.k_unMaxTrackedDeviceCount; i++)
            {
                if (system.GetTrackedDeviceClass(i) == ETrackedDeviceClass.HMD
                    && system.IsTrackedDeviceConnected(i))
                    return i;
            }
            return uint.MaxValue;
        }

        /// <summary>
        /// シリアルからデバイスインデックスを解決。未検出なら uint.MaxValue。
        /// </summary>
        internal uint ResolveDeviceIndex(string serial)
        {
            if (string.IsNullOrEmpty(serial)) return uint.MaxValue;
            if (_vrSerialToIndex.TryGetValue(serial, out uint idx)) return idx;
            return uint.MaxValue;
        }

        // ── キャッシュ更新（定期スキャン）────────────────────────────────────

        private void TickVRDeviceScan()
        {
            float now = Time.unscaledTime;
            if (now < _vrDeviceScanNextTime) return;
            _vrDeviceScanNextTime = now + VRDeviceScanInterval;

            var system = OpenVR.System;
            if (system == null) return;

            _vrSerialToIndex.Clear();
            _vrIndexToSerial.Clear();

            for (uint i = 0; i < OpenVR.k_unMaxTrackedDeviceCount; i++)
            {
                var cls = system.GetTrackedDeviceClass(i);
                if (cls == ETrackedDeviceClass.Invalid) continue;
                if (!system.IsTrackedDeviceConnected(i)) continue;

                string serial = GetDeviceSerial(system, i);
                if (string.IsNullOrEmpty(serial)) continue;

                _vrSerialToIndex[serial] = i;
                _vrIndexToSerial[i] = serial;
            }
        }

        // ── ポーズ取得 ──────────────────────────────────────────────────────

        /// <summary>
        /// 最新のトラッキングポーズをバッファに取得する。
        /// 毎フレーム1回呼ぶこと。
        /// </summary>
        private void FetchVRPoses()
        {
            var compositor = OpenVR.Compositor;
            if (compositor == null) return;
            compositor.GetLastPoses(_vrPoses, _vrGamePoses);
        }

        /// <summary>
        /// 指定デバイスのワールド座標とワールド回転を返す。
        /// トラッキングが有効でない場合はfalse。
        /// </summary>
        internal bool TryGetDevicePose(uint deviceIndex, out Vector3 worldPos, out Quaternion worldRot)
        {
            worldPos = Vector3.zero;
            worldRot = Quaternion.identity;

            if (deviceIndex >= OpenVR.k_unMaxTrackedDeviceCount)
                return false;

            ref TrackedDevicePose_t pose = ref _vrPoses[deviceIndex];
            if (!pose.bPoseIsValid || !pose.bDeviceIsConnected)
                return false;

            Matrix4x4 m = ConvertSteamVRMatrix(pose.mDeviceToAbsoluteTracking);
            worldPos = m.MultiplyPoint3x4(Vector3.zero);
            worldRot = m.rotation;
            return true;
        }

        // ── 内部ユーティリティ ────────────────────────────────────────────

        private static string GetDeviceSerial(CVRSystem system, uint index)
        {
            var sb = new StringBuilder(64);
            var error = ETrackedPropertyError.TrackedProp_Success;
            system.GetStringTrackedDeviceProperty(
                index,
                ETrackedDeviceProperty.Prop_SerialNumber_String,
                sb, 64, ref error);
            return error == ETrackedPropertyError.TrackedProp_Success ? sb.ToString() : string.Empty;
        }

        /// <summary>
        /// SteamVRのHmdMatrix34_t → Unity Matrix4x4 変換（右手→左手座標系）。
        /// </summary>
        private static Matrix4x4 ConvertSteamVRMatrix(HmdMatrix34_t m)
        {
            return new Matrix4x4
            {
                m00 =  m.m0,  m01 =  m.m1,  m02 = -m.m2,  m03 =  m.m3,
                m10 =  m.m4,  m11 =  m.m5,  m12 = -m.m6,  m13 =  m.m7,
                m20 = -m.m8,  m21 = -m.m9,  m22 =  m.m10, m23 = -m.m11,
                m30 =  0f,    m31 =  0f,    m32 =  0f,     m33 =  1f
            };
        }
    }
}
