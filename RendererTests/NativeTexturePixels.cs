using RecompOne.Runtime.Assets.Native;
using RecompOne.Runtime.Hle;

// Synthetic source assets only: the tests cannot accidentally pass by finding a
// matching image in VRAM. Run on each real GL backend at 1x, 2x, and 4x.
static class NativeTexturePixels
{
    public static void Run(GlCore core,string label,Action<bool,string> check)
    {
        NativeTextureBindings.OriginalAssetsOnly=true;
        void Check(bool ok,string name)=>check(ok,label+": "+name);
        string dir=Path.Combine(AppContext.BaseDirectory,"Fixtures");
        NativeTextureMaterial Material(string name,int w=16)=>new(new NativeTextureAsset("original-asset/"+name,
            Path.Combine(dir,name+".png"),w,16),0,0,w,16,0x11a,0);
        var red=Material("red");var green=Material("green");var detail=Material("detail");var alpha=Material("alpha");var gradient=Material("gradient",64);
        const ushort Blue=0x7c00;
        core.WriteVram(640,256,64,64,Enumerable.Repeat(Blue,4096).ToArray());
        HleVertex V(float x,float y,float u,float v,float z=1)=>new(){X=x,Y=y,U=u,V=v,Z=z,HasGteZ=z!=1,R=128,G=128,B=128};
        var f=new PrimFlags{Textured=true,RawTexture=true,TPage=0x11a,NativeTexture=red};
        ushort[] Finish(){core.Flush();var p=new ushort[4096];core.ReadVram(0,0,64,64,p);return p;}
        void Clear(HleDrawEnv? env=null){core.FillRect(0,0,64,64,Blue);core.SetDrawEnv(env??new HleDrawEnv{ClipX1=63,ClipY1=63});}
        void Quad(PrimFlags flags,float x0=0,float x1=64)
        {
            HleVertex a=V(x0,0,0,0),b=V(x1,0,15,0),c=V(x0,64,0,15),d=V(x1,64,15,15);
            core.DrawTri(a,b,c,flags);core.DrawTri(b,d,c,flags);
        }
        ushort[] Render(NativeTextureMaterial? m){Clear();var flags=f;flags.NativeTexture=m;Quad(flags);return Finish();}
        int R(ushort p)=>p&31;int B(ushort p)=>(p>>10)&31;
        var native=Render(red);
        Check(native[20*64+20]==31,"explicit red native image replaces contrasting blue source pixels");
        core.WriteVram(640,256,64,64,Enumerable.Repeat((ushort)0x7fff,4096).ToArray());
        Check(Render(red).AsSpan().SequenceEqual(native),"changing all source VRAM pixels does not change native asset selection/output");
        var original=Render(null);
        Check(original[20*64+20]==0x7fff,"unbound primitive uses ordinary original texture rendering");
        core.WriteVram(640,256,64,64,Enumerable.Repeat(Blue,4096).ToArray());
        Check(Render(Material("missing"))[20*64+20]==Blue,"missing native PNG falls back without invoking hash replacement");
        Check(Render(red with{TPage=0x11b})[20*64+20]==Blue,"changed native material cannot reskin an unrelated page");
        var hd=Render(detail);
        int[] scan=Enumerable.Range(8,12).Select(x=>hd[16*64+x]&0x7fff).Distinct().ToArray();
        Check(scan.Contains(31)&&scan.Contains(31<<5),"distinct 4x texels survive; replacement was not reduced to original resolution");
        Clear();var rf=f;rf.NativeTexture=red;var gf=f;gf.NativeTexture=green;Quad(rf,0,32);Quad(gf,32,64);var batch=Finish();
        Check(batch[16*64+16]==31&&batch[16*64+48]==(31<<5),"same page/palette/UV with different native IDs produces distinct batched assets");
        Clear();var af=f;af.NativeTexture=alpha;af.SemiTrans=true;Quad(af);var ap=Finish();
        Check(ap[32*64+10]==Blue,"native alpha zero leaves destination unchanged");
        Check(R(ap[32*64+32]) is >=15 and <=16 && B(ap[32*64+32]) is >=15 and <=16,"native STP=128 uses PS1 semi-transparent blending");
        Check(ap[32*64+54]==31,"native opaque texels do not inherit a neighboring STP flag");
        // Non-contiguous mask: clear bits 3 and 5, then force bit 5. Modulo(mask+1)
        // cannot reproduce this; both modern GLSL and GLSL 1.20 must implement it.
        var env=new HleDrawEnv{ClipX1=63,ClipY1=63,TwMaskX=5,TwOffX=4};
        Clear(env);var grad=f;grad.NativeTexture=gradient;
        core.DrawTri(V(4,4,0,0),V(60,4,63,0),V(4,60,0,15),grad);var window=Finish();
        int errors=0;
        foreach(int x in new[]{8,12,19,26,34,42})
        {
            double u=(x+.5-4)/56*63;int whole=(int)Math.Floor(u);double mapped=(whole&215)|32;
            int expected=(int)Math.Floor((mapped+u-whole)*4)/8;
            if(Math.Abs(R(window[8*64+x])-expected)>1)errors++;
        }
        Check(errors==0,$"non-contiguous texture-window mapping retains 4x fractional UV ({errors} errors)");
        foreach(int whole in new[]{7,8,15,16,31,32,63})
        {
            Clear(env);float u=whole+.25f;
            core.DrawTri(V(0,0,u,8),V(64,0,u,8),V(0,64,u,8),grad);var constant=Finish();
            int expected=(((whole&215)|32)*4+1)/8;
            Check(R(constant[16*64+16])==expected,$"exact bit-window boundary {whole} plus fractional texel");
        }
        ushort[] Triangle(bool perspective)
        {
            Clear();core.DrawTri(V(4,4,0,0,perspective?100:1),V(60,4,63,0,perspective?800:1),V(4,60,0,15,perspective?100:1),grad);return Finish();
        }
        var precise=Triangle(true);var affine=Triangle(false);errors=0;
        foreach(int x in new[]{10,18,26,34})foreach(int y in new[]{10,18})
        {
            double lb=(x+.5-4)/56,lc=(y+.5-4)/56,la=1-lb-lc;
            double u=(lb*63/800)/(la/100+lb/800+lc/100);
            int expected=(int)Math.Floor(u*4)/8;
            if(Math.Abs(R(precise[y*64+x])-expected)>1)errors++;
        }
        Check(errors==0,"native high-resolution UV retains analytical perspective-correct interpolation");
        Check(Math.Abs(R(precise[20*64+20])-R(affine[20*64+20]))>=5,"native perspective result differs from affine negative control");
        NativeTextureBindings.OriginalAssetsOnly=false;
    }
}
