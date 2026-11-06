"""Resumeable, sequential HS2 map conversion; isolate failed maps and keep reports."""
import collections
import argparse
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import time
from pathlib import Path

SOURCE=Path(__file__).resolve().parent
ROOT=Path('J:/workspaces/hs2_kks_map_conversion')
WORK=ROOT/'batch'
PROJECT=WORK/'UnityProject'
PYTHON=Path('J:/system_tools/venvs/py310-common/Scripts/python.exe')
UNITY=Path('C:/Program Files/Unity/Hub/Editor/2019.4.9f1/Editor/Unity.exe')
STATE=ROOT/'progress.json'


def save(state):
    state['updated']=time.strftime('%Y-%m-%d %H:%M:%S')
    state['counts']=dict(collections.Counter(m['status'] for m in state['maps']))
    temp=STATE.with_suffix('.tmp');temp.write_text(json.dumps(state,ensure_ascii=False,indent=2),encoding='utf-8');temp.replace(STATE)


def clear_generated(path):
    target=path.resolve();root=WORK.resolve()
    if target==root or not target.is_relative_to(root):raise ValueError(('Unsafe cleanup',target))
    if target.exists():shutil.rmtree(target)


def setup_project():
    template=Path('F:/kks/work/hs2_hotel_zipmods/UnityProject')
    for sub in ('Assets/Editor','Assets/ImportData','ProjectSettings','Packages'):(PROJECT/sub).mkdir(parents=True,exist_ok=True)
    for p in (template/'Assets').glob('*.cs'):shutil.copy2(p,PROJECT/'Assets'/p.name)
    for n in ('TagManager.asset','ProjectVersion.txt'):shutil.copy2(template/'ProjectSettings'/n,PROJECT/'ProjectSettings'/n)
    shutil.copy2(template/'Packages/manifest.json',PROJECT/'Packages/manifest.json')
    shutil.copy2(template/'Assets/ImportData/native_tags.json',PROJECT/'Assets/ImportData/native_tags.json')
    shutil.copy2(SOURCE/'UnityEditor/Hs2MapBuild.cs',PROJECT/'Assets/Editor/Hs2MapBuild.cs')


def make_state():
    inventory=json.loads((ROOT/'inventory.json').read_text(encoding='utf-8'))
    records=inventory['maps']
    # Only the latest installed version of a duplicate source GUID is eligible.
    def version(v):return tuple((0,int(x)) if x.isdigit() else (1,x.lower()) for x in re.findall(r'\d+|[^\d.]+',v or '0'))
    chosen={}
    for r in records:
        if r.get('guid'):
            candidate=(version(r.get('version')),r['archive'])
            if r['guid'] not in chosen or candidate>chosen[r['guid']]:chosen[r['guid']]=candidate
    records=[r for r in records if not r.get('guid') or r['archive']==chosen[r['guid']][1]]
    installed=json.loads((ROOT/'installed_index.json').read_text(encoding='utf-8'))
    used={n for p in installed['archives'] for n in p['ids']}
    number=299000;maps=[]
    for r in records:
        while number in used:number+=1
        identity='|'.join((r.get('guid') or r.get('archive','base'),r['bundle'].lower(),r['asset'].lower()))
        short=hashlib.sha256(identity.encode('utf-8')).hexdigest()[:16]
        spec={k:v for k,v in r.items() if k not in ('data','aliases','imageCandidates')}
        spec.update(id='bulk_'+short,pack=short,mapId=number,sourceScene=r['asset'],scale=.1,
                    label='HS2 '+r['label'],packLabel=str(number)+' '+r['label'],author=r.get('guid') or 'ILLUSION')
        maps.append({'key':short,'status':'pending','spec':spec})
        used.add(number);number+=1
    return {'status':'running','pid':os.getpid(),'total':len(maps),'sourceInventoryCount':len(inventory['maps']),'maps':maps,'kks_runtime_verified':False}


def step(cmd,log,timeout=7200):
    print('STEP',log.name,flush=True)
    env=dict(os.environ)
    for key in ('TEMP','TMP'):env[key]=str(ROOT/'temp')
    env['APPDATA']=str(ROOT/'UnityUser/Roaming');env['LOCALAPPDATA']=str(ROOT/'UnityUser/Local')
    env['PYTHONIOENCODING']='utf-8';env['PYTHONUNBUFFERED']='1'
    for p in (ROOT/'temp',ROOT/'UnityUser/Roaming',ROOT/'UnityUser/Local'):p.mkdir(parents=True,exist_ok=True)
    info=subprocess.STARTUPINFO();info.dwFlags|=subprocess.STARTF_USESHOWWINDOW;info.wShowWindow=0
    with log.open('w',encoding='utf-8') as stream:
        process=subprocess.Popen([str(x) for x in cmd],stdout=stream,stderr=subprocess.STDOUT,env=env,startupinfo=info)
        try:code=process.wait(timeout=timeout)
        except subprocess.TimeoutExpired:
            process.kill();process.wait();raise RuntimeError('Stage timeout: '+str(log))
    if code:raise RuntimeError('Stage failed ('+str(code)+'): '+str(log)+'\n'+log.read_text(encoding='utf-8',errors='replace')[-2500:])


def convert(group,state,serial):
    job=ROOT/'reports'/('%04d'%serial);job.mkdir(parents=True,exist_ok=True)
    for p in (WORK/'sources',PROJECT/'Assets/ImportData',PROJECT/'Assets/Generated',PROJECT/'Build',WORK/'dist'):clear_generated(p)
    setup_project()
    config={'work':str(WORK),'hs2':'F:/ILLUSION/HoneySelect2@','maps':[m['spec'] for m in group],
            'install':'F:/kks/mods/Local HS2 Bulk Imports','guidPrefix':'local.kks.hs2all.','installedIndex':str(ROOT/'installed_index.json')}
    configpath=job/'config.json';configpath.write_text(json.dumps(config,ensure_ascii=False,indent=2),encoding='utf-8')
    for stage,script in (('extract','prepare_sources.py'),('export','export_map.py')):
        state['stage']=stage;save(state)
        step([PYTHON,'-B',SOURCE/script,'--config',configpath],job/(stage+'.log'))
    state['stage']='build';save(state)
    unitylog=job/'unity_build.log'
    step([UNITY,'-batchmode','-nographics','-projectPath',PROJECT,'-executeMethod','Hs2MapBuild.Build','-logFile',unitylog],job/'unity_process.log')
    if 'HS2_ZIPMOD_BUILD_OK '+str(len(group))+' maps' not in unitylog.read_text(encoding='utf-8',errors='replace'):raise RuntimeError('Unity did not confirm successful build: '+str(unitylog))
    state['stage']='validate_package';save(state)
    step([PYTHON,'-B',SOURCE/'package_maps.py','--config',configpath],job/'package.log')
    result=json.loads((WORK/'package_validation.json').read_text(encoding='utf-8'))
    exports=json.loads((PROJECT/'Assets/ImportData/imports.json').read_text(encoding='utf-8'))
    conversion={'notes':exports.get('conversionNotes',[]),'maps':[{k:v for k,v in m.items() if k not in ('meshes','lights')} for m in exports['maps']],
                'shaders':dict(collections.Counter(m['shader'] for m in exports['materials']))}
    (job/'conversion_notes.json').write_text(json.dumps(conversion,ensure_ascii=False,indent=2),encoding='utf-8')
    shutil.copy2(WORK/'package_validation.json',job/'validation.json')
    shutil.copy2(WORK/'sources/maps.json',job/'sources.json')
    for m in group:
        m['status']='converted';m['report']=str(job/'validation.json')
        m['archive']=next(a for a in result['archives'] if a['guid']=='local.kks.hs2all.'+m['key'])
    save(state)


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--retry-failed',action='store_true');args=parser.parse_args()
    print('BULK_START',os.getpid(),flush=True)
    ROOT.mkdir(parents=True,exist_ok=True);(ROOT/'zipmods').mkdir(exist_ok=True)
    state=json.loads(STATE.read_text(encoding='utf-8')) if STATE.exists() else make_state()
    state['status']='running';state['pid']=os.getpid()
    for m in state['maps']:
        if m['status']=='converting':m['status']='pending'
        if args.retry_failed and m['status']=='failed':m['status']='pending'
    save(state)
    pending=[m for m in state['maps'] if m['status']=='pending']
    bysource=collections.OrderedDict()
    for m in pending:bysource.setdefault(m['spec'].get('archive','base'),[]).append(m)
    queue=[items[i:i+4] for items in bysource.values() for i in range(0,len(items),4)]
    serial=max([int(p.name) for p in (ROOT/'reports').glob('[0-9]*') if p.name.isdigit()]+[0]) if (ROOT/'reports').exists() else 0
    while queue:
        if (ROOT/'STOP').exists():state['status']='stopped';save(state);return
        if shutil.disk_usage(ROOT).free<10*1024**3:raise RuntimeError('Less than 10 GiB free on output drive')
        serial+=1;group=queue.pop(0)
        state['current']=[m['spec']['label'] for m in group]
        for m in group:m['status']='converting'
        save(state);print('JOB',serial,'maps',len(group),'remaining',sum(len(g) for g in queue),state['current'],flush=True)
        try:convert(group,state,serial)
        except Exception as e:
            print('JOB_FAILED',serial,str(e),flush=True)
            for m in group:
                m['status']='pending' if len(group)>1 else 'failed'
                m['error']=str(e);m['failedReport']=str(ROOT/'reports'/('%04d'%serial))
            if len(group)>1:queue[0:0]=[[m] for m in group]
            save(state)
    state['status']='finished';state['stage']='finished';state['current']=[];save(state)
    print('BULK_FINISHED',state['counts'],flush=True)


if __name__=='__main__':
    try:main()
    except Exception as e:
        print('BULK_FATAL',repr(e),flush=True)
        if STATE.exists():
            state=json.loads(STATE.read_text(encoding='utf-8'));state['status']='interrupted';state['fatalError']=repr(e);save(state)
        raise
