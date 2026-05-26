using UnityEngine;

namespace MainGameVoiceFaceEventBridge
{
    /// <summary>
    /// AudioSource と同じ GameObject に貼り付けて使う、PCMサンプル直接増幅フィルタ。
    /// Unity の AudioSource.volume は 0..1 で頭打ちになるため、それを超えた瞬間音量ブーストを
    /// 実現するために OnAudioFilterRead でサンプル値そのものを ×Gain する。
    /// クリッピングは [-1, 1] で頭打ち（過大時は歪むが詰みはしない）。
    /// </summary>
    internal sealed class VolumeBoostFilter : MonoBehaviour
    {
        /// <summary>増幅率。1.0 = 素通り。0 = 無音。上限 10（経験則の安全値）。</summary>
        public volatile float Gain = 1f;

        /// <summary>false にすると素通し（ゼロコスト）。</summary>
        public bool Enabled = true;

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (!Enabled || data == null || data.Length == 0) return;

            float g = Gain;
            if (g < 0f) g = 0f;
            else if (g > 10f) g = 10f;

            // 素通り判定（浮動小数の比較は緩めに）
            if (g >= 0.999f && g <= 1.001f) return;

            for (int i = 0; i < data.Length; i++)
            {
                float v = data[i] * g;
                if (v > 1f) v = 1f;
                else if (v < -1f) v = -1f;
                data[i] = v;
            }
        }
    }
}
