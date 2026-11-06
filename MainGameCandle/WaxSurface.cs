using System;
using MainGameMagicPaint;
using UnityEngine;
namespace MainGameCandle
{
    internal sealed class WaxSurface:IDisposable
    {
        readonly NativeSkinContact _contact;
        internal Renderer Renderer=>_contact.Renderer;
        internal ChaControl Actor=>_contact.Actor;
        internal bool IsSkin=>_contact.IsSkin;
        internal WaxSurface(Renderer renderer,ChaControl actor=null){_contact=new NativeSkinContact(actor??renderer.GetComponentInParent<ChaControl>(),renderer);}
        internal bool Ray(Ray ray,float limit,out WaxHit hit)
        {
            hit=default;
            if(!_contact.Ray(ray,limit,out var point,out var normal,out var distance))return false;
            hit=new WaxHit{Surface=this,Point=point,Normal=normal,Distance=distance};return true;
        }
        public void Dispose()=>_contact.Dispose();
    }
    internal struct WaxHit
    {
        internal WaxSurface Surface;
        internal Vector3 Point,Normal;
        internal float Distance;
    }
}
