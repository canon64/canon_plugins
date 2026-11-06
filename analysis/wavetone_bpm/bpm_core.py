"""Independent reproduction of WaveTone 2.74's BPM scoring core.

Input is a sequence of unsigned 16-bit feature frames (or the resulting
signed 32-bit onset sequence). Audio decoding and WaveTone's preceding pitch
spectrum analysis are deliberately outside this module.
"""

from __future__ import annotations

import argparse
import json
import math
import struct
from pathlib import Path
from typing import Sequence


TAU = 6.28318530717958
TABLE_SIZE = 4096
_SINE = tuple(math.sin(i * TAU / TABLE_SIZE) for i in range(TABLE_SIZE))
_COSINE = tuple(math.cos(i * TAU / TABLE_SIZE) for i in range(TABLE_SIZE))


def onset_from_feature_frames(frames: Sequence[Sequence[int]], rate: int) -> list[int]:
    """Match awlib's positive spectral rise and local-maximum emphasis.

    Each frame contains the same number of unsigned 16-bit feature values.
    ``rate`` is feature frames per second (WaveTone default: 40).
    """
    if rate < 4:
        raise ValueError("rate must be at least 4 frames per second")
    if len(frames) == 0:
        return []
    width = len(frames[0])
    if width == 0 or any(len(frame) != width for frame in frames):
        raise ValueError("feature frames must have a common nonzero width")

    raw = [0] * len(frames)
    for i in range(len(frames) - 1):
        earlier, later = frames[i], frames[i + 1]
        if any(not 0 <= value <= 65535 for value in earlier) or any(
            not 0 <= value <= 65535 for value in later
        ):
            raise ValueError("feature values must be unsigned 16-bit integers")
        raw[i] = sum(max(int(b) - int(a), 0) for a, b in zip(earlier, later))

    window = rate // 4
    onset = raw.copy()
    for i in range(len(raw)):
        start = max(0, i - window // 2)
        stop = min(len(raw), i - window // 2 + window)
        if stop <= start:
            continue
        peak_index = max(range(start, stop), key=raw.__getitem__)
        if peak_index == i:
            onset[i] += onset[i] // 2
    return onset


def bpm_scores(onset: Sequence[int], rate: int, target_bpm: int = 0) -> list[float]:
    """Return WaveTone's normalized candidate strengths.

    With ``target_bpm=0``, indices 0..179 represent 60..239 BPM. Otherwise
    indices 0..100 represent target-0.50..target+0.50 in 0.01 BPM steps.
    """
    if rate <= 0:
        raise ValueError("rate must be positive")
    if target_bpm < 0:
        raise ValueError("target_bpm cannot be negative")
    if target_bpm and target_bpm < 1:
        raise ValueError("target_bpm must be positive")

    scores: list[float] = []
    count = 180 if target_bpm == 0 else 101
    for candidate in range(count):
        bpm = (candidate + 60.0) if target_bpm == 0 else (
            target_bpm + (candidate - 50) * 0.01
        )
        phase_step = TABLE_SIZE / ((60.0 / (bpm + bpm)) * rate)
        phase = 0.0
        fundamental_cos = fundamental_sin = 0.0
        double_cos = double_sin = 0.0
        for value in onset:
            phase_index = int(phase)
            fundamental_index = (phase_index >> 1) & (TABLE_SIZE - 1)
            double_index = phase_index & (TABLE_SIZE - 1)
            fundamental_cos += value * _COSINE[fundamental_index]
            fundamental_sin += value * _SINE[fundamental_index]
            double_cos += value * _COSINE[double_index]
            double_sin += value * _SINE[double_index]
            phase += phase_step
        scores.append(
            math.hypot(fundamental_cos, fundamental_sin)
            + 0.5 * math.hypot(double_cos, double_sin)
        )

    maximum = max(scores, default=0.0)
    return [score / maximum for score in scores] if maximum else [0.0] * count


def estimate_bpm(onset: Sequence[int], rate: int, target_bpm: int = 0) -> tuple[float, list[float]]:
    scores = bpm_scores(onset, rate, target_bpm)
    if not any(scores):
        return (120.0 if target_bpm == 0 else float(target_bpm)), scores
    winner = max(range(len(scores)), key=scores.__getitem__)
    bpm = (winner + 60.0) if target_bpm == 0 else (
        target_bpm + (winner - 50) * 0.01
    )
    return bpm, scores


def _read_onsets(path: Path) -> list[int]:
    data = path.read_bytes()
    if len(data) % 4:
        raise ValueError("onset file size must be divisible by four")
    return list(struct.unpack(f"<{len(data) // 4}i", data))


def _read_frames(path: Path, width: int) -> list[tuple[int, ...]]:
    if width <= 0:
        raise ValueError("width must be positive")
    data = path.read_bytes()
    frame_bytes = width * 2
    if len(data) % frame_bytes:
        raise ValueError("feature file is not a whole number of frames")
    return [
        struct.unpack_from(f"<{width}H", data, offset)
        for offset in range(0, len(data), frame_bytes)
    ]


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    source = parser.add_mutually_exclusive_group(required=True)
    source.add_argument("--onsets", type=Path, help="little-endian signed int32 onset file")
    source.add_argument("--features", type=Path, help="little-endian uint16 feature matrix")
    parser.add_argument("--width", type=int, help="values per feature frame")
    parser.add_argument("--rate", type=int, default=40, help="feature frames per second")
    parser.add_argument("--target-bpm", type=int, default=0, help="optional 0.01 BPM refinement center")
    args = parser.parse_args()
    if args.features:
        if args.width is None:
            parser.error("--width is required with --features")
        onset = onset_from_feature_frames(_read_frames(args.features, args.width), args.rate)
    else:
        onset = _read_onsets(args.onsets)
    bpm, scores = estimate_bpm(onset, args.rate, args.target_bpm)
    top = sorted(range(len(scores)), key=scores.__getitem__, reverse=True)[:5]
    output = {
        "bpm": bpm,
        "rate": args.rate,
        "onset_count": len(onset),
        "top_candidates": [
            {
                "bpm": (i + 60.0) if args.target_bpm == 0 else args.target_bpm + (i - 50) * 0.01,
                "strength": scores[i],
            }
            for i in top
        ],
    }
    print(json.dumps(output, indent=2))


if __name__ == "__main__":
    main()
