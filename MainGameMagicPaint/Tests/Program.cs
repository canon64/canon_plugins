using System;
using System.Linq;
using MainGameMagicPaint;
using V=System.Numerics.Vector3;
using U=System.Numerics.Vector2;
static class Program
{
    static int _checks;
    static void Check(bool condition,string message) { _checks++; if(!condition)throw new Exception(message); }
    static string ReadShared(string path)
    { using(var file=new System.IO.FileStream(path,System.IO.FileMode.Open,System.IO.FileAccess.Read,System.IO.FileShare.ReadWrite)) using(var reader=new System.IO.StreamReader(file)) return reader.ReadToEnd(); }
    static void Main()
    {
        Console.WriteLine("[MagicPaint] Product editing/geometry/input tests started");
        Check(!PaintVisibility.Participates(true,true,false,false,11,"o_shadowcaster"),"native shadow proxy must not block face or body");
        Check(!PaintVisibility.Participates(true,true,false,false,11,"o_shadowcaster_cm"),"second native shadow proxy must not block");
        Check(!PaintVisibility.Participates(true,true,false,true,10,"cf_hair"),"shadow-only hidden hair must not block");
        Check(PaintVisibility.Participates(true,true,false,false,10,"cf_hair_f_20"),"hair visibility is independent of the actor hair-occlusion exclusion");
        Check(PaintVisibility.Participates(true,true,false,false,10,"o_bra_a"),"visible clothes must still block paint");
        Check(PaintVisibility.Participates(true,true,false,false,10,"cf_O_face"),"native face remains a contact candidate");
        Check(PaintVisibility.Participates(true,true,false,false,11,"visible_mesh"),"layer 11 alone must not hide a real obstacle");
        Check(!PaintVisibility.Participates(false,true,false,false,10,"mesh")&&!PaintVisibility.Participates(true,false,false,false,10,"mesh")&&!PaintVisibility.Participates(true,true,true,false,10,"mesh"),"disabled inactive and forced-off meshes stay excluded");
        uint red=PaintBuffer.Pack(255,0,0,255),blue=PaintBuffer.Pack(0,0,255,255);
        // Simulate BakeMesh vertices with scale already applied, then move and rotate the head.
        var bakedFace=new[]{new V(0,0,0),new V(.032f,0,0),new V(0,.024f,0)};
        var oldPose=SkinCoordinates.BakedToWorld(new V(2,1.5f,-64),System.Numerics.Quaternion.Identity);
        var oldWorld=bakedFace.Select(p=>V.Transform(p,oldPose)).ToArray();
        var oldPoint=oldWorld[0]*.5f+oldWorld[1]*.2f+oldWorld[2]*.3f;
        var followAnchor=SurfaceAnchor.Capture(oldWorld,new[]{0,1,2},0,oldPoint);
        var poseIndex=new TriangleIndex(oldWorld,new[]{0,1,2});
        var headPosition=new V(2.12f,1.55f,-63.9f);
        var headRotation=System.Numerics.Quaternion.CreateFromYawPitchRoll(.8f,-.3f,.2f);
        var currentPose=SkinCoordinates.BakedToWorld(headPosition,headRotation);
        var movedWorld=bakedFace.Select(p=>V.Transform(p,currentPose)).ToArray();
        Check(V.Distance(movedWorld[1],headPosition+V.Transform(bakedFace[1],headRotation))<1e-5f,"baked face uses current position and rotation without applying scale twice");
        Check(Math.Abs(V.Distance(movedWorld[0],movedWorld[1])-.032f)<1e-5f,"0.8-scaled baked face retains physical width after head rotation");
        var movedPoint=movedWorld[0]*.5f+movedWorld[1]*.2f+movedWorld[2]*.3f;
        var movedNormal=SurfaceMath.TriangleNormal(movedWorld[0],movedWorld[1],movedWorld[2]);
        Check(!poseIndex.Near(movedPoint+movedNormal*.005f,.02f,out _,out _),"old pose reproduces lost contact after head movement");
        poseIndex.Refit(movedWorld);
        Check(poseIndex.Near(movedPoint+movedNormal*.005f,.02f,out _,out var currentContact)&&V.Distance(currentContact,movedPoint)<2e-5f,"current pose restores 5 mm skin contact after translation and rotation");
        Check(poseIndex.Ray(movedPoint+movedNormal*.05f,-movedNormal,.1f,out _,out float poseDistance)&&Math.Abs(poseDistance-.05f)<2e-5f,"spray ray follows current head pose");
        Check(followAnchor.Resolve(movedWorld,out var movedAnchor)&&V.Distance(movedAnchor,movedPoint)<2e-5f,"previous stroke endpoint follows its triangle after head movement");
        var deformedWorld=(V[])movedWorld.Clone();deformedWorld[1]+=new V(.005f,.003f,.002f);
        var expectedDeformed=deformedWorld[0]*.5f+deformedWorld[1]*.2f+deformedWorld[2]*.3f;
        Check(followAnchor.Resolve(deformedWorld,out var deformedAnchor)&&V.Distance(deformedAnchor,expectedDeformed)<2e-5f,"stroke anchor also follows expression deformation without world-space trailing");
        Check(!default(SurfaceAnchor).Resolve(deformedWorld,out _),"missing stroke anchor cannot connect unrelated contacts");
        Check(!followAnchor.Resolve(new V[1],out _),"invalidated mesh anchor cannot read replaced topology");
        // Reproduce attached-but-no-ink on a 1 mm facial triangle in actual world coordinates.
        var tinyA=new V(2,1.5f,-64);var tinyB=tinyA+new V(.001f,0,0);var tinyC=tinyA+new V(0,.001f,0);
        var tinyVertices=new[]{tinyA,tinyB,tinyC};var tinyUv=new[]{U.Zero,U.UnitX,U.UnitY};
        var tinyCenter=(tinyA+tinyB+tinyC)/3;var tinyInk=new PaintBuffer(64);var zeroNormalStats=new BrushStats();
        Check(V.Cross(tinyB-tinyA,tinyC-tinyA).Length()<1e-5f,"small face cross product falls below the old normalization cutoff");
        BrushRaster.Stamp(tinyInk,tinyVertices,tinyUv,new[]{0,1,2},tinyCenter,V.Zero,.0004f,red,1,false,0,null,zeroNormalStats);
        Check(zeroNormalStats.FacingRejected==1&&tinyInk.Revision==0,"zero contact normal reproduces all-facing-rejected with no ink");
        var tinyNormal=SurfaceMath.TriangleNormal(tinyA,tinyB,tinyC);
        Check(V.Distance(tinyNormal,V.UnitZ)<1e-6f,"small face retains a unit contact normal");
        BrushRaster.Stamp(tinyInk,tinyVertices,tinyUv,new[]{0,1,2},tinyCenter,tinyNormal,.0004f,red,1,false,0);
        Check(tinyInk.Revision>0,"real raster writes ink on small facial triangle with corrected contact normal");
        var wrongSide=new PaintBuffer(64);BrushRaster.Stamp(wrongSide,tinyVertices,tinyUv,new[]{0,1,2},tinyCenter,-tinyNormal,.0004f,red,1,false,0);
        Check(wrongSide.Revision==0,"corrected normal preserves rejection of opposite skin side");
        Check(SurfaceMath.TriangleNormal(tinyA,tinyA,tinyC)==V.Zero,"degenerate triangle remains rejected");
        Check(V.Distance(SurfaceMath.TriangleNormal(tinyA,tinyC,tinyB),-V.UnitZ)<1e-6f,"normal respects triangle winding");
        Check(PaintBuffer.Blend(0,red,0,false)==0,"transparent ink must not paint");
        Check(PaintBuffer.Blend(blue,red,1,false)==red,"opaque source-over");
        uint mixed=PaintBuffer.Blend(blue,red,.5f,false);
        Check((mixed&255)>=127&&((mixed>>16)&255)>=127&&(mixed>>24)==255,"source-over keeps opacity and blends colors");
        uint erased=PaintBuffer.Blend(red,0,.5f,true);
        Check((erased>>24)>=127&&(erased>>24)<=128&&(erased&0xffffff)==255,"erasing reduces existing alpha, not color");
        for(int i=0;i<20;i++)erased=PaintBuffer.Blend(erased,0,.5f,true);
        Check(erased==0,"erase must eventually become fully transparent");
        var face=new PaintBuffer(64);var body=new PaintBuffer(64);var history=new PaintHistory();
        face.Set(1,1,red);body.Set(50,50,blue);history.Commit(new[]{face,body});
        Check(history.UndoCount==1,"one stroke spanning face/body is one action");
        history.Undo();Check(face.Pixels[65]==0&&body.Pixels[3250]==0,"undo both surfaces");
        history.Redo();Check(face.Pixels[65]==red&&body.Pixels[3250]==blue,"redo both surfaces");
        face.Clear();body.Clear();history.Commit(new[]{face,body});history.Undo();
        Check(face.Pixels[65]==red&&body.Pixels[3250]==blue,"B reset can be undone");
        history.Redo();Check(face.Pixels.All(p=>p==0)&&body.Pixels.All(p=>p==0),"B reset can be redone");
        history.Undo();face.Set(3,3,blue);history.Commit(new[]{face,body});Check(history.RedoCount==0,"new edits invalidate redo");
        var other=new PaintHistory();other.Undo();Check(face.Pixels[65]==red,"separate character history does not affect selected actor");
        history.Commit(new[]{face,body});Check(history.UndoCount==2,"no-op does not create history");
        Check(SurfaceMath.RayTriangle(new V(.25f,.25f,1),-V.UnitZ,V.Zero,V.UnitX,V.UnitY,out var d,out var bary)&&Math.Abs(d-1)<1e-6&&Math.Abs(bary.X-.5f)<1e-6,"ray returns exact deformed triangle barycentrics");
        Check(!SurfaceMath.RayTriangle(new V(2,2,1),-V.UnitZ,V.Zero,V.UnitX,V.UnitY,out d,out bary),"ray outside triangle rejected");
        Check(!SurfaceMath.RayTriangle(new V(0,0,-1),-V.UnitZ,V.Zero,V.UnitX,V.UnitY,out d,out bary),"behind-origin triangle rejected");
        Check(V.Distance(SurfaceMath.Closest(new V(.2f,.3f,.01f),V.Zero,V.UnitX,V.UnitY),new V(.2f,.3f,0))<1e-5,"closest contact uses skin, not capsule");
        Check(V.Distance(SurfaceMath.Closest(new V(3,0,0),V.Zero,V.UnitX,V.UnitY),V.UnitX)<1e-6,"closest contact clamps to triangle edge");
        // Adjacent physical triangles, disconnected UV islands. A stroke at the seam must hit both islands only.
        var verts=new[]{new V(-1,0,0),new V(0,0,0),new V(0,1,0),new V(0,0,0),new V(1,0,0),new V(0,1,0)};
        var uvs=new[]{new U(.05f,.1f),new U(.35f,.1f),new U(.35f,.9f),new U(.65f,.1f),new U(.95f,.1f),new U(.65f,.9f)};
        var buffer=new PaintBuffer(256);BrushRaster.Stamp(buffer,verts,uvs,new[]{0,1,2,3,4,5},new V(0,.4f,0),V.UnitZ,.15f,red,1,false,0);
        Check(buffer.Pixels.Where((p,i)=>i%256<128).Any(p=>p!=0),"seam left island painted");
        Check(buffer.Pixels.Where((p,i)=>i%256>=128).Any(p=>p!=0),"seam right island painted");
        Check(!buffer.Pixels.Where((p,i)=>i%256>100&&i%256<155).Any(p=>p!=0),"no false line drawn through UV gap");
        int painted=buffer.Pixels.Count(p=>p!=0);BrushRaster.Stamp(buffer,verts,uvs,new[]{0,1,2,3,4,5},new V(0,.4f,.4f),V.UnitZ,.1f,blue,1,false,0);
        Check(buffer.Pixels.Count(p=>p!=0)==painted,"brush does not paint a distant surface");
        var backwards=new PaintBuffer(256);BrushRaster.Stamp(backwards,verts,uvs,new[]{0,1,2,3,4,5},new V(0,.4f,0),-V.UnitZ,.15f,red,1,false,0);
        Check(backwards.Pixels.All(p=>p==0),"brush rejects opposite side triangles");
        // BVH queries must match an exhaustive reference after deformation, while pruning distant triangles.
        const int side=100; var grid=new V[(side+1)*(side+1)];var faces=new int[side*side*6];
        for(int y=0;y<=side;y++)for(int x=0;x<=side;x++)grid[y*(side+1)+x]=new V(x*.01f,y*.01f,0);
        for(int y=0;y<side;y++)for(int x=0;x<side;x++){int at=(y*side+x)*6,a=y*(side+1)+x;faces[at]=a;faces[at+1]=a+1;faces[at+2]=a+side+1;faces[at+3]=a+1;faces[at+4]=a+side+2;faces[at+5]=a+side+1;}
        var index=new TriangleIndex(grid,faces);var random=new Random(42);int visits=0;
        for(int query=0;query<80;query++)
        {
            var origin=new V((float)random.NextDouble(),(float)random.NextDouble(),1);float expected=float.PositiveInfinity;
            for(int i=0;i<faces.Length;i+=3)if(SurfaceMath.RayTriangle(origin,-V.UnitZ,grid[faces[i]],grid[faces[i+1]],grid[faces[i+2]],out float length,out _))expected=Math.Min(expected,length);
            Check(index.Ray(origin,-V.UnitZ,2,out _,out float actual)&&Math.Abs(actual-expected)<1e-5,"BVH ray agrees with exhaustive reference");visits+=index.TriangleVisits;
        }
        Check(visits<80*200,"BVH visits fewer than one percent of triangles in spatially local rays");
        for(int i=0;i<grid.Length;i++)grid[i].Z=.03f*(float)Math.Sin(grid[i].X*4);index.Refit(grid);
        Check(index.Ray(new V(.4f,.6f,1),-V.UnitZ,2,out _,out float moved)&&moved<.98f,"refit follows deformed skin");
        Check(index.Near(new V(.4f,.6f,.03f),.01f,out _,out _),"contact follows deformed skin");
        var candidates=new System.Collections.Generic.List<int>();index.Sphere(new V(.4f,.6f,.03f),.002f,candidates);
        Check(candidates.Count<200,"stamp only visits local triangle candidates");
        var indexed=new PaintBuffer(256);var exhaustive=new PaintBuffer(256);var meshIndex=new TriangleIndex(verts,new[]{0,1,2,3,4,5});
        meshIndex.Sphere(new V(0,.4f,0),.15f,candidates);
        BrushRaster.Stamp(indexed,verts,uvs,new[]{0,1,2,3,4,5},new V(0,.4f,0),V.UnitZ,.15f,red,1,false,0,candidates);
        BrushRaster.Stamp(exhaustive,verts,uvs,new[]{0,1,2,3,4,5},new V(0,.4f,0),V.UnitZ,.15f,red,1,false,0);
        Check(indexed.Pixels.SequenceEqual(exhaustive.Pixels),"indexed stamp preserves UV seams and pixels");
        var dirty=new PaintBuffer(64);dirty.Set(4,5,red);Check(dirty.UploadTiles.SetEquals(new[]{0}),"upload only dirty tile");dirty.UploadTiles.Clear();dirty.Restore(3,new uint[1024]);Check(dirty.UploadTiles.Contains(3),"undo marks restored tile for upload");
        Console.WriteLine("[MagicPaint] BVH exact triangle tests per ray: "+(visits/80.0).ToString("0.0")+" / 20000");
        Check(dirty.InkPixels==1,"ink pixel counter records actual painted pixels");dirty.Clear();Check(dirty.InkPixels==0,"ink counter follows erase/reset");
        var tiny=new PaintBuffer(256);BrushRaster.Stamp(tiny,new[]{V.Zero,V.UnitX,V.UnitY},new[]{U.Zero,U.UnitX,U.UnitY},new[]{0,1,2},new V(.251f,.251f,0),V.UnitZ,.0005f,red,1,false,0);
        Check(tiny.Pixels.Any(p=>p!=0),"sub-texel spray particles contribute area-weighted ink");
        var permission=new VrPaintState();
        Check(!permission.Press()&&!permission.CanPaint(true),"unheld trigger never paints");permission.Grip();
        Check(!permission.CanPaint(true),"grip and contact alone never paint");permission.Press();
        Check(!permission.CanPaint(false),"trigger requires current contact or current guide");
        Check(permission.CanPaint(true),"held + trigger + ready surface paints");permission.Release();
        Check(!permission.CanPaint(true),"grip release immediately revokes painting");permission.Grip();
        Check(!permission.CanPaint(true),"regrip while old trigger held cannot resume");permission.Press();permission.StopPress();
        Check(!permission.CanPaint(true),"menu/tool/trigger stop revokes painting");
        var nibLatch=new ContactLatch();var eraserLatch=new ContactLatch();nibLatch.Set(true);
        Check(Math.Abs(nibLatch.Limit(.02f,.03f)-.03f)<1e-6&&Math.Abs(eraserLatch.Limit(.02f,.03f)-.02f)<1e-6,"nib and eraser retain separate attach/hold distances");
        nibLatch.Set(false);Check(Math.Abs(nibLatch.Limit(.02f,.03f)-.02f)<1e-6,"detached nib requires attach distance again");
        var inspection=new StrokeInspection();Check(!inspection.Timeout(5)&&!inspection.Finish(),"inspection OFF does no capture");inspection.Arm();
        Check(!inspection.Timeout(4.99)&&inspection.Timeout(5),"one five-second no-input deadline");inspection.Input(false);
        Check(inspection.RawInput&&!inspection.Started&&inspection.Timeout(5),"rejected input distinguished from accepted trigger");
        Check(inspection.Finish()&&!inspection.Finish(),"capture finishes once including no-pixel strokes");inspection.Arm();inspection.Input(true);
        Check(inspection.Started&&!inspection.Timeout(999),"accepted trigger cancels no-input deadline while held");inspection.Cancel();
        Check(!inspection.Active&&!inspection.Finish(),"cancel prevents subsequent capture");
        string traceDir=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"magicpaint-trace-tests-"+Guid.NewGuid().ToString("N"));
        string tracePath=System.IO.Path.Combine(traceDir,"trace.log");int built=0;
        using(var trace=new DiagnosticTrace(tracePath))
        {
            trace.Write("OFF",()=>{built++;return "must not build";});
            Check(built==0&&!System.IO.Directory.Exists(traceDir),"logging OFF creates no directory and does not build message");
            trace.SetEnabled(true);trace.Write("ON",()=>{built++;return "visible event";});
            Check(built==1&&ReadShared(tracePath).Contains("visible event"),"enabled event is flushed to readable file");
            for(int i=0;i<40;i++){int state=i;trace.Transition("CONTACT",i.ToString(),()=>"state="+state);}
            Check(ReadShared(tracePath).Split(new[]{'\n'},StringSplitOptions.RemoveEmptyEntries).Length==14,"unstable contact transitions are bounded");
            trace.SetEnabled(false);long bytes=new System.IO.FileInfo(tracePath).Length;
            trace.Write("OFF_AGAIN",()=>throw new Exception("OFF formatted"));trace.Transition("CONTACT","next",()=>throw new Exception("OFF collected"));
            Check(new System.IO.FileInfo(tracePath).Length==bytes,"turning OFF stops formatting and files again");
        }
        System.IO.File.Delete(tracePath);System.IO.Directory.Delete(traceDir);
        var rasterStats=new BrushStats();var tracedBuffer=new PaintBuffer(256);
        BrushRaster.Stamp(tracedBuffer,verts,uvs,new[]{0,1,2,3,4,5},new V(0,.4f,0),V.UnitZ,.15f,red,1,false,0,null,rasterStats);
        Check(rasterStats.Writes==tracedBuffer.Revision&&rasterStats.Writes>0,"raster diagnostic records actual pixel mutations");
        var rejectStats=new BrushStats();BrushRaster.Stamp(new PaintBuffer(256),verts,uvs,new[]{0,1,2,3,4,5},new V(0,.4f,0),-V.UnitZ,.15f,red,1,false,0,null,rejectStats);
        Check(rejectStats.FacingRejected==2&&rejectStats.Writes==0,"diagnostic distinguishes facing rejection from accepted paint");
        var stick=new StickState();Check(stick.Edge(-1,0)==-1,"left=undo");Check(stick.Edge(-1,0)==0,"held stick does not repeat undo");
        stick.Edge(0,0);Check(stick.Edge(1,0)==1,"right=redo after returning neutral");
        stick.Edge(0,0);Check(stick.Edge(.6f,.8f)==2&&stick.Vertical,"diagonal locks to dominant vertical axis");
        Check(stick.Edge(1,.3f)==0&&stick.Vertical,"direction cannot change before neutral");
        stick.Reset();stick.Menu=true;stick.X=-.9f;Check(stick.MenuSelection==0,"left menu=marker");
        stick.X=.9f;Check(stick.MenuSelection==1,"right menu=spray");stick.X=0;Check(stick.MenuSelection==-1,"neutral cancels menu");
        stick.X=.6f;stick.Y=.9f;Check(stick.MenuSelection==-1,"vertical is not an unimplemented third brush");
        stick.BlockUntilNeutral();Check(stick.Edge(-1,0)==0,"menu direction cannot immediately execute undo on release");
        stick.Edge(0,0);Check(stick.Edge(-1,0)==-1,"undo available again after neutral");
        Console.WriteLine("[MagicPaint] PASS "+_checks+" checks. These are static/product-core tests, not live KKS/VR verification.");
    }
}
