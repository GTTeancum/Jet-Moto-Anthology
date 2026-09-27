using RecompOne.Runtime.Assets;
using RecompOne.Runtime.Assets.Native;
using RecompOne.Runtime.Hle;

static class TextureCachePixels
{
    public static void Run(GlCore core, string label, Action<bool,string> check)
    {
        void Check(bool ok,string message)=>check(ok,label+": "+message);
        string folder=Path.Combine(AppContext.BaseDirectory,"CacheFixtures");Directory.CreateDirectory(folder);
        var assets=new List<NativeTextureAsset>();
        for(int i=0;i<6;i++)
        {
            string file=Path.Combine(folder,$"color-{i}.png");
            if(!File.Exists(file)) {
                var pixels=new byte[1024*1024*4];
                for(int n=0;n<pixels.Length;n+=4){pixels[n+i%3]=255;pixels[n+3]=255;}
                PngWriter.WriteRgba(file,pixels,1024,1024);
            }
            assets.Add(new($"cache-{i}",file,256,256));
        }
        int stableId=core.RegisterNativeImage(assets[0]);
        Check(stableId>=0,"menu image receives a stable registration");
        var material=new NativeTextureMaterial(assets[0],0,0,256,256,0x11a,0);
        void Begin(){core.FillRect(0,0,64,64,0);core.SetDrawEnv(new HleDrawEnv{ClipX1=63,ClipY1=63});}
        void Triangle(PrimFlags flags,float uv)=>core.DrawTri(
            new HleVertex{X=0,Y=0,U=uv,V=uv,R=128,G=128,B=128},
            new HleVertex{X=64,Y=0,U=uv,V=uv,R=128,G=128,B=128},
            new HleVertex{X=0,Y=64,U=uv,V=uv,R=128,G=128,B=128},flags);
        ushort Pixel(){var p=new ushort[4096];core.ReadVram(0,0,64,64,p);return p[8*64+8];}
        Begin();Triangle(new PrimFlags{Textured=true,RawTexture=true,TPage=0x11a,NativeTexture=material},8);
        // Deliberately evict the asset while its triangle is still pending.
        foreach(var asset in assets.Skip(1))Check(core.PrepareNativeTexture(asset),"prepare large replacement");
        Check((Pixel()&0x7fff)==31,"pending batch survives texture eviction with exact red pixels");
        Check(core.NativeCacheStats.Evictions>0 && core.NativeCacheStats.Bytes<=16L*1024*1024,"GPU residency respects the small-machine budget");
        Begin();Triangle(new PrimFlags{Textured=true,UseImage=true,Image=stableId},.5f);
        Check((Pixel()&0x7fff)==31,"previously registered menu image reloads after eviction");
        Begin();Triangle(new PrimFlags{Textured=true,RawTexture=true,TPage=0x11a,NativeTexture=material},8);
        Check((Pixel()&0x7fff)==31,"native material identity also survives eviction and replay");
    }
}
