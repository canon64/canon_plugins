using System;
using System.Collections.Generic;
namespace UnityEngine
{
    public struct Rect{public Rect(float x,float y,float width,float height){}}
    public static class PaintBoundary
    {
        public static void GetVertices(this Mesh mesh,List<Vector3> output){output.Clear();output.AddRange(mesh.vertices);}
        public static void Apply(this Texture2D texture,bool mipmaps){texture.Apply(mipmaps,false);}
        public static void ReadPixels(this Texture2D texture,Rect region,int x,int y){throw new NotSupportedException("Native diagnostic capture is not simulated.");}
        public static byte[] EncodeToPNG(this Texture2D texture){throw new NotSupportedException("Native diagnostic capture is not simulated.");}
    }
}
