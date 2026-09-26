using RecompOne.Runtime.Assets;
using RecompOne.Runtime.Assets.Native;
using RecompOne.Runtime.Hle;

static class CutoutPixels
{
    public static void Run(GlCore core,string label,Action<bool,string> check)
    {
        string path=Path.Combine(AppContext.BaseDirectory,"Fixtures","cutout-categories.png");
        byte[] data=new byte[64*64*4];
        for(int y=0;y<64;y++)for(int x=0;x<64;x++) {
            int p=(y*64+x)*4;
            data[p]=248; data[p+3]=(byte)(x<32?0:y<32?255:128);
        }
        PngWriter.WriteRgba(path,data,64,64);
        var asset=new NativeTextureAsset("test-cutout",path,16,16,smoothCutout:true);
        var material=new NativeTextureMaterial(asset,0,0,16,16,0x11a,0);
        ushort background=(ushort)(8|(12<<5)|(16<<10));
        double[] Channels(ushort p)=>[(p&31),((p>>5)&31),((p>>10)&31)];
        HleVertex V(float x,float y,float u,float v)=>new(){X=x,Y=y,U=u,V=v,Z=1,R=128,G=128,B=128};
        foreach(int mode in new[]{-1,0,1,2,3})foreach(bool stp in new[]{false,true}) {
            core.FillRect(0,0,64,64,background);
            core.SetDrawEnv(new(){ClipX1=63,ClipY1=63});
            var flags=new PrimFlags{Textured=true,RawTexture=true,NativeTexture=material,SemiTrans=mode>=0,
                TPage=(ushort)(0x11a|(Math.Max(mode,0)<<5))};
            float v=stp?12:4;
            core.DrawTri(V(0,0,8,v),V(64,0,8,v),V(0,64,8,v),flags);
            core.Flush();ushort[] pixels=new ushort[4096];core.ReadVram(0,0,64,64,pixels);
            double[] src=[31,0,0],dst=Channels(background);
            var expected=dst.Select((d,i)=>.5*d+.5*(!stp||mode<0?src[i]:Math.Clamp(mode switch {
                0=>(d+src[i])*.5,1=>d+src[i],2=>d-src[i],3=>d+src[i]*.25,_=>0},0,31))).ToArray();
            check(Channels(pixels[12*64+12]).Zip(expected).All(p=>Math.Abs(p.First-p.Second)<=1),
                $"{label}: buoy edge blends occupancy independently of STP, mode {mode}, STP {stp}");
        }
        check(!asset.AllowsEffectCoverage&&!asset.WaterSpray,"buoy cutout cannot opt into spray semantics");
        core.FillRect(0,0,64,64,background);
        core.SetDrawEnv(new(){ClipX1=63,ClipY1=63,SetMask=true});
        var opaque=new PrimFlags{Textured=true,RawTexture=true,NativeTexture=material,TPage=0x11a};
        core.DrawTri(V(0,0,4,4),V(64,0,4,4),V(0,64,4,4),opaque);
        core.Flush();ushort[] empty=new ushort[4096];core.ReadVram(0,0,64,64,empty);
        check(empty[12*64+12]==background,$"{label}: transparent cutout discards without changing destination mask");
        string tilePath=Path.Combine(AppContext.BaseDirectory,"Fixtures","ui-tile-gradient.png");
        for(int y=0;y<64;y++)for(int x=0;x<64;x++) {
            int p=(y*64+x)*4;data[p]=(byte)(x*248/63);data[p+1]=(byte)(y*248/63);data[p+3]=255;
        }
        PngWriter.WriteRgba(tilePath,data,64,64);
        var untiled=new NativeTextureAsset("test-ui-reference",tilePath,16,16,smoothCutout:true);
        var tiled=new NativeTextureAsset("test-ui-tile",tilePath,16,16,smoothCutout:true,uiTileSize:8);
        ushort[] DrawTile(NativeTextureAsset source,float extent,bool rotated) {
            core.FillRect(0,0,64,64,background);
            core.SetDrawEnv(new(){ClipX1=63,ClipY1=63});
            var flags=new PrimFlags{Textured=true,RawTexture=true,TPage=0x11a,
                NativeTexture=new(source,0,0,16,16,0x11a,0)};
            var a=V(0,0,0,0);var b=V(64,0,extent,0);var c=V(0,64,0,extent);
            if(rotated){a=V(64,0,0,0);b=V(64,64,extent,0);c=V(0,0,0,extent);}
            core.DrawTri(a,b,c,flags);core.Flush();
            ushort[] result=new ushort[4096];core.ReadVram(0,0,64,64,result);return result;
        }
        foreach(bool rotated in new[]{false,true}) {
            var reference=DrawTile(untiled,8,rotated);
            check(reference.SequenceEqual(DrawTile(tiled,7,rotated)),
                $"{label}: inclusive menu tile UVs match contiguous replacement, rotated {rotated}");
            check(!reference.SequenceEqual(DrawTile(untiled,7,rotated)),
                $"{label}: non-UI materials retain original UVs, rotated {rotated}");
        }
    }
}
