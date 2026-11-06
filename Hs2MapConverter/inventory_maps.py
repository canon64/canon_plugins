"""Inventory every actual HS2 map registration without relying on archive names."""
import argparse
import csv
import io
import json
import sys
import time
import zipfile
import xml.etree.ElementTree as ET
from pathlib import Path

sys.path.insert(0,'F:/kks/work/clothing_setup/pydeps')
import UnityPy


def read_params(content):
    env=UnityPy.load(content)
    return [p for o in env.objects if o.type.name=='MonoBehaviour' for p in o.read_typetree().get('param',[])]


def inventory(hs2,out):
    print('INVENTORY_START',hs2,flush=True)
    out.mkdir(parents=True,exist_ok=True)
    records=[];errors=[];archives=0;seen={}
    def add(record):
        k=(record.get('archive','base'),record['bundle'].lower(),record['asset'].lower())
        if k in seen:
            seen[k].setdefault('aliases',[]).append(record)
        else:
            seen[k]=record;records.append(record)
    for p in (hs2/'abdata/map/list/mapinfo').rglob('*.unity3d'):
        try:
            for r in read_params(p.read_bytes()):
                add({'sourceType':'base','registration':p.relative_to(hs2).as_posix(),'sourceMapId':r['No'],
                     'label':r.get('MapNames',[''])[0], 'bundle':r['AssetBundleName'],'asset':r['AssetName'],'data':r})
        except Exception as e:errors.append({'file':str(p),'error':repr(e)})
    paths=[p for p in (hs2/'mods').rglob('*') if p.is_file() and p.suffix.lower() in ('.zipmod','.zip')]
    for p in paths:
        archives+=1
        if archives%250==0:print('SCANNED',archives,'/',len(paths),'maps',len(records),flush=True)
        try:
            with zipfile.ZipFile(p) as z:
                names=z.namelist();mapping={n.lower():n for n in names}
                info=[n for n in names if n.lower().startswith('abdata/map/list/mapinfo/') and n.lower().endswith('.unity3d')]
                lists=[n for n in names if n.lower().startswith('abdata/studio/info/') and Path(n).name.lower().startswith('map') and n.lower().endswith('.csv')]
                if not info and not lists:continue
                manifest=ET.fromstring(z.read('manifest.xml')) if 'manifest.xml' in names else None
                meta={'archive':p.relative_to(hs2).as_posix(),'guid':manifest.findtext('guid') if manifest is not None else '',
                      'version':manifest.findtext('version') if manifest is not None else '', 'archiveBytes':p.stat().st_size}
                for n in info:
                    for r in read_params(z.read(n)):
                        add(dict(meta,sourceType='zipmod',registration=n,sourceMapId=r['No'],label=r.get('MapNames',[''])[0],bundle=r['AssetBundleName'],asset=r['AssetName'],data=r))
                for n in lists:
                    raw=z.read(n)
                    try:txt=raw.decode('utf-8-sig')
                    except UnicodeDecodeError:txt=raw.decode('cp932')
                    for row in csv.reader(io.StringIO(txt)):
                        if len(row)<4 or not row[0].isdigit():continue
                        # HS2's native Studio map CSV: ID, name, bundle, scene, manifest, vanish bundle, vanish asset.
                        bundle=row[2];asset=row[3]
                        if not bundle or not asset:continue
                        add(dict(meta,sourceType='studio',registration=n,sourceMapId=int(row[0]),label=row[1],bundle=bundle,asset=asset,
                                 sourceBundleExists=('abdata/'+bundle.lower()) in mapping,data={'row':row},
                                 imageCandidates=[v for v in names if v.lower().endswith(('.png','.jpg','.jpeg')) and any(s in v.lower() for s in ('thumb','icon','preview'))]))
        except Exception as e:errors.append({'file':str(p),'error':repr(e)})
    result={'root':str(hs2),'scannedArchives':archives,'maps':records,'errors':errors,'counts':{k:sum(r['sourceType']==k for r in records) for k in ('base','zipmod','studio')}}
    target=out/'inventory.json';target.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print('INVENTORY_OK',str(target),json.dumps(result['counts']),'errors',len(errors),flush=True)


if __name__=='__main__':
    ap=argparse.ArgumentParser();ap.add_argument('--hs2',type=Path,default=Path('F:/ILLUSION/HoneySelect2@'));ap.add_argument('--out',type=Path,default=Path('J:/workspaces/hs2_kks_map_conversion'))
    args=ap.parse_args();inventory(args.hs2,args.out)
