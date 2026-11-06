"""Measure an audio file's BPM with the reproduced WaveTone BPM scoring core.

The audio-to-feature stage here is independent and approximate. It is not a
translation of WaveTone's AnalyzeWaveData2, so end-to-end results may differ.
Requires NumPy; ffmpeg is used to decode non-PCM WAV files and other formats.
"""

from __future__ import annotations

import argparse
import json
import shutil
import subprocess
import wave
from pathlib import Path

import numpy as np

from bpm_core import estimate_bpm, onset_from_feature_frames
from auto_tap import detect_taps, tap_tempo


RATE = 40
SAMPLE_RATE = 22050
WINDOW = 4096
BANDS = 72


def _decode_wave(path: Path, max_seconds: float) -> tuple[np.ndarray, int]:
    with wave.open(str(path), "rb") as source:
        channels = source.getnchannels()
        sample_rate = source.getframerate()
        width = source.getsampwidth()
        if source.getcomptype() != "NONE" or width not in (1, 2, 3, 4):
            raise ValueError("unsupported WAV encoding")
        data = source.readframes(min(source.getnframes(), int(max_seconds * sample_rate)))
    if width == 1:
        pcm = np.frombuffer(data, dtype=np.uint8).astype(np.float32)
        pcm = (pcm - 128.0) / 128.0
    elif width == 2:
        pcm = np.frombuffer(data, dtype="<i2").astype(np.float32) / 32768.0
    elif width == 4:
        pcm = np.frombuffer(data, dtype="<i4").astype(np.float32) / 2147483648.0
    else:
        raw = np.frombuffer(data, dtype=np.uint8).reshape(-1, 3).astype(np.int32)
        pcm = raw[:, 0] | (raw[:, 1] << 8) | (raw[:, 2] << 16)
        pcm = ((pcm ^ 0x800000) - 0x800000).astype(np.float32) / 8388608.0
    return pcm.reshape(-1, channels).mean(axis=1), sample_rate


def decode_audio(path: Path, max_seconds: float = 90.0) -> np.ndarray:
    if max_seconds <= 0:
        raise ValueError("max_seconds must be positive")
    if shutil.which("ffmpeg"):
        proc = subprocess.run(
            ["ffmpeg", "-nostdin", "-v", "error", "-i", str(path),
             "-t", str(max_seconds),
             "-f", "f32le", "-ac", "1", "-ar", str(SAMPLE_RATE), "-"],
            stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=False,
        )
        if proc.returncode != 0:
            raise ValueError(proc.stderr.decode(errors="replace").strip())
        return np.frombuffer(proc.stdout, dtype="<f4")
    mono, sample_rate = _decode_wave(path, max_seconds)
    if sample_rate == SAMPLE_RATE:
        return mono
    count = int(round(len(mono) * SAMPLE_RATE / sample_rate))
    old = np.arange(len(mono), dtype=np.float64) / sample_rate
    new = np.arange(count, dtype=np.float64) / SAMPLE_RATE
    return np.interp(new, old, mono).astype(np.float32)


def feature_frames(audio: np.ndarray) -> np.ndarray:
    if audio.ndim != 1 or len(audio) == 0:
        raise ValueError("audio is empty or not mono")
    duration = len(audio) / SAMPLE_RATE
    frame_count = max(1, int(duration * RATE))
    padded = np.pad(audio, (WINDOW // 2, WINDOW // 2))
    taper = np.hanning(WINDOW).astype(np.float32)
    frequencies = np.fft.rfftfreq(WINDOW, 1 / SAMPLE_RATE)
    edges = np.geomspace(45.0, min(9000.0, SAMPLE_RATE / 2), BANDS + 1)
    bins = np.searchsorted(edges, frequencies, side="right") - 1
    valid = (bins >= 0) & (bins < BANDS)
    groups = bins[valid]
    frames = np.zeros((frame_count, BANDS), dtype=np.float32)
    for index in range(frame_count):
        start = round(index * SAMPLE_RATE / RATE)
        spectrum = np.abs(np.fft.rfft(padded[start:start + WINDOW] * taper))
        frames[index] = np.bincount(groups, weights=spectrum[valid], minlength=BANDS)
    frames = np.log1p(frames)
    peak = float(np.max(frames))
    if peak > 0:
        frames *= 65535.0 / peak
    return np.rint(frames).clip(0, 65535).astype(np.uint16)


def measure(path: Path, max_seconds: float = 90.0) -> dict:
    samples = decode_audio(path, max_seconds)
    frames = feature_frames(samples)
    onset = onset_from_feature_frames(frames, RATE)
    bpm, scores = estimate_bpm(onset, RATE)
    taps = detect_taps(onset, RATE, bpm)
    tapped_bpm = tap_tempo(taps, scores) if len(taps) >= 2 else None
    best = sorted(range(len(scores)), key=scores.__getitem__, reverse=True)[:5]
    return {
        "file": str(path),
        "bpm": bpm,
        "auto_tap_bpm": tapped_bpm,
        "detected_tap_seconds": [round(time, 3) for time in taps],
        "duration_seconds": len(samples) / SAMPLE_RATE,
        "top_candidates": [
            {"bpm": index + 60, "strength": scores[index]} for index in best
        ],
        "feature_method": "log-frequency spectral rise (independent approximation)",
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("audio", type=Path, help="WAV or another format ffmpeg can decode")
    parser.add_argument("--seconds", type=float, default=90.0,
                        help="analyze this many seconds from the start (default: 90)")
    args = parser.parse_args()
    print(json.dumps(measure(args.audio, args.seconds), ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
