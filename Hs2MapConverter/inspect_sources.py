"""Read source HS2 map registrations, bundle names, and thumbnail references."""
import argparse
import collections
import json
import sys
import zipfile
from pathlib import Path

sys.path.insert(0, 'F:/kks/work/clothing_setup/pydeps')
import UnityPy


def inspect(path, summary=False, geometry=False):
    print('SOURCE', path, flush=True)
    if path.suffix.lower() == '.zipmod':
        with zipfile.ZipFile(path) as z:
            for n in z.namelist():
                if any(k in n.lower() for k in ('mapinfo', 'thumbnail', 'dependency')) and not n.endswith('.png'):
                    print('ENTRY', n)
        return
    env = UnityPy.load(str(path))
    print('TYPES', dict(collections.Counter(o.type.name for o in env.objects)))
    for f in env.assets:
        print('FILE', f.name, 'EXTERNALS', [x.path for x in f.externals])
    for o in env.objects:
        if geometry:
            if o.type.name == 'Material':
                t=o.read_typetree()
                shader=o.read().m_Shader.read_typetree()
                print('MATERIAL',t['m_Name'],shader.get('m_ParsedForm',{}).get('m_Name'), 'TEX', [k for k,v in t['m_SavedProperties']['m_TexEnvs'] if v['m_Texture']['m_PathID']])
            elif o.type.name == 'SkinnedMeshRenderer':
                t=o.read_typetree()
                print('SKINNED', o.read().m_GameObject.read().m_Name, 'mesh',t['m_Mesh'], 'bones',len(t['m_Bones']))
            elif o.type.name == 'MeshFilter':
                t=o.read_typetree()
                if t['m_Mesh']['m_FileID'] and 'default resources' in o.assets_file.externals[t['m_Mesh']['m_FileID']-1].path:
                    print('BUILTIN_MESH',o.read().m_GameObject.read().m_Name,t['m_Mesh'])
            continue
        if o.type.name == 'AssetBundle':
            t = o.read_typetree()
            print('CONTAINER', [v[0] for v in t['m_Container']])
        elif o.type.name == 'MonoBehaviour':
            t = o.read_typetree()
            print('MONO', json.dumps(t if not summary else {'name': t.get('m_Name'), 'keys': list(t)}, ensure_ascii=False)[:24000])
        elif o.type.name in ('Texture2D', 'Sprite'):
            d = o.read()
            print('IMAGE', o.type.name, d.m_Name, getattr(d, 'm_Width', ''), getattr(d, 'm_Height', ''))


if __name__ == '__main__':
    ap = argparse.ArgumentParser()
    ap.add_argument('paths', nargs='+')
    ap.add_argument('--summary', action='store_true')
    ap.add_argument('--geometry', action='store_true')
    args = ap.parse_args()
    for p in args.paths:
        inspect(Path(p), args.summary, args.geometry)
