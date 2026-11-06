using System;
using System.Linq;
using UnityEngine;
using CanonGate;
using Valve.VR;
using VRGIN.Core;
using MainGameVoiceFaceEventBridge.Audio;
using Plugin=MainGameCandle.Plugin;
using Object=UnityEngine.Object;

namespace MainGameCandle.Tests
{
    static class Program
    {
        static int _checks;
        static void Check(bool ok,string label){_checks++;if(!ok)throw new Exception(label);}
        static void Near(double actual,double wanted,string label){Check(Math.Abs(actual-wanted)<1e-4,label+" actual="+actual);}
        static Plugin Session(bool vr=false)
        {
            Object.Objects.Clear();GateApi.Allowed=true;VR.Active=vr;VR.Mode=new VR.Rig();
            var scene=new GameObject("H scene").AddComponent<MonoBehaviour>();
            GateApi.Current=new GateApi.SessionContext{Kind=GateApi.SessionKind.H,Scene=scene,Id=1};
            Camera.main=new GameObject("view").AddComponent<Camera>();
            var plugin=new Plugin();plugin.Begin();return plugin;
        }
        static Mesh Plane()=>new Mesh{vertices=new[]{new Vector3(-1,0,-1),new Vector3(1,0,-1),new Vector3(0,0,1)},uv=new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(.5f,1)},triangles=new[]{0,2,1}};
        static WaxSurface Skin(out ChaControl actor,out SkinnedMeshRenderer renderer,bool readable=true)
        {
            actor=new GameObject("actor").AddComponent<ChaControl>();renderer=new GameObject("skin").AddComponent<SkinnedMeshRenderer>();renderer.transform.SetParent(actor.transform,false);
            renderer.sharedMaterial=NativeMaterial();renderer.sharedMesh=Plane();renderer.sharedMesh.isReadable=readable;
            var bone=new GameObject("bone").transform;bone.SetParent(actor.transform,false);
            renderer.bones=new[]{bone};renderer.rootBone=bone;
            renderer.sharedMesh.boneWeights=Enumerable.Repeat(new BoneWeight{boneIndex0=0,weight0=1},3).ToArray();renderer.sharedMesh.bindposes=new[]{Matrix4x4.identity};
            actor.rendBody=renderer;return new WaxSurface(renderer);
        }
        static WaxHit SurfaceHit(WaxSurface surface)
        {
            Time.frameCount++;Check(surface.Ray(new Ray(new Vector3(0,1,0),Vector3.down),2,out var hit),"visible surface hit");return hit;
        }
        static void Main()
        {
            Flow();Settings();OverlayOrder();ModelConstruction();WaxRendering();DropOrigin();Relief();Ignition();Interaction();Surface();WaxLifetime();Reaction();Bridge();ContactLoading();NativeVoiceTable();Layout();
            Console.WriteLine("CANDLE_OK checks="+_checks+" (production source, simulated Unity/VR boundary; no game execution)");
        }
        static void Flow()
        {
            Near(CandleFlow.Rate(0,45,1),0,"upright");Near(CandleFlow.Rate(45,45,1),0,"threshold");Near(CandleFlow.Rate(90,45,1),6,"horizontal");Near(CandleFlow.Rate(135,45,1),15,"downward");
            double previous=0;for(int angle=0;angle<=180;angle++){double rate=CandleFlow.Rate(angle,45,1);Check(rate>=previous,"monotonic tilt "+angle);previous=rate;}
            var reference=new System.Collections.Generic.List<double>();
            foreach(int fps in new[]{4,30,60,90,144})
            {
                var flow=new CandleFlow(new System.Random(42));var portions=new System.Collections.Generic.List<double>();
                for(int i=0;i<fps*10;i++)flow.Advance(1d/fps,15,v=>portions.Add(v));
                Check(portions.Sum()<=150.00001&&portions.Sum()>148,"bounded remainder preserves total volume "+fps);
                if(reference.Count==0)reference=portions;else Check(reference.SequenceEqual(portions),"identical randomized emissions across frame rates "+fps);
            }
            Check(reference.Distinct().Count()>100&&reference.Min()<.1&&reference.Max()>1.9&&reference.Max()<=2&&reference.Max()/reference.Min()>20,"wide volume variation with capped long waits");
            foreach(int seed in Enumerable.Range(0,100))
            {
                var capped=new CandleFlow(new System.Random(seed));double time=0,lastTime=0;bool observed=false;
                for(int step=0;step<3000;step++){time+=.01;capped.Advance(.01,1.5,v=>{Check(time-lastTime<=2d/1.5+.010001,"default wait bounded including first drop");Check(v<=2,"bounded accumulation preserves proportional amount");lastTime=time;observed=true;});}
                Check(observed&&time-lastTime<=2d/1.5+.010001,"no unbounded tail after last emission");
            }
            var f=new CandleFlow(new System.Random(42));double elapsed=0,last=0,lastVolume=0;int emitted=0;
            for(int i=0;i<10000;i++){elapsed+=.001;f.Advance(.001,6,v=>{if(emitted++>0)Check(Math.Abs((elapsed-last)*6-v)<.0061,"longer wait carries proportional volume");last=elapsed;lastVolume=v;});}
            Check(lastVolume>0,"emissions observed");
            f.Reset();Check(f.Advance(.01,6)==0,"reset discards accumulation");
            Check(f.Advance(1,15)<=25,"stalled frame does not accumulate backlog");Check(f.Advance(.1,double.NaN)==0,"invalid rate stops");
            var slow=new CandleFlow(new System.Random(42));var fast=new CandleFlow(new System.Random(42));int a=0,b=0;
            for(int i=0;i<600;i++){a+=slow.Advance(1d/60,CandleFlow.Rate(65,45,1));b+=fast.Advance(1d/60,CandleFlow.Rate(110,45,1));}
            Check(b>a*2,"tilt increases randomized frequency");
        }
        static void Settings()
        {
            var p=Session();p.PlaybackBarPrimaryAction();
            Near(p.DefaultDropVolume,3,"default drops three times old volume");Near(p.DefaultVariation,100,"wide default variation");
            var wick=Object.Objects.Last(o=>o.name=="Black wick");var top=wick.GetComponent<MeshFilter>().sharedMesh.vertices.Max(v=>v.y);
            p.AdjustHeight(30);
            Near(p.Model.Tip.localPosition.y,CandleModel.WickBase+.030,"height setting moves emitter immediately");
            Near(wick.transform.TransformPoint(new Vector3(0,top,0)).y,p.Model.Tip.position.y,"height setting moves wick and flame together");
            p.AdjustWax(1,3,0);p.Model.Root.transform.rotation=Quaternion.Euler(0,0,90);
            for(int i=0;i<240;i++)p.TickCandle(1f/60);
            var drops=Object.Objects.Where(o=>o.name=="Falling candle wax").ToArray();
            Check(drops.Length==6,"frequency setting defaults to 1.5 drops per second in production loop");
            Near(Math.Pow(drops[0].transform.localScale.x/.0022,3),3,"volume setting controls production drop geometry");
            p.AdjustWax(0,3,0);for(int i=0;i<120;i++)p.TickCandle(1f/60);
            Check(Object.Objects.Count(o=>o.name=="Falling candle wax")==6,"zero frequency stops new drops");
            p.AdjustWax(2,6,0);for(int i=0;i<120;i++)p.TickCandle(1f/60);
            Check(Object.Objects.Count(o=>o.name=="Falling candle wax")==12,"frequency setting doubles production cadence");
            Near(Math.Pow(Object.Objects.Last(o=>o.name=="Falling candle wax").transform.localScale.x/.0022,3),6,"changing volume applies to next drops");
            p.Stop();
            var flow=new CandleFlow(new System.Random(42));var portions=new System.Collections.Generic.List<double>();
            for(int i=0;i<600;i++)flow.Advance(1d/60,6,v=>portions.Add(v),0,3);
            Check(portions.Count==60&&portions.All(v=>v==3),"zero variation is regular at selected volume");
        }
        static Material NativeMaterial()=>new Material{mainTexture=Solid(new Color32(230,180,150,255))};
        static Texture2D Solid(Color32 color){var t=new Texture2D(2,2,TextureFormat.RGBA32,false);t.SetPixels32(Enumerable.Repeat(color,4).ToArray());return t;}
        static Texture Assigned(Renderer r){var b=new MaterialPropertyBlock();r.GetPropertyBlock(b,0);return b.GetTexture(Shader.PropertyToID("_MainTex"))??r.sharedMaterial.mainTexture;}
        static void OverlayOrder()
        {
            Session();var renderer=new GameObject("native skin").AddComponent<MeshRenderer>();renderer.sharedMaterial=NativeMaterial();
            var paint=Solid(new Color32(0,0,255,255));var wax=Solid(new Color32(184,5,7,255));
            MainGameMagicPaint.SkinOverlayApi.SetLayer(MainGameMagicPaint.Plugin.Guid,renderer,paint,100);
            MainGameMagicPaint.SkinOverlayApi.SetLayer(Plugin.Guid,renderer,wax,200);
            Check(Assigned(renderer).Pixels[0].r==184,"wax over doodles");
            paint.SetPixels32(Enumerable.Repeat(new Color32(0,255,0,255),4).ToArray());
            MainGameMagicPaint.SkinOverlayApi.SetLayer(MainGameMagicPaint.Plugin.Guid,renderer,paint,100);
            Check(Assigned(renderer).Pixels[0].r==184,"later doodles stay below wax");
            MainGameMagicPaint.SkinOverlayApi.RemoveLayer(Plugin.Guid,renderer);
            Check(Assigned(renderer).Pixels[0].g==255,"erasing wax preserves doodles");
            var nextSkin=Solid(new Color32(100,110,120,255));var nativeBlock=new MaterialPropertyBlock();nativeBlock.SetTexture(Shader.PropertyToID("_MainTex"),nextSkin);renderer.SetPropertyBlock(nativeBlock);
            MainGameMagicPaint.SkinOverlayApi.Refresh(renderer);
            Check(MainGameMagicPaint.SkinOverlayApi.GetBindings(renderer).Single().Source==nextSkin,"native common texture refresh passes through indexed overlay");
            MainGameMagicPaint.SkinOverlayApi.SetLayer(Plugin.Guid,renderer,wax,200);
            MainGameMagicPaint.SkinOverlayApi.RemoveLayer(MainGameMagicPaint.Plugin.Guid,renderer);
            Check(Assigned(renderer).Pixels[0].r==184,"erasing doodles preserves wax");
            GateApi.Allowed=false;MainGameMagicPaint.SkinOverlayApi.RemoveLayer(Plugin.Guid,renderer);
            var restored=new MaterialPropertyBlock();renderer.GetPropertyBlock(restored,0);
            Check(restored.GetTexture(Shader.PropertyToID("_MainTex"))==null,"revoked cleanup removes indexed overlay");
            renderer.GetPropertyBlock(restored);Check(restored.GetTexture(Shader.PropertyToID("_MainTex"))==nextSkin,"cleanup preserves latest native common skin");
            Check(!MainGameMagicPaint.SkinOverlayApi.SetLayer(Plugin.Guid,renderer,wax,200),"revoked permit rejects new layer");GateApi.Allowed=true;
        }
        static void ModelConstruction()
        {
            foreach(var missing in new[]{new string[0],new[]{"Particles/Standard Unlit","Particles/Additive","Legacy Shaders/Particles/Additive"},new[]{"Standard","Legacy Shaders/Diffuse","Unlit/Color","Particles/Standard Unlit","Particles/Additive","Legacy Shaders/Particles/Additive"}})
            {
                Shader.Unavailable.Clear();foreach(var name in missing)Shader.Unavailable.Add(name);
                var p=Session();p.PlaybackBarPrimaryAction();Check(p.Model!=null&&p.Model.Root.activeInHierarchy,"actual model constructor survives stripped built-in shaders");
                var body=Object.Objects.Last(o=>o.name=="Red wax body");var mesh=body.GetComponent<MeshFilter>().sharedMesh;
                Check(mesh.vertices.Length>100&&mesh.triangles.Length>100,"actual body mesh exists");
                Check(Math.Abs(mesh.vertices.Max(v=>v.y)-mesh.vertices.Min(v=>v.y)-.2)<.003,"body is approximately twenty cm");
                var flame=Object.Objects.Last(o=>o.name=="Candle flame");Check(flame.GetComponent<ParticleSystem>().isPlaying&&flame.GetComponent<Light>().enabled,"constructed flame and light start on recall");
                p.Stop();Check(!body.activeInHierarchy&&!flame.activeInHierarchy,"actual model children disappear on cleanup");
            }
            Shader.Unavailable.Clear();
            var vr=Session(true);vr.DesktopFocus(false);vr.PlaybackBarPrimaryAction();Check(vr.Model!=null&&vr.Loop,"VR recall is independent of desktop focus");vr.Focus(false);Check(!vr.Loop,"actual VR input focus still stops dripping");vr.Stop();
        }
        static void AssertOpaque(Material material,string label)
        {
            Check(material.color.a==1&&material.color.r>.6f&&material.color.g<.05f&&material.color.b<.05f,label+" is opaque red");
            Check(material.Numbers["_SrcBlend"]==1&&material.Numbers["_DstBlend"]==0&&material.Numbers["_ZWrite"]==1,label+" replaces destination and writes depth");
            Check(material.renderQueue==2000&&material.Tags["RenderType"]=="Opaque"&&!material.Keywords.Contains("_ALPHABLEND_ON"),label+" uses opaque rendering");
        }
        static void ClosedMesh(Mesh mesh,string label)
        {
            var edges=new System.Collections.Generic.Dictionary<string,int>();
            Func<Vector3,string> key=v=>((int)Math.Round(v.x*10000))+","+((int)Math.Round(v.y*10000))+","+((int)Math.Round(v.z*10000));
            for(int i=0;i<mesh.triangles.Length;i+=3)
            {
                var ids=new[]{mesh.triangles[i],mesh.triangles[i+1],mesh.triangles[i+2]};
                if(Vector3.Cross(mesh.vertices[ids[1]]-mesh.vertices[ids[0]],mesh.vertices[ids[2]]-mesh.vertices[ids[0]]).sqrMagnitude<1e-12f)continue;
                for(int j=0;j<3;j++){var a=key(mesh.vertices[ids[j]]);var b=key(mesh.vertices[ids[(j+1)%3]]);string edge=string.CompareOrdinal(a,b)<0?a+"|"+b:b+"|"+a;edges.TryGetValue(edge,out int n);edges[edge]=n+1;}
            }
            Check(edges.Count>0&&edges.Values.All(n=>n==2),label+" has no open boundary after welding seam vertices");
        }
        static void WaxRendering()
        {
            var p=Session();p.PlaybackBarPrimaryAction();AssertOpaque(p.Model.Wax,"candle body");
            p.Fire(1);var flying=Object.Objects.Last(o=>o.name=="Falling candle wax");var drop=flying.GetComponent<MeshFilter>().sharedMesh;
            AssertOpaque(flying.GetComponent<MeshRenderer>().sharedMaterial,"falling wax");
            Check(drop.colors.Length==drop.vertexCount&&drop.colors.All(c=>c.a==1&&c.r==1&&c.g==1&&c.b==1),"falling wax has explicit opaque white vertex tint");
            ClosedMesh(drop,"falling wax");Check(drop.vertices.Min(v=>v.y)<0&&drop.vertices.Max(v=>v.y)>0,"falling wax is a closed volume around its origin");
            p.FireVolume(.35f);var small=Object.Objects.Last(o=>o.name=="Falling candle wax").transform.localScale;
            p.FireVolume(1.65f);var large=Object.Objects.Last(o=>o.name=="Falling candle wax").transform.localScale;
            Near((large.x*large.y*large.z)/(small.x*small.y*small.z),1.65/.35,"flight mesh volume matches accumulated wax");
            var skin=Skin(out _,out _);int objects=Object.Objects.Count;
            var hit=SurfaceHit(skin);p.AddDeposit(hit,.35f);int smallInk=p.SkinInk;p.Erase();p.AddDeposit(hit,1.65f);
            Check(smallInk>0&&p.SkinInk>smallInk*2,"larger accumulated drop paints larger area on native UV");
            Check(Object.Objects.Count(o=>o.name=="Candle thin skin relief"&&o.activeInHierarchy)==1,"erasing old wax removes old relief and preserves one current skin cap");
            var cap=Object.Objects.Last(o=>o.name=="Candle thin skin relief");
            var relief=cap.GetComponent<SkinnedMeshRenderer>().sharedMesh;
            Near(relief.vertices[0].y,.00122,"new wax uses default thickness");
            int ink=p.SkinInk;p.AdjustThickness(3);
            Near(relief.vertices[0].y,.00302,"setting updates already attached wax");
            p.AdjustThickness(0);Check(!cap.activeInHierarchy&&p.SkinInk==ink,"flat setting preserves UV wax");
            p.AdjustThickness(2);Check(cap.activeInHierarchy,"setting restores existing relief");
            p.AddDeposit(hit);var added=Object.Objects.Last(o=>o.name=="Candle thin skin relief");
            Near(added.GetComponent<SkinnedMeshRenderer>().sharedMesh.vertices[0].y,.00202,"new deposits use selected thickness");
            GateApi.Allowed=false;p.AdjustThickness(4);
            Near(relief.vertices[0].y,.00202,"revoked permit rejects existing wax thickness edits");
            GateApi.Allowed=true;p.Stop();skin.Dispose();
        }
        static void DropOrigin()
        {
            var p=Session();p.PlaybackBarPrimaryAction();
            var body=Object.Objects.Last(o=>o.name=="Red wax body");
            var vertices=body.GetComponent<MeshFilter>().sharedMesh.vertices.Skip(5*33).Take(32).ToArray();
            foreach(float angle in new[]{46f,70f,90f,120f,134f})
            {
                p.Model.Root.transform.rotation=Quaternion.Euler(12,35,angle);
                p.Model.Root.transform.position=new Vector3(.2f,1.1f,-.3f);
                var rim=vertices.Select(v=>body.transform.TransformPoint(v)).ToArray();
                var origin=p.Model.WaxDropPosition;
                Check(rim.Any(v=>Vector3.Distance(v,origin)<1e-6),"drop originates on actual wax body rim "+angle);
                Near(origin.y,rim.Min(v=>v.y),"wax leaves lowest top rim edge "+angle);
                p.AdjustHeight(40);Near(Vector3.Distance(origin,p.Model.WaxDropPosition),0,"wick height cannot move wax origin "+angle);
                p.Fire(1);var drop=Object.Objects.Last(o=>o.name=="Falling candle wax");
                Near(Vector3.Distance(drop.transform.position,origin),0,"production spawn uses body rim "+angle);
                Check(Vector3.Distance(drop.transform.position,p.Model.Tip.position)>.02,"wax never originates at wick tip "+angle);
            }
            p.Stop();
        }
        static void Relief()
        {
            Session();var surface=Skin(out _,out var renderer);var hit=SurfaceHit(surface);
            var probe=new MainGameMagicPaint.MeshProbe(renderer);
            using(var patch=MainGameMagicPaint.SkinReliefPatch.Create(probe,hit.Point,hit.Normal,.006f,NativeMaterial()))
            {
                Check(patch!=null&&patch.Object.transform.parent==renderer.transform,"thin cap belongs to actual native skin renderer");
                var mesh=patch.Mesh;Check(mesh.vertexCount==37,"small concentric cap follows sampled skin");
                var offsets=mesh.vertices.Select((v,i)=>MainGameMagicPaint.SkinReliefPatch.WorldMatrix(mesh.boneWeights[i],renderer.bones,mesh.bindposes).MultiplyPoint3x4(v).y).ToArray();
                Check(offsets.Min()>=.000019&&offsets.Max()<=.001221,"cap vertices stay within selected thickness");
                Near(offsets[0],.00122,"default cap maximum thickness is 1.2 mm plus seam offset");
                patch.SetThickness(.003f);Near(mesh.vertices[0].y,.00302,"existing cap changes immediately to three mm");
                patch.SetThickness(0);Check(!patch.Object.activeInHierarchy,"zero thickness hides relief");
                patch.SetThickness(.0012f);Check(patch.Object.activeInHierarchy,"increasing thickness restores same cap");
                renderer.bones[0].position=new Vector3(.3f,.4f,.5f);renderer.bones[0].rotation=Quaternion.Euler(30,25,12);
                Vector3 n=renderer.bones[0].rotation*Vector3.up;
                for(int i=0;i<mesh.vertexCount;i++)
                {
                    var current=MainGameMagicPaint.SkinReliefPatch.WorldMatrix(mesh.boneWeights[i],renderer.bones,mesh.bindposes).MultiplyPoint3x4(mesh.vertices[i]);
                    Near(Vector3.Dot(current-renderer.bones[0].position,n),offsets[i],"native pose preserves cap distance from skin "+i);
                }
            }
            Check(!Object.Objects.Last(o=>o.name=="Candle thin skin relief").activeInHierarchy,"relief disposal removes cap");
            probe.Dispose();surface.Dispose();
        }
        static void Ignition()
        {
            Check(CandleFlow.CanIgnite(134.999f)&&!CandleFlow.CanIgnite(135)&&!CandleFlow.CanIgnite(180),"ignition boundary is strictly below 135 degrees");
            foreach(var hand in new[]{SteamVR_Input_Sources.LeftHand,SteamVR_Input_Sources.RightHand})
            {
                var p=Session(true);p.PlaybackBarPrimaryAction();var controller=hand==SteamVR_Input_Sources.LeftHand?VR.Mode.Left:VR.Mode.Right;controller.transform.position=p.Model.Root.transform.position;p.Grip(hand);
                var flame=Object.Objects.Last(o=>o.name=="Candle flame");var particles=flame.GetComponent<ParticleSystem>();
                Near(particles.main.startSize.constantMin,.006*2.5,"flame minimum size is half previous five times larger");Near(particles.main.startSize.constantMax,.009*2.5,"flame maximum size is half previous five times larger");
                Check(!particles.velocityOverLifetime.enabled&&particles.velocityOverLifetime.y==0&&particles.main.startSpeed.constantMax==0,"flame particles never drift away from wick");
                Check(particles.main.simulationSpace==ParticleSystemSimulationSpace.Local,"existing particles move with the wick instead of remaining below it");
                var wick=Object.Objects.Last(o=>o.name=="Black wick");var wickMesh=wick.GetComponent<MeshFilter>().sharedMesh;
                Near(Vector3.Distance(p.Model.Tip.position,wick.transform.TransformPoint(new Vector3(0,wickMesh.vertices.Max(v=>v.y),0))),0,"emitter is at wick tip");
                var body=Object.Objects.Last(o=>o.name=="Red wax body").GetComponent<MeshFilter>().sharedMesh;
                Check(p.Model.Tip.localPosition.y-body.vertices.Max(v=>v.y)>.006f,"wick and emission clear the highest wax rim by six mm");
                var flameRenderer=flame.GetComponent<ParticleSystemRenderer>();
                Check(flameRenderer.renderMode==ParticleSystemRenderMode.VerticalBillboard&&flameRenderer.pivot.sqrMagnitude==0,"upright centered billboard avoids downward velocity stretching");
                Check(flameRenderer.velocityScale==0&&flameRenderer.cameraVelocityScale==0,"camera and particle speeds cannot shift flame base");
                Check(particles.main.startSize3D,"flame uses explicit width and height");
                Near(particles.main.startSizeY.constantMin/2,(.006+.18*.065)*2.5,"anchored sprite preserves previous minimum upper extent");
                Near(particles.main.startSizeY.constantMax/2,(.009+.28*.065)*2.5,"anchored sprite preserves previous maximum upper extent");
                Check(!particles.shape.enabled,"point emission has no sphere inside candle");
                var sprite=flame.GetComponent<ParticleSystemRenderer>().sharedMaterial.mainTexture;
                Check(sprite.Pixels.Take(512).All(c=>c.a==0)&&sprite.Pixels.Skip(512).Any(c=>c.a>0),"visible flame starts above emitter");
                Vector3 originalPosition=p.Model.Root.transform.position;
                foreach(float tilt in new[]{0f,45f,90f,130f})
                {
                    p.Model.Root.transform.rotation=Quaternion.Euler(0,0,tilt);
                    p.Model.Root.transform.position+=new Vector3(.1f,.2f,-.1f);
                    Near(Vector3.Distance(flame.transform.position,p.Model.Tip.position),0,"particle origin remains at moving wick "+tilt);
                    Near(Vector3.Distance(flame.transform.position,wick.transform.TransformPoint(new Vector3(0,wickMesh.vertices.Max(v=>v.y),0))),0,"flame lower edge remains at physical wick "+tilt);
                }
                p.Model.Root.transform.position=originalPosition;
                p.Model.Root.transform.rotation=Quaternion.Euler(0,0,136);p.TickCandle(.01f);
                Check(!p.Burning&&!p.Loop&&p.Held&&!particles.isPlaying&&!flame.GetComponent<Light>().enabled,"deep tilt extinguishes flame and light without releasing the hand");
                p.Trigger(hand);Check(!p.Burning,"trigger while inverted cannot ignite");
                p.Model.Root.transform.rotation=Quaternion.Euler(0,0,134);
                Check(!p.Burning,"returning upright does not relight without a fresh trigger");
                p.Trigger(hand==SteamVR_Input_Sources.LeftHand?SteamVR_Input_Sources.RightHand:SteamVR_Input_Sources.LeftHand);Check(!p.Burning,"other hand cannot relight");
                p.Trigger(hand);Check(p.Burning&&p.Loop&&p.Held&&particles.isPlaying,"holding hand trigger relights below 135");
                p.Model.Root.transform.rotation=Quaternion.Euler(0,0,136);p.TickCandle(.01f);p.Up(hand);p.Trigger(hand);Check(!p.Burning,"released candle ignores trigger");
                p.Grip(hand);p.Model.Root.transform.rotation=Quaternion.identity;p.Trigger(hand);Check(p.Burning&&p.Held,"extinguished candle can be regrabbed and relit");
                p.Stop();
            }
        }
        static void Interaction()
        {
            var toggle=Session();toggle.PlaybackBarPrimaryAction();var candle=toggle.Model;
            candle.Root.transform.position=new Vector3(3,4,5);
            for(int i=0;i<12;i++)
            {
                toggle.PlaybackBarPrimaryAction();
                Check(candle.Root.GetComponent<MainGameTransformGizmo.TransformGizmo>().IsVisible==(i%2==0),"bar repeatedly toggles candle gizmo");
                Check(toggle.Model==candle&&candle.Root.transform.position.x==3&&candle.Root.transform.position.y==4,"bar never recalls or replaces positioned candle");
            }
            toggle.Stop();
            foreach(var hand in new[]{SteamVR_Input_Sources.LeftHand,SteamVR_Input_Sources.RightHand})
            {
                var p=Session(true);Check(p.Model==null&&!p.Loop,"start does not spawn");p.PlaybackBarPrimaryAction();Check(p.Model!=null&&p.Loop,"recall creates lit loop");
                var controller=hand==SteamVR_Input_Sources.LeftHand?VR.Mode.Left:VR.Mode.Right;
                controller.transform.position=p.Model.Root.transform.position;
                p.Model.Root.transform.rotation=Quaternion.Euler(35,22,15);Vector3 up=p.Model.Root.transform.up;
                p.Grip(hand);Check(p.Held,"grip "+hand);Check(Vector3.Distance(up,p.Model.Root.transform.up)<1e-5,"grab preserves angle");
                var lock1=controller.Current;p.Up(hand);Check(!p.Held&&lock1.IsValid&&lock1.IsInvalidating,"SafeRelease is deferred, not fake immediate release");
                p.Grip(hand);Check(p.Held&&!controller.Current.IsInvalidating&&!ReferenceEquals(lock1,controller.Current)&&!lock1.IsValid,"fresh lock on quick regrab");
                p.Up(hand);p.Lit(false);p.Grip(hand);Check(p.Held&&!p.Loop,"unlit candle can be regrabbed");p.Up(hand);
                p.PlaybackBarPrimaryAction();p.Grip(hand);Check(p.Held,"gizmo to grip");
                var other=hand==SteamVR_Input_Sources.LeftHand?SteamVR_Input_Sources.RightHand:SteamVR_Input_Sources.LeftHand;
                p.Up(other);Check(p.Held,"other hand cannot release");p.Grip(other);Check(p.Held,"other hand cannot steal");
                var ownLock=controller.Current;p.PlaybackBarDelete();Check(!p.Held&&p.Model==null&&!ownLock.IsValid,"held delete clears ownership and retirement");
                p.PlaybackBarDelete();p.PlaybackBarPrimaryAction();Check(p.Model!=null,"delete then recall");
                controller.transform.position=p.Model.Root.transform.position;p.Grip(hand);p.Focus(false);Check(!p.Held&&!p.Loop&&!controller.Current.IsValid,"focus loss releases and stops");
                p.Focus(true);p.Lit(true);Check(p.Loop,"focus resume restores lit loop");
                controller.transform.position=p.Model.Root.transform.position;p.Grip(hand);p.Stop();Check(!p.Held&&p.Model==null&&!controller.Current.IsValid,"H end releases held candle");p.Stop();
            }
            var denied=Session();GateApi.Allowed=false;denied.PlaybackBarPrimaryAction();Check(denied.Model==null,"invalid permit rejects spawn");GateApi.Allowed=true;denied.Stop();
            var fault=Session(true);fault.PlaybackBarPrimaryAction();VR.Mode.Left.transform.position=fault.Model.Root.transform.position;fault.Grip(SteamVR_Input_Sources.LeftHand);
            var focus=VR.Mode.Left.Current;fault.Model.Root.transform.ThrowParent=true;
            try{fault.Up(SteamVR_Input_Sources.LeftHand);}catch(InvalidOperationException){}
            Check(!fault.Held&&focus.IsInvalidating,"parent failure still releases own lock");fault.Model.Root.transform.ThrowParent=false;fault.Stop();Check(!focus.IsValid,"cleanup finishes own retiring lock");
            var blocked=Session(true);blocked.PlaybackBarPrimaryAction();VR.Mode.Left.transform.position=blocked.Model.Root.transform.position;VR.Mode.Left.TryAcquireFocus(out var unrelated);blocked.Grip(SteamVR_Input_Sources.LeftHand);Check(!blocked.Held&&unrelated.IsValid,"active unrelated lock is never stolen");blocked.Stop();Check(unrelated.IsValid,"cleanup does not release other owner");
        }
        static void Surface()
        {
            Session();var surface=Skin(out _,out var renderer);var hit=SurfaceHit(surface);int bakes=renderer.Bakes;
            surface.Ray(new Ray(new Vector3(0,1,0),Vector3.down),2,out _);Check(renderer.Bakes==bakes,"one bake per candidate per frame");
            using(var layer=new WaxTextureLayer(renderer))
            {
                var mark=layer.Stamp(surface,hit,.005f);Check(layer.InkPixels>0,"native UV receives wax");
                int ink=layer.InkPixels;renderer.bones[0].position=new Vector3(1,2,3);
                Check(layer.InkPixels==ink&&renderer.GetComponent<MeshFilter>()==null,"wax belongs to existing skinned renderer without separate geometry");
                layer.Remove(mark);Check(layer.InkPixels==0,"old mark removal rebuilds UV layer");
            }
            var directActor=renderer.GetComponentInParent<ChaControl>();renderer.transform.SetParent(null,true);
            using(var direct=new WaxSurface(renderer,directActor))
            using(var directLayer=new WaxTextureLayer(renderer,directActor))
            {
                var directHit=SurfaceHit(direct);directLayer.Stamp(direct,directHit,.005f);
                Check(directLayer.InkPixels>0,"native actor renderer reference works without hierarchy lookup");
            }
            renderer.enabled=false;Time.frameCount++;Check(!surface.Ray(new Ray(new Vector3(0,1,0),Vector3.down),2,out _),"hidden skin cannot receive wax");renderer.enabled=true;
            renderer.transform.localScale=new Vector3(2,3,4);renderer.transform.position=new Vector3(0,2,0);Time.frameCount++;
            Check(surface.Ray(new Ray(new Vector3(0,3,0),Vector3.down),2,out var scaled)&&Math.Abs(scaled.Point.y-2)<1e-4,"legacy BakeMesh scale applied once");surface.Dispose();
            var fallback=Skin(out _,out var nonreadable,false);var fallbackHit=SurfaceHit(fallback);
            using(var layer=new WaxTextureLayer(nonreadable)){layer.Stamp(fallback,fallbackHit,.005f);Check(layer.InkPixels>0,"baked UV supports nonreadable skin");}fallback.Dispose();
            var p=Session();p.PlaybackBarPrimaryAction();var body=Skin(out var owner,out _);p.Track(body);
            foreach(string proxyName in new[]{"o_shadowcaster","o_shadowcaster_cm"})
            {
                var proxy=new GameObject(proxyName);proxy.layer=11;proxy.transform.position=new Vector3(0,.2f,0);proxy.AddComponent<MeshFilter>().sharedMesh=Plane();
                var proxyRenderer=proxy.AddComponent<MeshRenderer>();proxyRenderer.name=proxyName;proxyRenderer.sharedMaterial=NativeMaterial();
                p.Track(new WaxSurface(proxyRenderer));Time.frameCount++;
                Check(p.Hit(new Ray(new Vector3(0,1,0),Vector3.down),2,out var belly)&&ReferenceEquals(belly.Surface,body),"invisible native shadow shell cannot intercept belly wax "+proxyName);
                p.AddDeposit(belly);Check(p.SkinInk>0&&belly.Surface.IsSkin,"belly contact paints native skin below shadow shell");
            }
            var cloth=new GameObject("clothing");cloth.transform.SetParent(owner.transform,false);cloth.transform.position=new Vector3(0,.3f,0);cloth.AddComponent<MeshFilter>().sharedMesh=Plane();var clothRenderer=cloth.AddComponent<MeshRenderer>();clothRenderer.sharedMaterial=NativeMaterial();var clothSurface=new WaxSurface(clothRenderer);p.Track(clothSurface);Time.frameCount++;
            Check(p.Hit(new Ray(new Vector3(0,1,0),Vector3.down),2,out var first)&&ReferenceEquals(first.Surface,body),"clothing is ignored and wax reaches skin");
            Check(first.Surface.IsSkin,"contact is real skin");p.AddDeposit(first);Check(p.SkinInk>0,"native skin below clothes receives wax");
            int deposits=p.Deposits;p.AddDeposit(new WaxHit{Surface=clothSurface,Point=first.Point,Normal=first.Normal});Check(p.Deposits==deposits,"direct product entry rejects clothes");p.Stop();
            var deleted=Skin(out _,out var disappearing);SurfaceHit(deleted);disappearing.sharedMesh=null;Time.frameCount++;
            Check(!deleted.Ray(new Ray(new Vector3(0,1,0),Vector3.down),2,out _),"destroyed mesh rejected");Check(!deleted.Ray(new Ray(new Vector3(0,1,0),Vector3.down),2,out _),"destroyed mesh cannot reuse stale hit in same frame");deleted.Dispose();
        }
        static void WaxLifetime()
        {
            var p=Session();p.PlaybackBarPrimaryAction();p.Fire(140);Check(p.Flying==128,"flight capacity bounded");p.Lit(false);Check(p.Flying==0&&!p.Loop,"extinguish clears flight and loop");p.Lit(true);
            var target=Skin(out var tableActor,out _);p.Voice(false);p.Track(target);var hit=SurfaceHit(target);
            for(int i=0;i<520;i++)p.AddDeposit(hit);Check(p.Deposits==512,"deposit capacity bounded");
            p.FinishFaceNow(tableActor);
            p.PlaybackBarDelete();Check(p.Deposits==512&&p.Flying==0&&p.SkinInk>0,"delete preserves deposited wax only");
            Check(Object.Objects.Count(o=>o.name=="Candle thin skin relief"&&o.activeInHierarchy)==512,"capacity eviction removes old caps while candle deletion preserves attached relief");
            p.PlaybackBarPrimaryAction();p.Erase();Check(p.Deposits==0&&p.Model!=null&&p.SkinInk==0,"manual erase preserves candle");
            Check(!Object.Objects.Any(o=>o.name=="Candle thin skin relief"&&o.activeInHierarchy),"erase removes all relief geometry");
            target.Renderer.enabled=false;p.Fire(1);p.Fly(3.1f);Check(p.Flying==0,"missed drops expire");
            var skin=Skin(out var actor,out _);p.Track(skin);p.Voice(false);var skinHit=SurfaceHit(skin);p.AddDeposit(skinHit);Check(p.Faces==1,"skin deposit reacts");
            int reactions=p.Reactions;actor.rendBody=null;var clothing=new WaxSurface(skin.Renderer);p.AddDeposit(new WaxHit{Surface=clothing,Point=skinHit.Point,Normal=skinHit.Normal});Check(p.Reactions==reactions,"clothing does not add a skin reaction");
            p.Stop();Check(p.Deposits==0&&p.Flying==0&&p.Faces==0,"H end clears wax, flights, and reactions");
            Check(!Object.Objects.Any(o=>o.name=="Candle thin skin relief"&&o.activeInHierarchy),"H end removes attached relief geometry");
        }
        static void Reaction()
        {
            var p=Session();p.PlaybackBarPrimaryAction();p.Voice(false);var actor=new GameObject("reactor").AddComponent<ChaControl>();actor.Eyes=5;actor.Brows=6;actor.Mouth=7;
            Time.unscaledTime=10;p.ReactTo(actor);Check(p.Faces==1&&actor.Eyes==1&&actor.Mouth==2,"reaction applies to actual actor");p.FinishFaceNow(actor);Check(actor.Eyes==5&&actor.Brows==6&&actor.Mouth==7&&p.Faces==0,"reaction restores snapshots");
            Time.unscaledTime=11;p.ReactTo(actor);Check(p.Faces==0,"per actor two second cooldown");Time.unscaledTime=12;p.ReactTo(actor);Check(p.Faces==1,"cooldown expires without pending queue");
            actor.Eyes=9;p.Focus(false);Check(actor.Eyes==9&&actor.Brows==6&&p.Faces==0,"cleanup preserves later external expression");p.Stop();
        }
        static void Bridge()
        {
            var p=new MainGameVoiceFaceEventBridge.Plugin();var first=new GameObject("first actor").AddComponent<ChaControl>();var second=new GameObject("second actor").AddComponent<ChaControl>();var stranger=new GameObject("stranger").AddComponent<ChaControl>();
            MainGameVoiceFaceEventBridge.SessionGate.Females.Clear();MainGameVoiceFaceEventBridge.SessionGate.Females.AddRange(new[]{first,second});MainGameVoiceFaceEventBridge.SessionGate.Allowed=true;GateApi.Allowed=true;
            first.fileParam.personality=0;second.fileParam.personality=8;
            var clip00=new AudioClip{name="native c00 moan",length=3.8f};var clip08=new AudioClip{name="native c08 moan",length=2.6f};
            AssetBundleData.Assets.Clear();
            AssetBundleData.Assets["sound/c00/h|native00"]=System.Threading.Tasks.Task.FromResult(clip00);
            AssetBundleData.Assets["sound/c08/h|native08"]=System.Threading.Tasks.Task.FromResult(clip08);
            AssetBundleData.Assets["sound/c08/h|too-long"]=System.Threading.Tasks.Task.FromResult(new AudioClip{length=4.1f});
            MainGameVoiceFaceEventBridge.SessionGate.Voice=new HVoiceCtrl{dicShortBreathIntos=new[]{
                new System.Collections.Generic.Dictionary<int,System.Collections.Generic.Dictionary<int,NativeBreathInfo>>{{0,new System.Collections.Generic.Dictionary<int,NativeBreathInfo>{{0,new NativeBreathInfo{pathAsset="sound/c00/h",nameFile="native00",word="あぁっ……"}}}}},
                new System.Collections.Generic.Dictionary<int,System.Collections.Generic.Dictionary<int,NativeBreathInfo>>{{0,new System.Collections.Generic.Dictionary<int,NativeBreathInfo>{{0,new NativeBreathInfo{pathAsset="sound/c08/h",nameFile="native08",word="んっ……"}},{1,new NativeBreathInfo{pathAsset="sound/c08/h",nameFile="too-long",word="ああっ"}},{2,new NativeBreathInfo{pathAsset="sound/c08/h",nameFile="talk",word="気持ちいいわ"}}}}}
            }};
            p.LoadContacts();
            Check(p.RequestActorImpactCue(Plugin.Guid,second)&&ReferenceEquals(p._externalVoicePlayer.Target,second)&&p._externalVoicePlayer.Explicit,"bridge forwards touched actor, not female zero");
            Check(p._externalVoicePlayer.Clip==clip08,"personality eight selects its native clip under four seconds");
            Check(p.RequestActorImpactCue(Plugin.Guid,first)&&p._externalVoicePlayer.Clip==clip00,"personality zero independently selects 3.8 second native moan");
            Check(!p.RequestActorImpactCue(Plugin.Guid,stranger),"bridge rejects nonparticipant");GateApi.Allowed=false;Check(!p.RequestActorImpactCue(Plugin.Guid,second),"bridge rejects expired owner permit");GateApi.Allowed=true;
            p.SyringeDialogueActive=true;Check(!p.RequestActorImpactCue(Plugin.Guid,second),"syringe dialogue remains protected");
            var firstMouth=new GameObject("first mouth").transform;var secondMouth=new GameObject("second mouth").transform;
            MainGameVoiceFaceEventBridge.SessionGate.Flags=new HFlag{transVoiceMouth=new[]{firstMouth,secondMouth}};
            Manager.Voice.Stopped.Clear();Check(!p.PrepareContact(second)&&Manager.Voice.Stopped.Count==0,"late syringe dialogue protects native voice at playback start");
            p.SyringeDialogueActive=false;Check(p.PrepareContact(second),"contact voice can start for touched participant");
            Check(Manager.Voice.Stopped.SequenceEqual(new[]{secondMouth,second.transform}),"only touched actor's native voice is replaced");
            Check(!p.PrepareContact(stranger),"playback start rejects nonparticipant");
            foreach(bool contact in new[]{false,true})foreach(bool thought in new[]{false,true})foreach(bool native in new[]{false,true})
                Check(ContactCuePolicy.RejectNativeVoice(contact,thought,native)==(!contact&&!thought&&native),"contact admission bypasses native busy only for contact or thought");
            Check(ContactCuePolicy.IsShortMoan(1.623)&&ContactCuePolicy.IsShortMoan(3.999)&&ContactCuePolicy.IsShortMoan(4)&&!ContactCuePolicy.IsShortMoan(4.001)&&!ContactCuePolicy.IsShortMoan(double.NaN),"contact moans restricted to four seconds including boundary");
            Check(ContactCuePolicy.IsMoanText("あぁっ、んっ……")&&!ContactCuePolicy.IsMoanText("気持ちいいわ"),"native breath classification excludes ordinary voiced sentences");
            p.ClearContacts();Check(p.PoolCount==0,"H end clears personality clip caches");
            Check(ReferenceEquals(ImpactCueTarget.Choose(true,true,first,second),second),"contact cue does not switch to current speaker");Check(ReferenceEquals(ImpactCueTarget.Choose(false,true,first,second),first),"legacy cue target preserved");
        }
        static void ContactLoading()
        {
            GateApi.Allowed=true;MainGameVoiceFaceEventBridge.SessionGate.Allowed=true;
            var actor=new GameObject("async actor").AddComponent<ChaControl>();
            MainGameVoiceFaceEventBridge.SessionGate.Females.Clear();MainGameVoiceFaceEventBridge.SessionGate.Females.Add(actor);
            var data=new NativeBreathInfo{pathAsset="sound/c{0:00}/h",nameFile="moan",word="んーーっ……"};
            MainGameVoiceFaceEventBridge.SessionGate.Voice=new HVoiceCtrl{dicShortBreathIntos=new[]{new System.Collections.Generic.Dictionary<int,System.Collections.Generic.Dictionary<int,NativeBreathInfo>>{{0,new System.Collections.Generic.Dictionary<int,NativeBreathInfo>{{0,data}}}}}};
            var old=new System.Threading.Tasks.TaskCompletionSource<AudioClip>();
            AssetBundleData.Assets.Clear();AssetBundleData.Assets["sound/c00/h|moan"]=old.Task;
            var p=new MainGameVoiceFaceEventBridge.Plugin();p.LoadContacts();
            Check(!p.RequestActorImpactCue(Plugin.Guid,actor)&&p._externalVoicePlayer.Calls==0,"contact while loading is discarded, never queued");
            p.ClearContacts();MainGameVoiceFaceEventBridge.SessionGate.Allowed=false;
            old.SetResult(new AudioClip{name="old c00",length=3});
            Check(p.PoolCount==0&&p._externalVoicePlayer.Calls==0,"late H completion does not restore pool or play voice");
            MainGameVoiceFaceEventBridge.SessionGate.Allowed=true;
            var pending=new System.Threading.Tasks.TaskCompletionSource<AudioClip>();AssetBundleData.Assets["sound/c00/h|moan"]=pending.Task;
            p.LoadContacts();actor.fileParam.personality=8;
            var next=new AudioClip{name="c08",length=3.9f};var other=new AudioClip{name="c08 second",length=2.5f};
            AssetBundleData.Assets["sound/c08/h|moan"]=System.Threading.Tasks.Task.FromResult(next);
            AssetBundleData.Assets["sound/c08/h|moan2"]=System.Threading.Tasks.Task.FromResult(other);
            MainGameVoiceFaceEventBridge.SessionGate.Voice.dicShortBreathIntos[0][0].Add(1,new NativeBreathInfo{pathAsset="sound/c{0:00}/h",nameFile="moan2",word="あぁっ"});
            Check(p.RequestActorImpactCue(Plugin.Guid,actor),"personality change loads and selects new native voice pool");
            pending.SetResult(new AudioClip{name="stale c00",length=1});
            AudioClip last=null;
            for(int i=0;i<10;i++)
            {
                Check(p.RequestActorImpactCue(Plugin.Guid,actor),"native contact pool remains playable");
                var current=p._externalVoicePlayer.Clip;
                Check((current==next||current==other)&&current!=last,"new personality only and no immediate repeat");last=current;
            }
            p.ClearContacts();MainGameVoiceFaceEventBridge.SessionGate.Voice=null;
            var scene=new GameObject("late H setup").AddComponent<TestNativeHScene>();
            scene.lstFemale=new System.Collections.Generic.List<ChaControl>();MainGameVoiceFaceEventBridge.SessionGate.Scene=scene;
            MainGameVoiceFaceEventBridge.SessionGate.Females.Clear();
            Check(!p.RequestActorImpactCue(Plugin.Guid,actor),"empty native H initialization rejects contact without poisoning later requests");
            scene.lstFemale.Add(actor);scene.voice=new HVoiceCtrl{dicShortBreathIntos=new[]{new System.Collections.Generic.Dictionary<int,System.Collections.Generic.Dictionary<int,NativeBreathInfo>>{{0,new System.Collections.Generic.Dictionary<int,NativeBreathInfo>{{0,data}}}}}};scene.flags=new HFlag();
            Check(p.RequestActorImpactCue(Plugin.Guid,actor)&&p._externalVoicePlayer.Clip==next,"real contact refreshes late native actor and voice fields");
            p.ClearContacts();scene.voice=null;
            CommonLib.Assets["h/list/01.unity3d|personality_shortbreath_c08_00"]=new ShortBreathData{param={new ShortBreathData.Row{infos=new[]{data}}}};
            Check(p.RequestActorImpactCue(Plugin.Guid,actor)&&p._externalVoicePlayer.Clip==next,"raw native personality table works without H voice controller");
            CommonLib.Assets.Clear();
            MainGameVoiceFaceEventBridge.SessionGate.Scene=null;p.ClearContacts();
        }
        static void NativeVoiceTable()
        {
            // The real c00 table stores these reactions under voice, not breath/shortbreath.
            GateApi.Allowed=true;MainGameVoiceFaceEventBridge.SessionGate.Allowed=true;
            var actor=new GameObject("native c00 actor").AddComponent<ChaControl>();
            var scene=new GameObject("native c00 scene").AddComponent<TestNativeHScene>();
            scene.lstFemale=new System.Collections.Generic.List<ChaControl>{actor};
            MainGameVoiceFaceEventBridge.SessionGate.Scene=scene;
            string bundle="sound/data/pcm/c00/h/01.unity3d";
            var a=new NativeBreathInfo{pathAsset=bundle,nameFile="h_hh_00_00_086",word="やんっ……"};
            var b=new NativeBreathInfo{pathAsset=bundle,nameFile="h_hh_00_00_087",word="あんっ……"};
            var sentence=new NativeBreathInfo{pathAsset=bundle,nameFile="ordinary-dialogue",word="気持ちいいわ"};
            var longMoan=new NativeBreathInfo{pathAsset=bundle,nameFile="long-reaction",word="あぁっ……"};
            var climax=new NativeBreathInfo{pathAsset=bundle,nameFile="h_on_00_02_013",word="いっくぅぅ！"};
            var climax2=new NativeBreathInfo{pathAsset=bundle,nameFile="h_on_00_03_013",word="イックぅぅ！！"};
            var first=new AudioClip{name=a.nameFile,length=1.623f};var second=new AudioClip{name=b.nameFile,length=1.678f};
            AssetBundleData.Assets.Clear();AssetBundleData.Assets[bundle+"|"+a.nameFile]=System.Threading.Tasks.Task.FromResult(first);
            AssetBundleData.Assets[bundle+"|"+b.nameFile]=System.Threading.Tasks.Task.FromResult(second);
            AssetBundleData.Assets[bundle+"|"+sentence.nameFile]=System.Threading.Tasks.Task.FromResult(new AudioClip{length=1});
            AssetBundleData.Assets[bundle+"|"+longMoan.nameFile]=System.Threading.Tasks.Task.FromResult(new AudioClip{length=4.01f});
            AssetBundleData.Assets[bundle+"|"+climax.nameFile]=System.Threading.Tasks.Task.FromResult(new AudioClip{length=3.483f});
            AssetBundleData.Assets[bundle+"|"+climax2.nameFile]=System.Threading.Tasks.Task.FromResult(new AudioClip{length=3.936f});
            foreach(string name in new[]{climax.nameFile,climax2.nameFile,"h_on_08_02_013","h_on_39_03_013"})Check(!ContactCuePolicy.IsContactAsset(name),"climax family excluded independently of personality and subtitle");
            var group=new System.Collections.Generic.Dictionary<int,System.Collections.Generic.Dictionary<int,NativeBreathInfo>>{{0,new System.Collections.Generic.Dictionary<int,NativeBreathInfo>{{0,a},{1,b},{2,sentence},{3,longMoan},{4,climax},{5,climax2}}}};
            scene.voice=new HVoiceCtrl{dicVoiceIntos=new[]{new System.Collections.Generic.Dictionary<int,System.Collections.Generic.Dictionary<int,System.Collections.Generic.Dictionary<int,NativeBreathInfo>>>{{0,group}}}};
            var p=new MainGameVoiceFaceEventBridge.Plugin();p.LoadContacts();
            for(int i=0;i<6;i++)Check(p.RequestActorImpactCue(Plugin.Guid,actor)&&(p._externalVoicePlayer.Clip==first||p._externalVoicePlayer.Clip==second),"native ordinary voice table yields short nonverbal contact reaction");
            p.ClearContacts();scene.voice=null;CommonLib.Assets.Clear();
            CommonLib.Assets["h/list/01.unity3d|personality_voice_c00"]=new VoiceAllData{param={new VoiceAllData.Mode{data={new VoiceAllData.Row{info=new[]{a,b,sentence,longMoan,climax,climax2}}}}}};
            Check(p.RequestActorImpactCue(Plugin.Guid,actor)&&(p._externalVoicePlayer.Clip==first||p._externalVoicePlayer.Clip==second),"raw native ordinary voice table works with no native voice controller");
            p.ClearContacts();CommonLib.Assets.Clear();MainGameVoiceFaceEventBridge.SessionGate.Scene=null;
        }
        static void Layout()
        {
            foreach(float width in new[]{640f,800f,980f,1280f,1920f})foreach(bool vmd in new[]{false,true})
            {
                var flow=new MainGameBlankMapAdd.PlaybackButtonFlow(width);for(int i=0;i<10;i++)flow.Take(100);var itemGroup=flow.Take(MainGameBlankMapAdd.PlaybackButtonFlow.PhoneMagicCandleWidth);
                var candle=new MainGameBlankMapAdd.PlaybackButtonFlow.Slot{X=itemGroup.X+140,Y=itemGroup.Y,Width=86};
                Check(candle.Y==itemGroup.Y&&candle.X+candle.Width==itemGroup.X+itemGroup.Width,"iPhone, magic and candle stay in the same item row");
                Check(candle.X>=0&&candle.X+candle.Width<=width-6,"candle fits wrapped transport "+width+" "+vmd);
                Check(MainGameBlankMapAdd.PlaybackButtonFlow.ListOffset(width,56,true,true,vmd)>=26,"measured rows include candle");
            }
        }
    }
}
