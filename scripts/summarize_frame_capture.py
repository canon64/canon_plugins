"""Read a finite timing capture. Does not control the game or infer unmeasured FPS gains."""
import argparse
import json
from pathlib import Path


def summarize(report):
    print(f"Capture: {report.get('Reason')}  {report.get('Utc')}")
    print(report.get('Context', ''))
    for title, key in [('Uninstrumented baseline', 'Baseline'), ('Instrumented window (hook overhead included)', 'Instrumented')]:
        if key == 'Baseline' and report.get('NativeBaseline') is not None:
            title = 'Baseline (no method hooks; native recorder overhead included)'
        window = report.get(key)
        if not window:
            print(f'{title}: unavailable')
            continue
        print(f"{title}: frames={window['Frames']}, mean={window['MeanFps']:.2f} FPS / {window['MeanFrameMs']:.3f} ms, "
              f"p95={window['P95FrameMs']:.3f} ms, p99={window['P99FrameMs']:.3f} ms, max={window['MaxFrameMs']:.3f} ms")
        print(f"  Frames over 16.67ms: {window['FramesOver60FpsBudget']}; dropped percentile samples: {window['DroppedFrameSamples']}")
        if 'LongestOverBudgetRun' in window:
            print(f"  Longest over-budget run: {window['LongestOverBudgetRun']} frames; over20/33/50ms: "
                  f"{window['FramesOver20Ms']}/{window['FramesOver33Ms']}/{window['FramesOver50Ms']}")
        for name in ['Cpu', 'Gpu']:
            value = window.get(f'Engine{name}MeanMs')
            print(f"  Engine {name}: " + ('unavailable' if value is None else f'{value:.3f} ms') + f" ({window.get(f'Engine{name}TimingSamples', 0)} samples)")
        for method in window.get('Methods', [])[:20]:
            print(f"  exclusive/frame={method['ExclusiveMsPerFrame']:.3f} ms, inclusive total={method['InclusiveMs']:.3f} ms, "
                  f"calls={method['Calls']}, max call={method['MaxCallMs']:.3f} ms: {method['Method']}")
        for marker in report.get('Native' + key) or []:
            value = marker.get('MeanMsPerFrame')
            timing = 'unavailable' if value is None else f'{value:.3f} ms/frame'
            print(f"  Native recorder {marker['Marker']}: {timing}; frames with samples={marker['FramesWithSamples']}; "
                  f"blocks={marker['Blocks']}; error={marker.get('Error') or 'none'}")
        if window.get('DepthOverflows') or window.get('UnbalancedCalls'):
            print('  WARNING: nesting limit or unbalanced samples; attribution is incomplete')
        for frame in window.get('SlowFrames', []):
            print(f"  Slow interval #{frame['FrameIndexInWindow']}: {frame['FrameMs']:.3f} ms")
            for method in frame.get('Methods', [])[:5]:
                print(f"    exclusive={method['ExclusiveMs']:.3f} ms, calls={method['Calls']}: {method['Method']}")
    for failure in report.get('PatchFailures', []):
        print('Hook coverage limitation:', failure)
    print(report.get('Notes', ''))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('capture', type=Path)
    args = parser.parse_args()
    summarize(json.loads(args.capture.read_text(encoding='utf-8-sig')))
