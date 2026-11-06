"""Record an explicit user-requested stop after the worker process tree exits."""
import collections
import datetime
import json
from pathlib import Path

root=Path('J:/workspaces/hs2_kks_map_conversion')
target=root/'progress.json'
state=json.loads(target.read_text(encoding='utf-8'))
state['interruptedStage']=state.get('stage')
for entry in state['maps']:
    if entry['status']=='converting':entry['status']='pending'
state.update(status='stopped',stage='stopped',current=[],pid=None,stopReason='user_requested',
             updated=datetime.datetime.now(datetime.timezone(datetime.timedelta(hours=9))).isoformat())
state['counts']=dict(collections.Counter(m['status'] for m in state['maps']))
temp=target.with_suffix('.tmp');temp.write_text(json.dumps(state,ensure_ascii=False,indent=2),encoding='utf-8');temp.replace(target)
print('STOPPED',json.dumps(state['counts']),flush=True)
