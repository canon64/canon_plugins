"""Extract registered HS2 base/mod maps and their actual registered thumbnails."""
import argparse
import csv
import hashlib
import io
import json
import sys
import zipfile
from pathlib import Path

sys.path.insert(0, 'F:/kks/work/clothing_setup/pydeps')
import UnityPy
from PIL import Image,ImageDraw,ImageFont


def digest(data):
    return hashlib.sha256(data).hexdigest()


def params(env):
    return [p for o in env.objects if o.type.name == 'MonoBehaviour'
            for p in o.read_typetree().get('param', [])]


def prepare(config):
    hs2 = Path(config['hs2'])
    work = Path(config['work'])
    sources = work / 'sources'
    data = work / 'UnityProject/Assets/ImportData'
    sources.mkdir(parents=True, exist_ok=True)
    data.mkdir(parents=True, exist_ok=True)
    results = []
    for spec in config['maps']:
        print('PREPARE', spec['id'], flush=True)
        archive = zipfile.ZipFile(hs2 / spec['archive']) if spec['sourceType'] != 'base' else None
        read = archive.read if archive else lambda n: (hs2 / n).read_bytes()
        if spec['sourceType']=='studio':
            raw=read(spec['registration'])
            try:txt=raw.decode('utf-8-sig')
            except UnicodeDecodeError:txt=raw.decode('cp932')
            candidates=[{'No':int(r[0]),'AssetBundleName':r[2],'AssetName':r[3],'MapNames':[r[1]]} for r in csv.reader(io.StringIO(txt)) if len(r)>=4 and r[0].isdigit() and r[2]==spec['bundle'] and r[3]==spec['asset']]
        else:
            registration = UnityPy.load(read(spec['registration']))
            candidates = params(registration)
        print('REGISTRATIONS', len(candidates), flush=True)
        matches = [p for p in candidates if p.get('No') == spec.get('sourceMapId', -999)]
        if not matches:
            matches=[p for p in candidates if spec.get('sourceScene') and p.get('AssetName','').lower()==spec['sourceScene'].lower()]
        if len(matches) != 1:
            raise ValueError(('Ambiguous/missing source map', spec['id'], matches))
        reg = matches[0]
        print('SELECTED', json.dumps(reg, ensure_ascii=False), flush=True)
        main = 'abdata/' + reg['AssetBundleName']
        initial = [main]
        if archive:
            initial += [n for n in archive.namelist() if '_dependency_bundle_' in n and n.endswith('.unity3d')]
        envs = []
        extracted = []
        available = set()
        required = set()

        def extract(name):
            content = read(name)
            bucket=digest(spec.get('archive','base').encode('utf-8'))[:12]
            dest = sources / bucket / name
            dest.parent.mkdir(parents=True, exist_ok=True)
            if not dest.exists() or digest(dest.read_bytes()) != digest(content):
                dest.write_bytes(content)
            env = UnityPy.load(str(dest))
            envs.append(env)
            extracted.append({'source': name, 'path': str(dest), 'sha256': digest(content)})
            for f in env.assets:
                available.add(f.name.lower())
                required.update(x.path.rsplit('/', 1)[-1].lower() for x in f.externals if x.path.lower().startswith('archive:'))
            return env

        mainenv = None
        for name in dict.fromkeys(initial):
            env = extract(name)
            if name == main:
                mainenv = env
        if required - available and archive:
            # Resolve by serialized CAB identity, rather than assuming filenames match dependencies.
            selected = set(initial)
            for name in archive.namelist():
                if not required - available:
                    break
                if name in selected or not name.endswith('.unity3d'):
                    continue
                content = archive.read(name)
                candidate = UnityPy.load(content)
                if {f.name.lower() for f in candidate.assets} & (required - available):
                    extract(name)
                    selected.add(name)
        if required - available:
            raise ValueError(('Unresolved source dependencies', spec['id'], sorted(required - available)))
        containers = [name for o in mainenv.objects if o.type.name == 'AssetBundle'
                      for name, value in o.read_typetree()['m_Container']]
        scenes = [n for n in containers if Path(n).stem.lower() == reg['AssetName'].lower() and n.lower().endswith(('.unity','.prefab'))]
        if len(scenes) != 1:
            raise ValueError(('Scene mapping', reg, containers))
        thumb_bundle = reg.get('ThumbnailBundle_L')
        thumb_asset = reg.get('ThumbnailAsset_L')
        original=True
        thumbenv=None
        if thumb_bundle and thumb_asset:
            thumbenv = UnityPy.load(read('abdata/' + thumb_bundle))
            icons = [o for o in thumbenv.objects if o.type.name in ('Sprite', 'Texture2D') and o.read().m_Name == thumb_asset]
            icons.sort(key=lambda o: o.type.name != 'Sprite')
            if not icons:raise ValueError(('Source icon not found', thumb_bundle, thumb_asset))
            image = icons[0].read().image.convert('RGBA')
        else:
            found=[]
            if archive:
                for n in archive.namelist():
                    if 'thumb' in n.lower() and n.lower().endswith('.unity3d'):
                        te=UnityPy.load(read(n))
                        for o in te.objects:
                            if o.type.name=='Texture2D' and o.read().m_Name.lower() in (reg['AssetName'].lower(),reg['AssetName'].lower()+'_thumb',reg['AssetName'].lower()+'_thumb_l',reg['AssetName'].lower()+'_hs_thumb_l'):
                                found.append((n,o))
                    elif n.lower().endswith(('.png','.jpg','.jpeg')) and Path(n).stem.lower()==reg['AssetName'].lower():
                        found.append((n,None))
            if len(found)==1:
                thumb_bundle,obj=found[0]
                thumb_asset=obj.read().m_Name if obj else Path(thumb_bundle).stem
                image=obj.read().image.convert('RGBA') if obj else Image.open(io.BytesIO(read(thumb_bundle))).convert('RGBA')
            else:
                # Studio map registrations often have no icon at all. Do not mislabel unrelated images as originals.
                original=False;thumb_bundle='';thumb_asset=''
                image=Image.new('RGBA',(640,280),(40,45,54,255));draw=ImageDraw.Draw(image)
                font=ImageFont.truetype('C:/Windows/Fonts/meiryo.ttc',24)
                draw.text((18,70),spec['label'][:40],font=font,fill='white');draw.text((18,140),'元マップにアイコン登録なし',font=font,fill=(190,190,190))
        thumb = spec['id'] + '_thumb.png'
        image.save(data / thumb)
        # Keep the original portrait icon too, without stretching it into the landscape selection image.
        small_asset = reg.get('ThumbnailAsset_S')
        small_bundle = reg.get('ThumbnailBundle_S')
        if small_asset and small_bundle:
            smallenv = thumbenv if thumbenv is not None and small_bundle == thumb_bundle else UnityPy.load(read('abdata/' + small_bundle))
            small = next((o for o in smallenv.objects if o.type.name == 'Texture2D' and o.read().m_Name == small_asset), None)
            if small:
                small.read().image.save(data / (spec['id'] + '_icon_source.png'))
        result = dict(spec, kind='scene' if scenes[0].lower().endswith('.unity') else 'prefab', asset=scenes[0], bundles=[e['path'] for e in extracted],
                      thumbnail=thumb, scene='CanonHS2_' + spec['id'], registrationData=reg,
                      sourceFiles=extracted, thumbnailSource={'bundle': thumb_bundle, 'asset': thumb_asset,
                      'width': image.width, 'height': image.height, 'original':original,
                      'pixelSha256': digest(image.tobytes()), 'pngSha256': digest((data / thumb).read_bytes())})
        results.append(result)
        if archive:
            archive.close()
        print('PREPARED', spec['id'], len(extracted), 'bundles', image.size, flush=True)
    (sources / 'maps.json').write_text(json.dumps(results, ensure_ascii=False, indent=2), encoding='utf-8')
    print('PREPARATION_OK', len(results), flush=True)


if __name__ == '__main__':
    ap = argparse.ArgumentParser()
    ap.add_argument('--config', type=Path, default=Path(__file__).with_name('trials.json'))
    args = ap.parse_args()
    prepare(json.loads(args.config.read_text(encoding='utf-8')))
