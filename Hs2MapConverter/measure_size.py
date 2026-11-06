"""Measure source packs and completed outputs; avoid pretending total output is known."""
import json
import zipfile
from pathlib import Path

root=Path('J:/workspaces/hs2_kks_map_conversion')
data=json.loads((root/'inventory.json').read_text(encoding='utf-8'))
hs2=Path(data['root']);packs={r['archive'] for r in data['maps'] if 'archive' in r}
packbytes=sum((hs2/p).stat().st_size for p in packs)
base={r['bundle'] for r in data['maps'] if r['sourceType']=='base'}
basebytes=sum((hs2/'abdata'/p).stat().st_size for p in base)
bundlebytes=0;compressed=0
for p in packs:
    with zipfile.ZipFile(hs2/p) as z:
        for n in z.infolist():
            if n.filename.lower().endswith(('.unity3d','.resource','.ress')):
                bundlebytes+=n.file_size;compressed+=n.compress_size
outputs=list(Path('F:/kks/mods/Local HS2 Bulk Imports').glob('*.zipmod'))+list(Path('F:/kks/mods/Local HS2 Conversion Trials').glob('*.zipmod'))
sizes=[p.stat().st_size for p in outputs]
result={'sourceModPacks':len(packs),'sourceModArchivesGiB':round(packbytes/1024**3,2),'sourceBaseSceneBundlesGiB':round(basebytes/1024**3,2),
        'allAssetsInsideSourceModPacksGiB':round(bundlebytes/1024**3,2),'completedZipmods':len(outputs),'completedZipmodsGiB':round(sum(sizes)/1024**3,3),
        'completedSizesMiB':[round(n/1024**2,1) for n in sizes]}
print(json.dumps(result,ensure_ascii=False,indent=2))
(root/'size_measurement.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
