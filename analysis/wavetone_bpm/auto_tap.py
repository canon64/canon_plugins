"""Find audible beat positions and apply WaveTone's Ctrl-tap tempo rule."""

from __future__ import annotations

import math
from typing import Sequence

import numpy as np


def tap_tempo(tap_seconds: Sequence[float], auto_scores: Sequence[float]) -> int:
    """Apply the BPM selection in awlib's MeasKeyTempo to detected taps.

    The score is the tap phasor strength plus the automatic BPM evidence.
    """
    if len(tap_seconds) < 2:
        raise ValueError("at least two taps are required")
    times = np.rint(np.asarray(tap_seconds, dtype=np.float64) * 1000).astype(np.int64)
    average_ms = int((int(times[-1]) - int(times[0])) / (len(times) - 1))
    if average_ms < 1:
        raise ValueError("taps must be increasing")
    center = (average_ms // 2 + 60000) // average_ms
    lower = max(60, center - center // 16)
    upper = min(240, lower + center // 8)
    if upper <= lower:
        raise ValueError("tap tempo is outside WaveTone's range")
    strengths = np.zeros(180, dtype=np.float64)
    for bpm in range(lower, upper):
        angles = times * (math.tau * bpm / 60000.0)
        strengths[bpm - 60] = np.cos(angles).sum() ** 2 + np.sin(angles).sum() ** 2
    peak = strengths.max()
    if peak > 0:
        strengths /= peak
    combined = 2.0 * strengths + np.asarray(auto_scores)
    return int(np.argmax(combined)) + 60


def detect_taps(onsets: Sequence[int], rate: int, bpm: float) -> list[float]:
    """Pick local onset peaks near one regular beat grid, like automatic taps."""
    values = np.asarray(onsets, dtype=np.float64)
    if len(values) < 4 or not np.any(values > 0):
        return []
    period = 60.0 / bpm
    times = np.arange(len(values), dtype=np.float64) / rate
    local = np.r_[False, (values[1:-1] >= values[:-2]) &
                  (values[1:-1] > values[2:]), False]
    threshold = max(float(np.percentile(values, 70)), float(values.max()) * 0.08)
    indices = np.flatnonzero(local & (values >= threshold))
    if len(indices) == 0:
        return []
    peak_times = times[indices]
    weights = np.sqrt(values[indices])
    # A grid is evaluated every quarter of a feature frame. Only peaks within
    # about one tenth of a beat count as taps on that grid.
    tolerance = min(0.10 * period, 0.075)
    phases = np.arange(0, period, 1.0 / (rate * 4))
    best_phase = 0.0
    best_weight = -1.0
    for phase in phases:
        distance = np.abs((peak_times - phase + period / 2) % period - period / 2)
        weight = float(np.sum(weights * np.maximum(0, 1 - distance / tolerance)))
        if weight > best_weight:
            best_phase, best_weight = float(phase), weight
    grid = np.arange(best_phase, times[-1] + period / 2, period)
    taps = []
    for expected in grid:
        nearest = int(np.argmin(np.abs(peak_times - expected)))
        if abs(peak_times[nearest] - expected) <= tolerance:
            actual = float(peak_times[nearest])
            if not taps or actual - taps[-1] > period * 0.5:
                taps.append(actual)
    # A continuous sequence gives the tap calculation the intended interval.
    if len(taps) < 2:
        return []
    runs: list[list[float]] = []
    run = [taps[0]]
    for tap in taps[1:]:
        if tap - run[-1] > period * 1.5:
            runs.append(run)
            run = []
        run.append(tap)
    runs.append(run)
    return max(runs, key=len)
