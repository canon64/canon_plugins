using System;
using MainGameMagicPaint;
using UnityEngine;
namespace MainGameCandle
{
    internal sealed class WaxMark
    {
        internal WaxTextureLayer Layer;
        internal NativeSkinMark Native;
    }
    internal sealed class WaxTextureLayer:IDisposable
    {
        readonly NativeSkinLayer _native;
        internal int InkPixels=>_native.InkPixels;
        internal WaxTextureLayer(Renderer renderer,ChaControl actor=null){_native=new NativeSkinLayer(Plugin.Guid,actor??renderer.GetComponentInParent<ChaControl>(),renderer);}
        internal WaxMark Stamp(WaxSurface surface,WaxHit hit,float radius)=>new WaxMark{Layer=this,Native=_native.Stamp(hit.Point,hit.Normal,radius)};
        internal void Remove(WaxMark mark)=>_native.Remove(mark.Native);
        internal void SetThickness(float meters)=>_native.SetReliefThickness(meters);
        public void Dispose()=>_native.Dispose();
    }
}
