using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MainGameCandle
{
    internal sealed class CandleModel : IDisposable
    {
        internal const float FlameScale = 2.5f;
        internal const float WickBase = .096f, WickLength = .012f;
        internal GameObject Root { get; private set; }
        internal Transform Tip { get; private set; }
        internal Material Wax { get; private set; }
        readonly List<UnityEngine.Object> _owned = new List<UnityEngine.Object>();
        readonly Vector3[] _waxRim = new Vector3[32];
        internal Vector3 WaxDropPosition
        {
            get
            {
                var lowest=Root.transform.TransformPoint(_waxRim[0]);
                for(int i=1;i<_waxRim.Length;i++)
                {
                    var point=Root.transform.TransformPoint(_waxRim[i]);
                    if(point.y<lowest.y)lowest=point;
                }
                return lowest;
            }
        }
        ParticleSystem _flame;
        Light _light;
        Transform _wick;
        internal CandleModel(Transform parent)
        {
            Root = new GameObject("Candle"); Root.transform.SetParent(parent, false);
            try
            {
                var standard = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Hidden/Internal-Colored");
                if (standard == null) throw new InvalidOperationException("本体用の描画材質がありません");
                Wax = new Material(standard) { name = "Candle crimson wax" }; _owned.Add(Wax);
                WaxMaterial.MakeOpaque(Wax,WaxMaterial.Red);
                var wick = new Material(standard) { name = "Charred wick" }; _owned.Add(wick);
                WaxMaterial.MakeOpaque(wick,new Color(.035f,.022f,.012f,1));
                var body = Lathe(new[] { -.100f, -.100f, -.098f, .085f, .096f, .100f, .096f }, new[] { 0f, .007f, .009f, .0084f, .009f, .0075f, 0f }, true);
                // Ring five is the actual melted top edge; keep its generated uneven surface.
                Array.Copy(body.vertices,5*33,_waxRim,0,_waxRim.Length);
                Part("Red wax body", body, Wax, Vector3.zero, Vector3.one);
                Part("Black wick", Lathe(new[] { 0f, WickLength }, new[] { .00065f, .0004f }, false), wick, new Vector3(0, WickBase, 0), Vector3.one);
                // Irregular cooled wax trails on the candle itself.
                for (int i = 0; i < 5; i++)
                {
                    float a = i * 2.39996f, y = .084f - (i % 3) * .009f;
                    Part("Melted wax trail", Bead(), Wax, new Vector3(Mathf.Cos(a)*.0083f, y, Mathf.Sin(a)*.0083f), new Vector3(.002f, .009f + i*.0015f, .002f));
                }
                Tip = new GameObject("Wick tip").transform; Tip.SetParent(Root.transform, false); Tip.localPosition = new Vector3(0, WickBase+WickLength, 0);
                SetWickLength(.018f);
                var flameObject = new GameObject("Candle flame"); flameObject.transform.SetParent(Tip, false);
                _flame = flameObject.AddComponent<ParticleSystem>(); _flame.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = _flame.main; main.playOnAwake = false; main.loop = true; main.startLifetime = new ParticleSystem.MinMaxCurve(.18f,.28f);
                main.startSpeed = 0; main.startSize = new ParticleSystem.MinMaxCurve(.006f*FlameScale,.009f*FlameScale); main.maxParticles = 32; main.simulationSpace = ParticleSystemSimulationSpace.Local;
                // All particles stay at the wick. Their upright sprites provide the flame
                // height, so moving/rotating the candle cannot leave an old plume below it.
                // Preserve the old upper extent (rise during lifetime + sprite height).
                main.startSize3D=true;
                main.startSizeX=new ParticleSystem.MinMaxCurve(.006f*FlameScale,.009f*FlameScale);
                main.startSizeY=new ParticleSystem.MinMaxCurve(2*FlameScale*(.006f+.18f*.065f),2*FlameScale*(.009f+.28f*.065f));
                main.startSizeZ=1;
                main.startRotation3D=false;main.startRotation=0;
                var emission = _flame.emission; emission.rateOverTime = 55;
                var shape = _flame.shape; shape.enabled=false;
                var velocity = _flame.velocityOverLifetime; velocity.enabled = false; velocity.space = ParticleSystemSimulationSpace.World; velocity.x=0;velocity.y=0;velocity.z=0;
                var size = _flame.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0,.45f),new Keyframe(.3f,1),new Keyframe(1,0)));
                var color = _flame.colorOverLifetime; color.enabled = true;
                var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(new Color(.25f,.35f,1),0),new GradientColorKey(new Color(1,.78f,.22f),.2f),new GradientColorKey(new Color(1,.22f,.015f),1) },new[] {new GradientAlphaKey(0,0),new GradientAlphaKey(.9f,.12f),new GradientAlphaKey(0,1)}); color.color = gradient;
                // KKS can strip the dedicated built-in particle shaders. Keep the candle
                // renderable with the same sprite/colored shaders used by the installed gizmos.
                var shader = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Particles/Additive") ?? Shader.Find("Legacy Shaders/Particles/Additive") ?? Shader.Find("Sprites/Default") ?? standard;
                var texture = new Texture2D(32,32,TextureFormat.RGBA32,false) { name = "Candle soft flame" }; _owned.Add(texture);
                var pixels = new Color[1024];
                for(int y=0;y<32;y++)for(int x=0;x<32;x++)
                {
                    float height=(y-15.5f)/15.5f,alpha=0;
                    if(height>0){float width=.8f*(1-height)+.06f;float edge=Mathf.Clamp01(1-Mathf.Abs((x-15.5f)/15.5f)/width);alpha=edge*edge*Mathf.Sin(height*Mathf.PI);}
                    pixels[y*32+x]=new Color(1,1,1,alpha);
                }
                texture.SetPixels(pixels); texture.Apply(false,true);
                var flameMaterial = new Material(shader) { mainTexture = texture }; _owned.Add(flameMaterial);
                if (flameMaterial.HasProperty("_Color")) flameMaterial.color=new Color(1,.65f,.15f);
                if (flameMaterial.HasProperty("_Mode")) flameMaterial.SetFloat("_Mode",2);
                if (flameMaterial.HasProperty("_SrcBlend")) flameMaterial.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);
                if (flameMaterial.HasProperty("_DstBlend")) flameMaterial.SetInt("_DstBlend",(int)BlendMode.One);
                if (flameMaterial.HasProperty("_ZWrite")) flameMaterial.SetInt("_ZWrite",0);
                flameMaterial.DisableKeyword("_ALPHATEST_ON");flameMaterial.EnableKeyword("_ALPHABLEND_ON");flameMaterial.renderQueue=3000;
                var renderer = _flame.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = flameMaterial;
                renderer.renderMode=ParticleSystemRenderMode.VerticalBillboard;
                renderer.pivot=Vector3.zero;
                renderer.lengthScale=1;renderer.velocityScale=0;renderer.cameraVelocityScale=0;
                _light = flameObject.AddComponent<Light>(); _light.type = LightType.Point; _light.color = new Color(1,.52f,.17f); _light.intensity = .65f; _light.range = 1.1f; _light.shadows = LightShadows.None;
                SetLit(false);
            }
            catch { Dispose(); throw; }
        }
        void Part(string name, Mesh mesh, Material mat, Vector3 position, Vector3 scale)
        {
            _owned.Add(mesh); var o = new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer)); o.transform.SetParent(Root.transform,false); o.transform.localPosition=position;o.transform.localScale=scale;
            o.GetComponent<MeshFilter>().sharedMesh=mesh;o.GetComponent<MeshRenderer>().sharedMaterial=mat;
            if(name=="Black wick")_wick=o.transform;
        }
        internal void SetWickLength(float length)
        {
            length=Math.Max(.006f,Mathf.Min(.040f,length));
            _wick.localScale=new Vector3(1,length/WickLength,1);
            Tip.localPosition=new Vector3(0,WickBase+length,0);
        }
        internal void SetLit(bool value)
        {
            if (_light != null) _light.enabled = value;
            if (_flame == null) return;
            if(value) { if(!_flame.isPlaying) _flame.Play(); } else _flame.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        static Mesh Lathe(float[] heights,float[] radii,bool uneven)
        {
            const int n=32;var v=new Vector3[heights.Length*(n+1)];var t=new List<int>();
            for(int ring=0;ring<heights.Length;ring++)for(int j=0;j<=n;j++)
            {
                float a=j*Mathf.PI*2/n;float y=heights[ring]+(uneven&&heights[ring]>.09f&&radii[ring]>0?Mathf.Sin(a*3+.4f)*.0012f:0);
                int k=ring*(n+1)+j;v[k]=new Vector3(Mathf.Cos(a)*radii[ring],y,Mathf.Sin(a)*radii[ring]);
                if(ring>0&&j>0){int b=k-n-1;t.AddRange(new[]{b-1,k-1,b,b,k-1,k});}
            }
            // Hidden/Internal-Colored and sprite fallbacks multiply the material tint by
            // vertex colors. Supply opaque white explicitly for every generated vertex.
            var colors=new Color[v.Length];for(int i=0;i<colors.Length;i++)colors[i]=Color.white;
            var mesh=new Mesh{name="Candle lathe"};mesh.vertices=v;mesh.colors=colors;mesh.triangles=t.ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        // Unit bead, with a flat rim and a raised irregular dome. Caller chooses dimensions.
        internal static Mesh Bead()
        {
            var heights=new[]{0f,0f,.15f,.55f,.86f,1f};var radii=new[]{0f,1f,.98f,.82f,.5f,0f};return Lathe(heights,radii,false);
        }
        internal static Mesh Drop()=>Lathe(new[]{-1f,-.7f,0f,.65f,1f},new[]{0f,.65f,1f,.7f,0f},false);
        public void Dispose()
        {
            if(Root!=null) { Root.SetActive(false); UnityEngine.Object.Destroy(Root); } Root=null;
            foreach(var o in _owned) if(o!=null) UnityEngine.Object.Destroy(o);_owned.Clear();
        }
    }
}
