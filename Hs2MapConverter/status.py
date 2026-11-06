"""Show compact, factual bulk-conversion progress."""
import json
from pathlib import Path

root=Path('J:/workspaces/hs2_kks_map_conversion')
state=json.loads((root/'progress.json').read_text(encoding='utf-8'))
print(json.dumps({k:state.get(k) for k in ('status','pid','total','counts','stage','current','updated','fatalError')},ensure_ascii=False,indent=2))
for row in [r for r in state['maps'] if r['status']=='failed'][-3:]:
    print('FAILED',row['spec']['label'],row.get('error','')[-1400:])
