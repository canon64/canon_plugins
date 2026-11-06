"""Read back rebuilt maps, verify source icons, package and install native zipmods."""
import argparse,collections,csv,hashlib,io,json,shutil,sys,zipfile
from pathlib import Path
import xml.etree.ElementTree as ET
sys.path.insert(0,r'F:\kks\work\clothing_setup\pydeps')
import UnityPy
import numpy as np
from PIL import Image
ROOT=Path('F:/kks/work/hs2_map_trials')
BUILD=ROOT/'UnityProject/Build'
DIST=ROOT/'dist'
INSTALL=Path('F:/kks/mods/Local HS2 Conversion Trials')
PACKS={'trial_base':('Base SuiteRoom','ILLUSION'),'trial_mod':('MOD Onsen Afternoon','hooh')}
def sha(p):
    h=hashlib.sha256()
    with Path(p).open('rb') as f:
        for block in iter(lambda:f.read(8*1024*1024),b''):h.update(block)
    return h.hexdigest()
def objects(env,kind):return [(o,o.read_typetree()) for o in env.objects if o.type.name==kind]
def resolve(env,owner,p):
    if not p.get('m_PathID'):return None
    file=owner.assets_file
    if p['m_FileID']:
        name=file.externals[p['m_FileID']-1].path.rsplit('/',1)[-1].lower()
        file=next((f for f in env.assets if f.name.lower()==name),None)
    return file.objects.get(p['m_PathID']) if file else None
def validate(row):
    id=row['id'];number=row['mapId'];prefix='canon_hs2_'+id
    mapping={prefix+'_scene.unity3d':'abdata/map/scene/'+prefix+'.unity3d',prefix+'_mapinfo.unity3d':'abdata/map/list/mapinfo/'+prefix+'.unity3d',prefix+'_thumbnailinfo.unity3d':'abdata/map/list/mapthumbnailinfo/'+prefix+'.unity3d',prefix+'_thumbnail.unity3d':'abdata/map/thumbnail/'+prefix+'.unity3d',prefix+'_hpoints.unity3d':'abdata/h/common/'+prefix+'.unity3d'}
    envs={name:UnityPy.load(str(BUILD/name)) for name in mapping}
    for name,e in envs.items():
        assert all(f.unity_version=='2019.4.9f1' for f in e.assets),(name,'wrong Unity version')
        for o,t in objects(e,'MonoScript'):
            assert t['m_AssemblyName'] in ('Assembly-CSharp','Assembly-CSharp.dll'),(name,t)
            assert t['m_ClassName'] in ('SunLightInfo','MapInfo','MapThumbnailInfo','HPointData','HPointOmitObject'),(name,t['m_ClassName'])
    scene=envs[prefix+'_scene.unity3d']
    gos=objects(scene,'GameObject');names=[t['m_Name'] for o,t in gos]
    assert names.count('Map')==1 and all(n in names for n in ('MapObjectGroup','h_free','yuka00'))
    assert len(objects(scene,'MeshRenderer'))==row['renderers']
    assert len(objects(scene,'MeshCollider'))==row['colliders']>1
    assert not objects(scene,'Camera') and not objects(scene,'AudioListener')
    monos=objects(scene,'MonoBehaviour');assert len(monos)==1
    owner,sun=monos[0]
    script=resolve(scene,owner,sun['m_Script']);assert script.read_typetree()['m_ClassName']=='SunLightInfo'
    go=resolve(scene,owner,sun['m_GameObject']);assert go.read_typetree()['m_Name']=='Map'
    light=resolve(scene,owner,sun['_targetLight']);assert light and light.type.name=='Light'
    assert len(sun['_infos'])==4 and sorted(p['type'] for p in sun['_infos'])==[0,1,2,3]
    assert all(p['visibleList']==[] for p in sun['_infos'])
    info=[p for o,t in objects(envs[prefix+'_mapinfo.unity3d'],'MonoBehaviour') for p in t.get('param',[])];assert len(info)==1
    assert info[0]['No']==number and info[0]['AssetName']==row['scene'] and info[0]['isFreeH'] and info[0]['isH']
    assert 'abdata/'+info[0]['AssetBundleName'] in mapping.values()
    th=[p for o,t in objects(envs[prefix+'_thumbnailinfo.unity3d'],'MonoBehaviour') for p in t.get('param',[])];assert len(th)==1 and th[0]['ID']==number
    assert 'abdata/'+th[0]['Bundle'] in mapping.values()
    thumbnail=envs[prefix+'_thumbnail.unity3d']
    assert any(t['m_Name']==th[0]['Asset'] for o,t in objects(thumbnail,'Sprite')),'Missing Sprite: '+id
    assert any(t['m_Name']==th[0]['Asset'] for o,t in objects(thumbnail,'Texture2D')),'Missing Texture2D: '+id
    texture=next(o.read() for o in thumbnail.objects if o.type.name=='Texture2D' and o.read().m_Name==th[0]['Asset'])
    source=next(r for r in json.loads((ROOT/'sources/maps.json').read_text(encoding='utf-8')) if r['id']==id)
    original=Image.open(ROOT/'UnityProject/Assets/ImportData'/source['thumbnail']).convert('RGBA')
    actual=texture.image.convert('RGBA')
    assert actual.size==original.size,(id,'Source thumbnail resized',actual.size,original.size)
    a=np.array(actual).astype(float);b=np.array(original).astype(float)
    visible=b[:,:,3]>32
    error=float(np.abs(a[visible]-b[visible]).mean())
    assert error<3,(id,'Thumbnail pixels differ from registered HS2 icon',error)
    hp=envs[prefix+'_hpoints.unity3d'];pnames=[t['m_Name'] for o,t in objects(hp,'GameObject')]
    assert all(n+str(number) in pnames for n in ('HPoint_','HPoint_Add_','HPoint_3P_'))
    points=[(o,t) for o,t in objects(hp,'MonoBehaviour') if '_categorys' in t];assert len(points)==3
    for o,t in points:
        renderer=resolve(hp,o,t['_meshRender']);assert renderer and renderer.type.name=='MeshRenderer'
        assert t['_categorys'] and t['_targets']==[] and t['_groups']==[]
    tags=json.loads((ROOT/'UnityProject/Assets/ImportData/native_tags.json').read_text(encoding='utf-8'))
    tag=20000+tags['tags'].index('H/HPoint')
    cols=objects(hp,'CapsuleCollider');assert len(cols)==3
    assert all(resolve(hp,o,t['m_GameObject']).read_typetree()['m_Tag']==tag for o,t in cols)
    for e in envs.values():
        for f in e.assets:
            internal={s.name.lower() for s in e.assets}
            missing=[p.path for p in f.externals if p.path.lower().startswith('archive:') and p.path.rsplit('/',1)[-1].lower() not in internal]
            assert not missing,(id,'Missing bundle dependency',missing)
    return mapping,{'id':id,'mapId':number,'scene':row['scene'],'renderers':row['renderers'],'colliders':row['colliders'],'spawn':row['spawn'],'sunlight':True,'sprite':True,'hpoints':3,'thumbnail':dict(source['thumbnailSource'],pixelMeanError=error),'kks_runtime_verified':False}
def main():
    report=json.loads((BUILD/'build_report.json').read_text())
    inputs=json.loads((ROOT/'sources/maps.json').read_text(encoding='utf-8'))
    assert {r['id'] for r in report['maps']}=={r['id'] for r in inputs},'Stale/incomplete Unity build report'
    for source in inputs:
        for entry in source['sourceFiles']:
            assert sha(entry['path'])==entry['sha256'],('Extracted source changed',entry['path'])
        assert sha(ROOT/'UnityProject/Assets/ImportData'/source['thumbnail'])==source['thumbnailSource']['pngSha256'],'Source icon changed'
    old=json.loads(Path('F:/kks/work/hotel_blockout/reference_mod_room_icons/manifest.json').read_text(encoding='utf-8'))
    reserved={p['map']['No'] for p in old}|{297601}
    assert len({r['mapId'] for r in report['maps']})==len(report['maps'])
    assert all(r['mapId'] not in reserved for r in report['maps'])
    groups=collections.defaultdict(list);validations=[]
    for row in report['maps']:
        print('Validating',row['id'],flush=True)
        mapping,result=validate(row);validations.append(result);groups[row['pack']].append((row,mapping))
    DIST.mkdir(exist_ok=True);INSTALL.mkdir(parents=True,exist_ok=True);archives=[]
    # Scan the actual current installation, not only a saved inventory.
    existing=set(Path('F:/kks/mods').rglob('*.zipmod'))|set(Path('F:/kks/mods').rglob('*.zip'))
    existing_guids={}
    candidates={r['mapId'] for r in report['maps']}
    cached=settings.get('installedIndex')
    index=json.loads(Path(cached).read_text(encoding='utf-8')) if cached and Path(cached).exists() else None
    if index:
        own={settings.get('guidPrefix','local.kks.hs2trial.')+p for p in PACKS}
        for entry in index['archives']:
            path=Path(entry['path']);guid=entry['guid'];existing_guids.setdefault(guid,[]).append(path)
            if guid not in own:assert not set(entry['ids'])&candidates,('Installed map ID collision',str(path),set(entry['ids'])&candidates)
    for path in ([] if index else existing):
        if not zipfile.is_zipfile(path):continue
        with zipfile.ZipFile(path) as z:
            if 'manifest.xml' not in z.namelist():continue
            guid=ET.fromstring(z.read('manifest.xml')).findtext('guid');existing_guids.setdefault(guid,[]).append(path)
            if guid in {settings.get('guidPrefix','local.kks.hs2trial.')+pack for pack in PACKS}:continue
            for n in z.namelist():
                if n.lower().startswith('abdata/map/list/mapinfo/') and n.endswith('.unity3d'):
                    e=UnityPy.load(z.read(n))
                    ids={p['No'] for o,t in objects(e,'MonoBehaviour') for p in t.get('param',[]) if 'No' in p}
                    assert not ids&candidates,('Installed map ID collision',str(path),ids&candidates)
                if n.lower().startswith('abdata/studio/info/') and Path(n).name.lower().startswith('map') and n.endswith('.csv'):
                    for row in csv.reader(io.StringIO(z.read(n).decode('utf-8-sig',errors='replace'))):
                        if row and row[0].isdigit():assert int(row[0]) not in candidates,('Installed Studio ID collision',str(path),row[0])
    print('COLLISION_SCAN_OK',len(existing),'archives',flush=True)
    for pack,maps in groups.items():
        label,author=PACKS[pack];guid=settings.get('guidPrefix','local.kks.hs2trial.')+pack
        import re
        safe=re.sub(r'[<>:"/\\|?*\x00-\x1f]','_',label)[:120].rstrip('. ')
        target=DIST/('[Local][KKS] HS2 '+safe+' v1.0.0.zipmod');installed=INSTALL/target.name
        assert all(p.resolve()==installed.resolve() for p in existing_guids.get(guid,[])),('Duplicate GUID',guid)
        manifest=ET.Element('manifest',{'schema-ver':'1'})
        for k,v in {'guid':guid,'name':'HS2 '+label+' - KKS conversion','version':'1.0.0','author':author+' / local KKS conversion','description':'Source HS2 geometry and textures rebuilt for KKS Free H and Studio. No BepInEx loader.','game':'Koikatsu Sunshine'}.items():ET.SubElement(manifest,k).text=v
        with zipfile.ZipFile(target,'w',compression=zipfile.ZIP_DEFLATED,compresslevel=4) as z:
            z.writestr('manifest.xml',ET.tostring(manifest,encoding='utf-8'))
            studio=io.StringIO();writer=csv.writer(studio,lineterminator='\n');writer.writerow(['Map','','','','','','','Colliders']);writer.writerow(['ID','Category','Name','Bundle Path','File Path','Manifest','Bundle Path','File Path'])
            for row,mapping in maps:
                prefix='canon_hs2_'+row['id'];writer.writerow([row['mapId'],0,row['label'],'map/scene/'+prefix+'.unity3d',row['scene'],'','',''])
                for source,dest in mapping.items():z.write(BUILD/source,dest)
            z.writestr('abdata/studio/info/canon_hs2_'+pack+'/Map_canon_hs2_'+pack+'.csv',studio.getvalue().encode('utf-8'))
            z.writestr('conversion/source.json',json.dumps([p for p in inputs if p['pack']==pack],ensure_ascii=False,indent=2).encode('utf-8'))
        with zipfile.ZipFile(target) as z:
            assert z.testzip() is None and len(z.namelist())==len(set(z.namelist()))
            assert not any(n.endswith('.dll') for n in z.namelist())
            assert ET.fromstring(z.read('manifest.xml')).findtext('game')=='Koikatsu Sunshine'
        pending=installed.with_suffix('.pending')
        shutil.copy2(target,pending)
        assert sha(target)==sha(pending)
        pending.replace(installed)
        assert sha(target)==sha(installed) and target.stat().st_size==installed.stat().st_size
        archives.append({'file':str(target),'installed':str(installed),'guid':guid,'sha256':sha(target),'bytes':target.stat().st_size,'maps':[r['label'] for r,m in maps]})
        print('INSTALLED',installed,flush=True)
        if index is not None:
            index['archives']=[p for p in index['archives'] if p['guid']!=guid]+[{'path':str(installed),'guid':guid,'ids':[r['mapId'] for r,m in maps]}]
            Path(cached).write_text(json.dumps(index,ensure_ascii=False),encoding='utf-8')
    (ROOT/'package_validation.json').write_text(json.dumps({'archives':archives,'maps':validations,'kks_runtime_verified':False,'thumbnail_note':'Original HS2 registered images, extracted from source bundles; output pixel comparison passed'},ensure_ascii=False,indent=2),encoding='utf-8')
    print('RESULT',json.dumps(archives,ensure_ascii=False),flush=True)
if __name__=='__main__':
    ap=argparse.ArgumentParser();ap.add_argument('--config',type=Path,default=Path(__file__).with_name('trials.json'));args=ap.parse_args()
    settings=json.loads(args.config.read_text(encoding='utf-8'));ROOT=Path(settings['work']);BUILD=ROOT/'UnityProject/Build';DIST=ROOT/'dist';INSTALL=Path(settings.get('install',str(INSTALL)))
    if settings.get('guidPrefix'):
        PACKS={r['pack']:(r.get('packLabel',r['label']),r.get('author','HS2 source authors')) for r in settings['maps']}
    main()
