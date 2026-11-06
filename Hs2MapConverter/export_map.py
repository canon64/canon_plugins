"""Export real HS2 meshes/material inputs for a KKS-version Unity rebuild."""
import argparse,collections,copy,csv,hashlib,itertools,json,math,struct,sys
from pathlib import Path
sys.path.insert(0,r'F:\kks\work\clothing_setup\pydeps')
import UnityPy,numpy as np
from UnityPy.helpers.MeshHelper import MeshHandler
from PIL import Image
ROOT=Path('F:/kks/work/hs2_map_trials')
DATA=ROOT/'UnityProject/Assets/ImportData'
SOURCE=ROOT/'sources'

def vec(d):return np.array([d[k] for k in 'xyz'],dtype=np.float64)
def rgba(d):return [d[k] for k in 'rgba']
def key(o):return (o.assets_file.name.lower(),o.path_id)
def main():
    rows=json.loads((SOURCE/'maps.json').read_text(encoding='utf-8'))
    files={};environments=[]
    for name in dict.fromkeys(n for r in rows for n in r['bundles']):
        print('Reading source',name,flush=True)
        env=UnityPy.load(str(name));environments.append(env)
        for f in env.assets:
            if f.name.lower() in files:raise ValueError('Duplicate source CAB identity '+f.name)
            files[f.name.lower()]=f
    # Built-in quad/cube meshes (for example mirrors) are external to scene bundles.
    hs2=Path(settings['hs2'])
    for name in ('unity default resources','unity_builtin_extra'):
        env=UnityPy.load(str(hs2/'HoneySelect2_Data/Resources'/name));environments.append(env)
        for f in env.assets:files[name]=f
    def resolve(f,p):
        if not p or not p.get('m_PathID'):return None
        if p['m_FileID']:
            path=f.externals[p['m_FileID']-1].path.lower().rsplit('/',1)[-1]
            f=files.get(path)
            if f is None:return None
        return f.objects.get(p['m_PathID'])
    cache={};matcache={};meshcache={};texcache={};matrices={};goxform={}
    def tree(o):
        k=key(o)
        if k not in cache:cache[k]=o.read_typetree()
        return cache[k]
    for f in files.values():
        for o in f.objects.values():
            if o.type.name=='Transform':goxform[(f.name.lower(),tree(o)['m_GameObject']['m_PathID'])]=o
    def world(o):
        k=key(o)
        if k in matrices:return matrices[k]
        t=tree(o);x,y,z,w=[t['m_LocalRotation'][k] for k in 'xyzw']
        m=np.eye(4)
        m[:3,:3]=np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],[2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],[2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]])@np.diag(vec(t['m_LocalScale']))
        m[:3,3]=vec(t['m_LocalPosition'])
        parent=resolve(o.assets_file,t['m_Father'])
        if parent:m=world(parent)@m
        matrices[k]=m;return m
    def active(o):
        while o:
            g=resolve(o.assets_file,tree(o)['m_GameObject'])
            if not tree(g)['m_IsActive']:return False
            o=resolve(o.assets_file,tree(o)['m_Father'])
        return True
    def texture(o,normal=False):
        if o is None:return ''
        k=(key(o),normal)
        if k in texcache:return texcache[k]
        name='texture_%04d%s.png'%(len(texcache),'_normal' if normal else '')
        data=o.read();im=data.image.convert('RGBA')
        if normal:
            a=np.array(im).astype(np.float32)/255
            # Standard Unity's DXT5nm stores X in alpha and Y in green.
            if data.m_TextureFormat in (12,):
                x=a[:,:,3]*2-1;y=a[:,:,1]*2-1
                a[:,:,0]=(x+1)/2;a[:,:,1]=(y+1)/2;a[:,:,2]=(np.sqrt(np.maximum(0,1-x*x-y*y))+1)/2;a[:,:,3]=1
                im=Image.fromarray(np.clip(a*255,0,255).astype('uint8'),'RGBA')
        im.save(DATA/name);texcache[k]=name;return name
    def material(o):
        if o is None:raise ValueError('Renderer has missing source material')
        k=key(o)
        if k in matcache:return matcache[k]
        t=tree(o);s=t['m_SavedProperties'];floats=dict(s.get('m_Floats',[]));colors=dict(s.get('m_Colors',[]));textures=dict(s.get('m_TexEnvs',[]))
        sh=resolve(o.assets_file,t['m_Shader']);shader=tree(sh).get('m_ParsedForm',{}).get('m_Name','Standard') if sh else 'Standard'
        def prop(names):return next((textures[n] for n in names if n in textures and textures[n]['m_Texture']['m_PathID']),None)
        def tex(names,normal=False):
            p=prop(names)
            return texture(resolve(o.assets_file,p['m_Texture']),normal) if p else ''
        mainprop=prop(('_MainTex','_Albedo','_BaseMap','_Diffuse'))
        result={'id':'material_%04d'%len(matcache),'name':t['m_Name'],'shader':shader,'mode':int(floats.get('_Mode',0)),'color':rgba(colors.get('_Color',{'r':1,'g':1,'b':1,'a':1})),
            'main':tex(('_MainTex','_Albedo','_BaseMap','_Diffuse')),'normal':tex(('_BumpMap','_NormalMap'),True),'metallicMap':tex(('_MetallicGlossMap',)),'specularMap':tex(('_SpecGlossMap',)),'occlusion':tex(('_OcclusionMap',)),'emissionMap':tex(('_EmissionMap',)),
            'metallic':floats.get('_Metallic',0),'smoothness':floats.get('_Glossiness',floats.get('_GlossMapScale',.25)),'cutoff':floats.get('_Cutoff',.5),'bumpScale':floats.get('_BumpScale',1),'emission':rgba(colors.get('_EmissionColor',{'r':0,'g':0,'b':0,'a':1})),
            'scale':[mainprop['m_Scale'][a] for a in 'xy'] if mainprop else [1,1],'offset':[mainprop['m_Offset'][a] for a in 'xy'] if mainprop else [0,0],'queue':t.get('m_CustomRenderQueue',-1),'twoSided':'Two Sided' in shader or floats.get('_Cull',2)==0}
        if 'SpeedTree' in shader:
            result['mode']=1;result['twoSided']=True
        matcache[k]=result;return result
    def mesh(o):
        k=key(o)
        if k not in meshcache:
            mh=MeshHandler(o.read());mh.process()
            meshcache[k]=(mh,mh.get_triangles())
        return meshcache[k]
    maps=[]
    for idx,row in enumerate(rows):
        print('Exporting',row['id'],flush=True)
        if row['kind']=='scene':
            f=files['buildplayer-'+Path(row['asset']).stem.lower()]
            roots=[o for o in f.objects.values() if o.type.name=='Transform' and not tree(o)['m_Father']['m_PathID']]
            allowed=None
        else:
            f=next(f for f in files.values() if any(o.type.name=='AssetBundle' and any(c[0]==row['asset'] for c in tree(o)['m_Container']) for o in f.objects.values()))
            ab=next(tree(o) for o in f.objects.values() if o.type.name=='AssetBundle')
            go=resolve(f,next(c[1]['asset'] for c in ab['m_Container'] if c[0]==row['asset']))
            roots=[goxform[key(go)]];allowed=set();stack=list(roots)
            while stack:
                o=stack.pop();allowed.add(key(o));stack.extend(resolve(o.assets_file,p) for p in tree(o)['m_Children'])
        skipped=set()
        for o in f.objects.values():
            if o.type.name=='LODGroup':
                for lod in tree(o).get('m_LODs',[])[1:]:
                    for p in lod.get('renderers',[]):
                        rr=resolve(f,p if 'm_PathID' in p else p['renderer'])
                        if rr:skipped.add(key(rr))
        geos=[];lights=[];allpoints=[];floors=[];emptyrenderers=[]
        for o in f.objects.values():
            if o.type.name not in ('MeshRenderer','SkinnedMeshRenderer','Light') or key(o) in skipped:continue
            t=tree(o)
            if not t.get('m_Enabled',True):continue
            go=resolve(f,t['m_GameObject']);tr=goxform.get(key(go))
            if tr is None or not active(tr) or (allowed is not None and key(tr) not in allowed):continue
            m=world(tr);name=tree(go)['m_Name']
            if abs(np.linalg.det(m[:3,:3]))<1e-12:continue
            if o.type.name=='Light':
                if t.get('m_Type') not in (0,1,2):continue
                lights.append({'name':name,'type':t['m_Type'],'position':m[:3,3].tolist(),'direction':(m[:3,:3]@np.array([0,0,1])).tolist(),'color':rgba(t['m_Color']),'intensity':t['m_Intensity'],'range':t['m_Range'],'spotAngle':t.get('m_SpotAngle',45)})
                continue
            skinned=o.type.name=='SkinnedMeshRenderer'
            if skinned:
                mesh_pointer=t['m_Mesh'];source=resolve(o.assets_file,mesh_pointer)
            else:
                components=[resolve(go.assets_file,c['component']) for c in tree(go)['m_Component']]
                mf=next((c for c in components if c and c.type.name=='MeshFilter'),None)
                if mf is None:continue
                mesh_pointer=tree(mf)['m_Mesh'];source=resolve(mf.assets_file,mesh_pointer)
            if source is None:
                if not mesh_pointer['m_PathID']:
                    emptyrenderers.append(name);continue
                raise ValueError('Unresolved source mesh '+name)
            mh,submeshes=mesh(source)
            if not mh.m_Vertices:continue
            verts=np.asarray(mh.m_Vertices)[:,:3]
            vnorm=np.asarray(mh.m_Normals)[:,:3] if mh.m_Normals else np.zeros_like(verts)
            if skinned:
                # Bake the source rest pose once; no HS2 animator or runtime script is needed.
                bind=tree(source)['m_BindPose'];weights=np.array(mh.m_BoneWeights);indices=np.array(mh.m_BoneIndices)
                if weights.shape!=indices.shape:raise ValueError('Invalid skin weights '+name)
                worldverts=np.zeros_like(verts);worldnorm=np.zeros_like(vnorm)
                for bi,p in enumerate(t['m_Bones']):
                    bone=resolve(o.assets_file,p)
                    if bone is None:raise ValueError('Unresolved skin bone '+name)
                    b=np.array([[bind[bi]['e%d%d'%(i,j)] for j in range(4)] for i in range(4)])
                    bm=world(bone)@b
                    w=np.where(indices==bi,weights,0).sum(axis=1)
                    worldverts+=(verts@bm[:3,:3].T+bm[:3,3])*w[:,None]
                    worldnorm+=(vnorm@np.linalg.inv(bm[:3,:3]))*w[:,None]
                verts=worldverts;vnorm=worldnorm
            batch=t.get('m_StaticBatchInfo',{});static=batch.get('subMeshCount',0)>0
            matrix=np.eye(4) if static or skinned else m
            start=batch.get('firstSubMesh',0) if static else 0
            for j,p in enumerate(t['m_Materials']):
                si=start+j
                if si>=len(submeshes):continue
                tri=np.array(submeshes[si],dtype=np.int64)
                if not tri.size:continue
                tri+=int(source.read().m_SubMeshes[si].baseVertex or 0)
                used,inv=np.unique(tri,return_inverse=True)
                pts=verts[used,:3]
                points=pts@matrix[:3,:3].T+matrix[:3,3]
                normals=vnorm[used,:3]@np.linalg.inv(matrix[:3,:3])
                lengths=np.linalg.norm(normals,axis=1);normals/=np.maximum(lengths[:,None],1e-12)
                uv=np.asarray(mh.m_UV0)[used,:2] if mh.m_UV0 else np.zeros((len(used),2))
                tris=inv.reshape(-1,3).astype('<i4')
                if np.linalg.det(matrix[:3,:3])<0:tris=tris[:,[0,2,1]]
                mat=material(resolve(f,p))
                if mat['twoSided']:
                    count=len(points)
                    points=np.concatenate((points,points));normals=np.concatenate((normals,-normals));uv=np.concatenate((uv,uv))
                    tris=np.concatenate((tris,tris[:,[0,2,1]]+count)).astype('<i4')
                geos.append({'name':name,'material':mat['id'],'points':points,'normals':normals,'uv':uv,'tris':tris,'collider':mat['mode']==0 and not any(s in name.lower() for s in ('curtain','leaf','plant','light','lamp','glass','water','sky'))})
                allpoints.append(points)
                if 'floor' in name.lower() or 'tatami' in name.lower() or 'yuka' in name.lower():floors.append(points)
        if not geos:raise ValueError('No source geometry '+row['id'])
        total=np.concatenate(allpoints);lo=total.min(0);hi=total.max(0)
        # Source HS2 human-scale geometry is ten times KKS; retain an explicit transform.
        scale=float(row['scale'])
        if floors:
            floor=max(floors,key=lambda a:np.prod(np.maximum(a.max(0)-a.min(0),1e-5)[[0,2]]))
            anchor=(floor.min(0)+floor.max(0))/2;anchor[1]=floor[:,1].max()
        else:anchor=(lo+hi)/2;anchor[1]=lo[1]
        entries=[]
        for j,g in enumerate(geos):
            points=(g['points']-anchor)*scale
            name=row['id']+'_%04d.bin'%j
            values=np.concatenate([points,g['normals'],g['uv']],axis=1).astype('<f4')
            with (DATA/name).open('wb') as out:
                out.write(struct.pack('<ii',len(points),g['tris'].size));out.write(values.tobytes());out.write(g['tris'].tobytes())
            entries.append({'name':g['name'],'file':name,'material':g['material'],'collider':g['collider']})
        for l in lights:l['position']=((np.array(l['position'])-anchor)*scale).tolist();l['range']*=scale
        thumb=row['thumbnail']
        if not (DATA/thumb).is_file():raise ValueError('Registered source thumbnail missing '+thumb)
        maps.append({'id':row['id'],'label':row['label'],'mapId':row['mapId'],'scene':row['scene'],'pack':row['pack'],'meshes':entries,'lights':lights,'thumbnail':thumb,'bounds':((hi-lo)*scale).tolist(),'sourceAnchor':anchor.tolist(),'scale':scale,'thumbnailSource':row['thumbnailSource'],'emptySourceRenderers':emptyrenderers})
        print('EXPORTED',row['id'],len(entries),'meshes; size',maps[-1]['bounds'],flush=True)
    config={'maps':maps,'materials':list(matcache.values())}
    config['conversionNotes']=['Static rest-pose geometry; HS2 scripts, animation, reflection/water effects and baked lighting are not reproduced. Nonstandard shaders are mapped to KKS-compatible Standard materials.']
    (DATA/'imports.json').write_text(json.dumps(config,ensure_ascii=False),encoding='utf-8')
    print('RESULT',len(maps),'maps',len(matcache),'materials',len(texcache),'textures',flush=True)
if __name__=='__main__':
    ap=argparse.ArgumentParser();ap.add_argument('--config',type=Path,default=Path(__file__).with_name('trials.json'));args=ap.parse_args()
    settings=json.loads(args.config.read_text(encoding='utf-8'));ROOT=Path(settings['work']);DATA=ROOT/'UnityProject/Assets/ImportData';SOURCE=ROOT/'sources'
    main()
