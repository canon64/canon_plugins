# WaveTone 2.74 BPM analysis core

`bpm_core.py` independently reproduces the automatic BPM scoring stage in
WaveTone 2.74's `awlib.dll`. The source was derived from local binary analysis;
no decompiled source is copied here.

The input is WaveTone's **feature frames**, not audio PCM. For each adjacent
pair of frames, the onset value is the sum of positive rises across unsigned
16-bit feature channels. A local peak in a quarter-second window is boosted
by 50%. The BPM stage evaluates 60 through 239 BPM, combining the fundamental
phasor magnitude with half the double-frequency magnitude, then divides all
scores by the maximum. The optional target mode scans 101 values from target
minus 0.50 to target plus 0.50 BPM in 0.01 BPM steps.

```
python bpm_core.py --features feature-frames.u16le --width 120 --rate 40
python bpm_core.py --onsets onsets.i32le --rate 40
python bpm_core.py --onsets onsets.i32le --rate 40 --target-bpm 120
```

`--rate` is feature frames per second; WaveTone's default is 40. The feature
width depends on WaveTone's analysis settings. Input files are raw little-endian
integer arrays, with no headers. The output is JSON with the winning BPM and
five strongest candidate scores.

## Verification and limits

The implementation was compared with the installed WaveTone 2.74
`awlib.dll` (`SHA-256 A40749620A0A31C75A3DF9AE5AFF0D51B3B9F2FE29739B1F3C1E5CE38014A8B2`).
Random feature-frame onset output matched exactly. Automatic BPM winners
matched for synthetic pulse and random onset inputs; the largest normalized
score difference in that comparison was below `5e-16`.

WaveTone's preceding audio-to-feature analysis, phase offset output, and
manual tap-tempo processing are not reproduced here. This script therefore
cannot directly analyze a WAV file or claim end-to-end parity with WaveTone.
