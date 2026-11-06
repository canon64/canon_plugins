using System;
using UnityEngine;

namespace MainGameIPhoneCamera
{
    public sealed partial class Plugin
    {
        private AudioSource _captureSound;
        private AudioClip _shutterSound, _recordStartSound, _recordStopSound;

        private void CreatePhoneSounds()
        {
            _captureSound = _root.AddComponent<AudioSource>();
            _captureSound.playOnAwake = false;
            _captureSound.spatialBlend = 1;
            _captureSound.minDistance = .3f; _captureSound.maxDistance = 8;
            _captureSound.rolloffMode = AudioRolloffMode.Linear;
            _captureSound.dopplerLevel = 0;
            // Use the installed game's SystemSE.photo asset. Clone it so deleting
            // the phone never destroys the native clip used by other game UI.
            var nativeShutter = CommonLib.LoadAsset<AudioClip>("sound/data/systemse/00.unity3d", "se_ks_system_009", false, "");
            if (nativeShutter == null) throw new InvalidOperationException("ゲームの撮影用シャッター音が見つかりません");
            _shutterSound = Instantiate(nativeShutter);
            _recordStartSound = CaptureSound("IPhoneRecordStart", false);
            _recordStopSound = CaptureSound("IPhoneRecordStop", true);
        }

        private static AudioClip CaptureSound(string name, bool descending)
        {
            const int rate = 44100;
            var samples = new float[(int)(rate * .32)];
            for (int i = 0; i < samples.Length; i++)
            {
                double t = (double)i / rate;
                int note = t < .16 ? 0 : 1;
                double age = t - note * .16;
                double frequency = (note == 0 ^ descending) ? 880 : 1320;
                double envelope = Math.Min(1, age / .008) * Math.Max(0, 1 - age / .14);
                samples[i] = (float)(Math.Sin(age * 2 * Math.PI * frequency) * envelope * .45);
            }
            var clip = AudioClip.Create(name, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void PlayPhoneSound(AudioClip clip)
        {
            if (_captureSound != null && clip != null && _captureSoundVolume.Value > 0)
                _captureSound.PlayOneShot(clip, _captureSoundVolume.Value);
        }

        private void DestroyPhoneSounds()
        {
            if (_captureSound != null) _captureSound.Stop();
            _captureSound = null;
            if (_shutterSound != null) Destroy(_shutterSound);
            if (_recordStartSound != null) Destroy(_recordStartSound);
            if (_recordStopSound != null) Destroy(_recordStopSound);
            _shutterSound = _recordStartSound = _recordStopSound = null;
        }
    }
}
