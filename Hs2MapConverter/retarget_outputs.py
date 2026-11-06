"""Update this converter's own records after checked output relocation."""
import json
from pathlib import Path

root=Path('J:/workspaces/hs2_kks_map_conversion')
old=root/'zipmods';new=Path('F:/kks/mods/Local HS2 Bulk Imports')
indexfile=root/'installed_index.json';index=json.loads(indexfile.read_text(encoding='utf-8'))
for entry in index['archives']:
    p=Path(entry['path'])
    if p.parent==old:
        assert (new/p.name).is_file();entry['path']=str(new/p.name)
indexfile.write_text(json.dumps(index,ensure_ascii=False),encoding='utf-8')
statefile=root/'progress.json';state=json.loads(statefile.read_text(encoding='utf-8'))
assert state['status']=='stopped'
for item in state['maps']:
    if item.get('archive'):
        item['archive']['installed']=str(new/Path(item['archive']['installed']).name)
statefile.write_text(json.dumps(state,ensure_ascii=False,indent=2),encoding='utf-8')
stop=root/'STOP'
if stop.exists():stop.unlink()
print('OUTPUT_RECORDS_RETARGETED',new,flush=True)
