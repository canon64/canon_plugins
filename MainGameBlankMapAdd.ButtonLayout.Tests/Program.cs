using System;
using System.Collections.Generic;
using MainGameBlankMapAdd;
static class Program
{
    static int checks;
    static void Check(bool condition,string text){checks++;if(!condition)throw new Exception(text);}
    static void Main()
    {
        foreach(float width in new[]{320f,480f,640f,760f,932f,980f,1034f,1270f,1400f,1874f,2514f})
        foreach(bool folder in new[]{false,true}) foreach(bool handy in new[]{false,true}) foreach(bool vmd in new[]{false,true})
        {
            float bw=width<1054?48:64;var flow=new PlaybackButtonFlow(width);var rects=new List<PlaybackButtonFlow.Slot>();
            Action<float,float> take=(w,gap)=>rects.Add(flow.Take(w,gap));
            if(vmd){for(int i=0;i<5;i++)take(56,4);if(width>=650)take(130,4);}
            else{for(int i=0;i<(folder?5:3);i++)take(bw,6);if(folder){take(Math.Max(bw,92),6);take(Math.Max(bw,108),6);}take(Math.Max(bw,108),6);if(folder)take(Math.Max(bw,98),6);}
            if(handy)take(110,4);take(58,4);take(58,4);take(58,4);
            var pair=flow.Take(136);rects.Add(pair);take(70,4);take(70,4);
            foreach(var r in rects){Check(r.X>=6&&r.X+r.Width<=width-6,"button outside bar at width "+width);}
            for(int i=1;i<rects.Count;i++)Check(rects[i].Y>rects[i-1].Y||rects[i].X>=rects[i-1].X+rects[i-1].Width,"overlapping buttons");
            Check(pair.Width==58+4+74,"iPhone and magic must remain an adjacent pair");
            float list=PlaybackButtonFlow.ListOffset(width,bw,folder,handy,vmd);
            Check(list==(flow.Row+1)*26+(vmd&&width<980?26:0),"measurement/drawing row count mismatch");
            foreach(var r in rects)Check(r.Y+22<list,"button overlaps following list");
            if(width>=760)Check(flow.Row<=1,"ordinary viewport should use at most two button rows");
            float min=PlaybackTransportLayout.MinimumHeight(width,false,bw,folder,handy,vmd);
            Check(min>=list+26+PlaybackTransportLayout.ToggleRowHeight(width),"bar height omits wrapped rows");
        }
        Console.WriteLine("PASS button layout checks="+checks+"; iPhone/magic paired; normal/VMD; narrow/portrait/wide; list separation.");
    }
}
