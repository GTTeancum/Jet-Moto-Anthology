using RecompOne.Runtime.Assets.Native;
using RecompOne.Runtime.Hle;

// New alpha means geometric coverage, NOT the old PS1 STP bit. Check the
// real output against (1-a)*destination + a*nativeBlend(source,destination).
// Runs at 1x/2x/4x on all three actual GL backends, including overlapping sprites.
static class EffectPixels
{
    public static void Run(GlCore core,string label,Action<bool,string> check)
    {
        void Check(bool ok,string name)=>check(ok,label+": "+name);
        NativeTextureBindings.OriginalAssetsOnly=true;
        string dir=Path.Combine(AppContext.BaseDirectory,"Fixtures");
        NativeTextureMaterial M(string name)=>new(new NativeTextureAsset("original-asset/"+name,
            Path.Combine(dir,name+".png"),16,16,true),0,0,16,16,0x11a,0);
        var materials=new[]{M("coverage-0"),M("coverage-64"),M("coverage-128"),M("coverage-192"),M("coverage-255")};
        var edge=M("coverage-edge");
        ushort background=(ushort)(8|(12<<5)|(16<<10));
        HleVertex V(float x,float y,float u=8,float v=8)=>new(){X=x,Y=y,U=u,V=v,Z=1,R=128,G=128,B=128};
        PrimFlags F(NativeTextureMaterial m,int mode)=>new(){Textured=true,RawTexture=true,NativeTexture=m,SemiTrans=mode>=0,TPage=(ushort)(0x11a|((Math.Max(mode,0))<<5))};
        void Clear(bool set=false,bool checkMask=false,ushort? color=null){core.FillRect(0,0,64,64,color??background);core.SetDrawEnv(new HleDrawEnv{ClipX1=63,ClipY1=63,SetMask=set,CheckMask=checkMask});}
        void Tri(PrimFlags f,float u=8)=>core.DrawTri(V(0,0,u),V(64,0,u),V(0,64,u),f);
        ushort[] Finish(){core.Flush();var p=new ushort[4096];core.ReadVram(0,0,64,64,p);return p;}
        double[] Channels(ushort p)=>new[]{(double)(p&31),(double)((p>>5)&31),(double)((p>>10)&31)};
        bool Near(ushort p,double[] expected,int tolerance=1)=>Channels(p).Zip(expected).All(x=>Math.Abs(x.First-x.Second)<=tolerance);
        double[] Blend(double[] dst,double a,int mode){var src=new[]{31.0,0,0};return dst.Select((d,i)=>Math.Clamp((1-a)*d+a*(mode switch{-1=>src[i],0=>(d+src[i])*.5,1=>d+src[i],2=>d-src[i],3=>d+src[i]*.25,_=>throw new Exception()}),0,31)).ToArray();}
        int[] levels={0,64,128,192,255};
        for(int mode=-1;mode<4;mode++) for(int k=0;k<5;k++) {
            Clear();Tri(F(materials[k],mode));var pixels=Finish();
            var expected=Blend(Channels(background),levels[k]/255.0,mode);
            Check(Near(pixels[12*64+12],expected),$"mode {mode}, alpha {levels[k]} preserves analytical coverage blend (got {string.Join(',',Channels(pixels[12*64+12]))})");
            Check((pixels[12*64+12]&0x8000)==0,$"mode {mode}, alpha {levels[k]} does not turn coverage into the PS1 mask/STP bit");
        }
        foreach(int mode in new[]{-1,0,1,2,3}) {
            Clear();var f=F(materials[2],mode);Tri(f);Tri(f);var p=Finish();
            var expected=Blend(Blend(Channels(background),128/255.0,mode),128/255.0,mode);
            Check(Near(p[12*64+12],expected,2),$"overlapping same-asset coverage accumulates correctly, mode {mode}");
        }
        Clear();var edgeFlags=F(edge,-1);Tri(edgeFlags,7.75f);var boundary=Finish();
        Check(Near(boundary[12*64+12],Channels(background)),"transparent padded side remains transparent");
        Clear();Tri(edgeFlags,8f);var half=Finish();
        Check(Near(half[12*64+12],Blend(Channels(background),.5,-1)),"linear high-resolution edge gives half coverage without a dark color fringe");
        Clear();Tri(edgeFlags,8.25f);var full=Finish();
        Check(Near(full[12*64+12],new[]{31.0,0,0}),"opaque side of filtered boundary retains full color");
        Clear(set:true);Tri(F(materials[2],-1));var masked=Finish();
        Check((masked[12*64+12]&0x8000)!=0,"explicit SetMaskBit still applies to effect draws");
        Clear(set:true);Tri(F(materials[0],-1));var empty=Finish();
        Check(empty[12*64+12]==background,"alpha zero discards before SetMaskBit and preserves destination");
        Clear(checkMask:true,color:(ushort)(background|0x8000));Tri(F(materials[4],-1));var protectedPixels=Finish();
        Check(protectedPixels[12*64+12]==(background|0x8000),"CheckMaskBit protects destination from smooth effects");
        // Deliberately alternate classic and coverage assets sharing all page state.
        var classic=new NativeTextureMaterial(new NativeTextureAsset("classic",Path.Combine(dir,"green.png"),16,16),0,0,16,16,0x11a,0);
        Clear();Tri(F(materials[2],-1));Tri(F(classic,-1));Tri(F(materials[2],-1));var alternate=Finish();
        Check(Near(alternate[12*64+12],Blend(new[]{0.0,31,0},128/255.0,-1)),"coverage/classic batch switches retain distinct material semantics");
        var wrongScene=new WorldScene("effect-conflict",new byte[16],2,new(-32,-32),new(64,64),new(-16,80),System.Numerics.Vector3.UnitZ,new(0,0,1));
        var wrongCamera=new WorldCamera(wrongScene,new WorldBasis(1,0,0,0,-1,0,0,0,-1),new(0,0,64),0);
        var effectConflict=F(materials[2],-1);
        effectConflict.WorldSurface=new WorldSurface(wrongCamera,System.Numerics.Vector3.Zero,System.Numerics.Vector3.UnitZ,4,32,32,32);
        Clear();Tri(effectConflict);var conflictPixels=Finish();
        Check(Near(conflictPixels[12*64+12],Blend(Channels(background),128/255.0,-1)),
            "verified spray ignores conflicting ocean metadata and retains its own color and coverage");
        string plumePath=Path.Combine(dir,"plume-frame.png");
        byte[] plumePixels=new byte[256*256*4];
        for(int y=0;y<256;y++)for(int x=0;x<256;x++) {
            int i=(y*256+x)*4;plumePixels[i]=255;plumePixels[i+3]=(byte)(y>=128?255:0);
        }
        RecompOne.Runtime.Assets.PngWriter.WriteRgba(plumePath,plumePixels,256,256);
        File.WriteAllText(plumePath+".material.json",System.Text.Json.JsonSerializer.Serialize(new {
            format="jetmoto-effect-material-1",sourceKey="original-asset/plume-frame",
            sourceWidth=64,sourceHeight=64,alphaMode="coverage"}));
        var plume=new NativeTextureMaterial(new NativeTextureAsset("original-asset/plume-frame",plumePath,64,64,true),0,0,64,64,0x11a,0);
        ushort[] PlumeFrame(float bottom) {
            Clear();core.DrawTri(V(0,0,0,2),V(64,0,63,2),V(0,64,0,bottom),F(plume,-1));return Finish();
        }
        var newborn=PlumeFrame(4);var mature=PlumeFrame(59);
        Check(Near(newborn[40*64+8],new[]{31.0,0,0}),"newborn native spray slice samples the authored plume instead of transparent top padding");
        Check(newborn.AsSpan().SequenceEqual(mature),"native spray UV growth retains complete coverage art across frame ages");
        for(int i=0;i<35;i++)core.AdvanceFrame();
        var flowing=PlumeFrame(59);
        Check(!flowing.AsSpan().SequenceEqual(mature),"spray coverage travels across a stationary native primitive with time");
        for(int i=35;i<70;i++)core.AdvanceFrame();
        var beforeWrap=PlumeFrame(59);core.AdvanceFrame();var afterWrap=PlumeFrame(59);
        double delta=beforeWrap.Zip(afterWrap).Average(pair=>Math.Abs((pair.First&31)-(pair.Second&31)));
        Check(delta<1.0,"spray transport phase wrap does not flash across the plume");
        var waterSpray=new NativeTextureMaterial(new NativeTextureAsset("original-asset/plume-frame",plumePath,64,64,true,true),0,0,64,64,0x11a,0);
        ushort[] Spray(NativeTextureMaterial material) {
            Clear();var f=F(material,-1);
            core.DrawTri(V(8,32,0,2),V(56,32,63,2),V(8,56,0,59),f);
            core.DrawTri(V(56,32,63,2),V(56,56,63,59),V(8,56,0,59),f);
            return Finish();
        }
        var flatWake=Spray(plume);var airborne=Spray(waterSpray);
        Check(Enumerable.Range(0,32*64).All(i=>flatWake[i]==background),"ordinary coverage effects do not acquire airborne geometry");
        Check(Enumerable.Range(0,32*64).Count(i=>airborne[i]!=background)>5,"verified water effect emits visible droplets above the retained surface wake");
        for(int i=0;i<15;i++)core.AdvanceFrame();
        var airborneMoved=Spray(waterSpray);
        Check(Enumerable.Range(0,32*64).Count(i=>airborneMoved[i]!=airborne[i])>5,"airborne droplets travel independently above stationary native wake geometry");
        ushort[] SkewedSpray(bool alternateDiagonal) {
            Clear();var f=F(waterSpray,-1);
            var a=V(8,32,0,2);var b=V(40,32,63,2);
            var c=V(24,56,0,59);var d=V(56,56,63,59);
            if(alternateDiagonal) {
                core.DrawTri(a,b,d,f);core.DrawTri(a,d,c,f);
            } else {
                core.DrawTri(a,b,c,f);core.DrawTri(b,d,c,f);
            }
            return Finish();
        }
        var diagonalA=SkewedSpray(false);var diagonalB=SkewedSpray(true);
        int diagonalChanges=Enumerable.Range(0,32*64).Count(i=>Math.Abs((diagonalA[i]&31)-(diagonalB[i]&31))>1);
        Check(diagonalChanges<=2,$"skewed airborne wake lift is independent of quad diagonal (changed pixels: {diagonalChanges})");
        ushort[] Foam(byte brightness=128) {
            Clear();var f=F(waterSpray,-1);f.RawTexture=false;
            HleVertex W(float x,float y,float u,float v) {
                var p=V(x,y,u,v);p.R=p.G=p.B=brightness;return p;
            }
            core.DrawTri(W(8,24,0,2),W(56,24,63,2),W(8,48,0,59),f);
            core.DrawTri(W(56,24,63,2),W(56,48,63,59),W(8,48,0,59),f);
            return Finish();
        }
        var foam=Foam();
        Check(Enumerable.Range(28,16).All(y=>foam[y*64+32]!=background),
            "surface foam has filled longitudinal coverage instead of separate plume outlines");
        Check(Enumerable.Range(0,64).All(y=>foam[y*64]==background && foam[y*64+63]==background),
            "surface foam remains bounded to its native effect footprint");
        for(int i=0;i<18;i++)core.AdvanceFrame();
        var foamMoved=Foam();
        Check(Enumerable.Range(28,16).Sum(y=>Enumerable.Range(16,32).Count(x=>foam[y*64+x]!=foamMoved[y*64+x]))>30,
            "foam flows within a stationary footprint independently of bike movement");
        var lateFoam=Foam(24);var deadFoam=Foam(0);
        Check(lateFoam.All(p=>(p&31)>=(background&31)) && lateFoam.Any(p=>(p&31)>(background&31)),
            "late water effects fade in opacity without becoming dark decals");
        Check(deadFoam.All(p=>p==background),"expired water effects leave no foam or airborne ghost");
        var halfFoam=Foam(64);var fullFoam=Foam();
        // A nearly transparent filament can quantize to the same 5-bit value;
        // require monotonicity everywhere and strict decay over the full effect.
        Check(Enumerable.Range(0,4096).All(i=>(lateFoam[i]&31)<=(halfFoam[i]&31) && (halfFoam[i]&31)<=(fullFoam[i]&31))
            && lateFoam.Sum(p=>p&31)<halfFoam.Sum(p=>p&31) && halfFoam.Sum(p=>p&31)<fullFoam.Sum(p=>p&31),
            "native particle lifetime gives a monotonic foam opacity fade");
        if(Environment.GetEnvironmentVariable("JETMOTO_WORLD_WAKE")=="1") {
            var world=new WorldScene("world-spray-pixels",new byte[16],2,new(-32,-32),new(64,64),
                new(-16,80),System.Numerics.Vector3.UnitZ,flatWaterLevel:0,waterSprayMaterial:waterSpray);
            ushort[] WorldSpray(float time,float x,bool atlasWindow=false) {
                // Follow the moving emitter, as gameplay does. A fixed camera
                // after a sudden stop lets momentum-carrying spray leave view.
                var camera=new WorldCamera(world,new WorldBasis(1,0,0,0,-1,0,0,0,-1),new(-x,0,16),time);
                var bounds=new RiderBounds();bounds.Include(new(x,0,4));camera.Riders[217]=bounds;
                var flags=new PrimFlags {WorldSurface=new WorldSurface(camera,new(x,0,4),System.Numerics.Vector3.UnitZ,3,32,32,32,riderId:217)};
                Clear();
                if(atlasWindow)core.SetDrawEnv(new HleDrawEnv{ClipX1=63,ClipY1=63,TwMaskX=31,TwMaskY=31,TwOffX=16,TwOffY=16});
                core.DrawTri(V(2,2),V(3,2),V(2,3),flags);return Finish();
            }
            var emptyWorld=WorldSpray(0,-6);WorldSpray(.1f,6);
            var worldFan=WorldSpray(.3f,30);
            int changed=Enumerable.Range(8,48).Sum(y=>Enumerable.Range(8,48).Count(x=>worldFan[y*64+x]!=emptyWorld[y*64+x]));
            Check(changed>5,$"world-space rider spray produces actual coverage pixels without any native wake packet ({changed} pixels)");
            var movingFan=WorldSpray(.35f,36);
            Check(!worldFan.AsSpan().SequenceEqual(movingFan),"world-space spray pixel footprint moves as ballistic particles age");
            Check(WorldSpray(2,36).AsSpan().SequenceEqual(emptyWorld),"expired world-space spray leaves no coverage pixels");
            WorldSpray(0,-6,true);WorldSpray(.1f,6,true);
            Check(WorldSpray(.3f,30,true).AsSpan().SequenceEqual(worldFan),"generated spray is independent of inherited native atlas window");
        }
        NativeTextureBindings.OriginalAssetsOnly=false;
    }
}
