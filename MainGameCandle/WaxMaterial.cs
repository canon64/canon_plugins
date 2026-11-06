using UnityEngine;
using UnityEngine.Rendering;

namespace MainGameCandle
{
    internal static class WaxMaterial
    {
        internal static readonly Color Red = new Color(.72f,.018f,.028f,1f);
        internal static void MakeOpaque(Material material, Color color)
        {
            color.a=1;
            material.color=color;
            if(material.HasProperty("_Color"))material.SetColor("_Color",color);
            if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);
            if(material.HasProperty("_Mode"))material.SetFloat("_Mode",0);
            if(material.HasProperty("_Surface"))material.SetFloat("_Surface",0);
            if(material.HasProperty("_SrcBlend"))material.SetInt("_SrcBlend",(int)BlendMode.One);
            if(material.HasProperty("_DstBlend"))material.SetInt("_DstBlend",(int)BlendMode.Zero);
            if(material.HasProperty("_ZWrite"))material.SetInt("_ZWrite",1);
            if(material.HasProperty("_ZTest"))material.SetInt("_ZTest",(int)CompareFunction.LessEqual);
            if(material.HasProperty("_Cull"))material.SetInt("_Cull",(int)CullMode.Back);
            if(material.HasProperty("_Metallic"))material.SetFloat("_Metallic",0);
            if(material.HasProperty("_Glossiness"))material.SetFloat("_Glossiness",.3f);
            material.DisableKeyword("_ALPHATEST_ON");material.DisableKeyword("_ALPHABLEND_ON");material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.SetOverrideTag("RenderType","Opaque");material.renderQueue=(int)RenderQueue.Geometry;
        }
    }
}
