"""Capture live on-disk KKS map IDs/GUIDs once for a sequential bulk conversion."""
import csv
import io
import json
import sys
import zipfile
import xml.etree.ElementTree as ET
from pathlib import Path
sys.path.insert(0,'F:/kks/work/clothing_setup/pydeps')
import UnityPy


def main():
    print('KKS_INDEX_START',flush=True)
    records=[];unreadable=[]
    paths=list(Path('F:/kks/mods').rglob('*.zipmod'))+list(Path('F:/kks/mods').rglob('*.zip'))
    for i,p in enumerate(paths):
        if i%500==0:print('INDEX',i,len(paths),flush=True)
        if not zipfile.is_zipfile(p):
            unreadable.append(str(p));continue
        with zipfile.ZipFile(p) as z:
            names=z.namelist()
            if 'manifest.xml' not in names:continue
            guid=ET.fromstring(z.read('manifest.xml')).findtext('guid')
            ids=set()
            for n in names:
                if n.lower().startswith('abdata/map/list/mapinfo/') and n.lower().endswith('.unity3d'):
                    env=UnityPy.load(z.read(n))
                    ids.update(v['No'] for o in env.objects if o.type.name=='MonoBehaviour' for v in o.read_typetree().get('param',[]) if 'No' in v)
                elif n.lower().startswith('abdata/studio/info/') and Path(n).name.lower().startswith('map') and n.lower().endswith('.csv'):
                    for row in csv.reader(io.StringIO(z.read(n).decode('utf-8-sig',errors='replace'))):
                        if row and row[0].isdigit():ids.add(int(row[0]))
            records.append({'path':str(p),'guid':guid,'ids':sorted(ids)})
    target=Path('J:/workspaces/hs2_kks_map_conversion/installed_index.json')
    target.parent.mkdir(parents=True,exist_ok=True)
    target.write_text(json.dumps({'archives':records,'unreadableArchives':unreadable},ensure_ascii=False),encoding='utf-8')
    print('KKS_INDEX_OK',len(records),'unreadable',len(unreadable),flush=True)


if __name__=='__main__':main()
