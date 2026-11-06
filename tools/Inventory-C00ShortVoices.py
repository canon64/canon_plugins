"""Read existing c00 extraction metadata and verify the selected WAVs without launching Unity."""
import audioop
import csv
import hashlib
import html
import json
import math
import wave
from pathlib import Path

root = Path("F:/kks/voice_extract")
out = Path(__file__).resolve().parent.parent / "reports/c00-short-voices"
print("[c00] inventory started", flush=True)
out.mkdir(parents=True, exist_ok=True)
texts = {}
for line in (root / "voice_csv/c00.csv").read_text(encoding="utf-8-sig").splitlines():
    fields = line.split("|", 3)
    if len(fields) == 4:
        texts[Path(fields[0]).stem] = fields[3]
with (root / "c00_all_clips.tsv").open(encoding="utf-8-sig", newline="") as stream:
    inventory = list(csv.DictReader(stream, delimiter="\t"))
short = sorted((r for r in inventory if r["bundle"].startswith("h/") and float(r["duration_seconds"]) <= 4),
               key=lambda r: (float(r["duration_seconds"]), r["clip_name"]))
selected = []
with (root / "voice_wave/c00/single_moans_up_to_4s/一覧.csv").open(encoding="utf-8-sig", newline="") as stream:
    for item in csv.DictReader(stream):
        name = Path(item["ファイル名"]).stem
        source = next(r for r in inventory if r["clip_name"] == name)
        path = root / "voice_wave/c00/single_moans_up_to_4s" / item["ファイル名"]
        with wave.open(str(path), "rb") as wav:
            pcm = wav.readframes(wav.getnframes())
            duration = wav.getnframes() / wav.getframerate()
            rms = audioop.rms(pcm, wav.getsampwidth())
            maximum = audioop.max(pcm, wav.getsampwidth())
            selected.append(dict(name=name, seconds=round(duration, 3), text=texts.get(name, "字幕表に未掲載・要試聴"),
                                 bundle="sound/data/pcm/c00/" + source["bundle"],
                                 rate=wav.getframerate(), channels=wav.getnchannels(), peak=maximum,
                                 rms_dbfs=round(20 * math.log10(max(rms, 1) / (2 ** (wav.getsampwidth() * 8 - 1))), 2),
                                 wav=str(path)))
fields = ["name", "seconds", "text", "bundle", "rate", "channels", "peak", "rms_dbfs", "wav"]
with (out / "selected.csv").open("w", encoding="utf-8-sig", newline="") as stream:
    writer = csv.DictWriter(stream, fieldnames=fields); writer.writeheader(); writer.writerows(selected)
all_rows = [dict(name=r["clip_name"], seconds=r["duration_seconds"], text=texts.get(r["clip_name"], "字幕表に未掲載"),
                 bundle="sound/data/pcm/c00/" + r["bundle"], asset=r["asset_name"]) for r in short]
with (out / "all-h-under-4s.csv").open("w", encoding="utf-8-sig", newline="") as stream:
    writer = csv.DictWriter(stream, fieldnames=["name", "seconds", "text", "bundle", "asset"]); writer.writeheader(); writer.writerows(all_rows)
bundle = Path("F:/kks/abdata/sound/data/pcm/c00/h/01.unity3d")
report = dict(personality="セクシー c00", inventory_source=str(root / "c00_all_clips.tsv"),
              inventory_clips=len(inventory), h_short_count=len(short), selected=selected,
              source_bundle=str(bundle), bundle_sha256=hashlib.file_digest(bundle.open("rb"), "sha256").hexdigest()
              if hasattr(hashlib, "file_digest") else hashlib.sha256(bundle.read_bytes()).hexdigest(),
              limitations="既存抽出の分類を再利用。全短音声にキス・普通の短い台詞も含む。今回の聴感確認は未実施。")
(out / "inventory.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
rows = "".join("<tr>" + "".join("<td>" + html.escape(str(r[k])) + "</td>" for k in ["name", "seconds", "text", "bundle"]) + "</tr>" for r in selected)
(out / "index.html").write_text('<!doctype html><meta charset="utf-8"><title>c00 短い単発喘ぎ候補</title><style>body{font:16px sans-serif;margin:32px}td,th{border:1px solid #ccc;padding:8px}table{border-collapse:collapse}</style><h1>セクシー c00：短い単発喘ぎ候補</h1><p>4秒以下。音量の小ささは分類していません。未掲載音声は要試聴です。</p><table><tr><th>名前</th><th>秒</th><th>字幕表</th><th>収録先</th></tr>'+rows+"</table>", encoding="utf-8")
print(json.dumps(dict(h_short_count=len(short), selected=selected, report=str(out / "index.html")), ensure_ascii=False), flush=True)
