using System.Numerics;
using RecompOne.Runtime;
using RecompOne.Runtime.Hle;
using RecompOne.Runtime.Assets.Native;

static class WorldLightingPixels
{
 public static void Run(GlCore core,string label,Action<bool,string> check)
 {
  void Check(bool x,string s)=>check(x,label+": "+s);
  WorldScene Scene(bool blocker=false)
  {
   int n=128;byte[] p=new byte[n*n*4];for(int y=0;y<n;y++)for(int x=0;x<n;x++){
    // 1 world unit per texel; a known distant raised solid casts onto the plane.
    int height=blocker&&x is >=25 and <=48 && y is >=25 and <=96?38:0;
    int q=(int)Math.Round((height+16)/80.0*65535);int i=(y*n+x)*4;p[i]=(byte)(q>>8);p[i+1]=(byte)q;p[i+2]=255;p[i+3]=255;
   }
   return new WorldScene(blocker?"occluder":"flat",p,n,new(-64,-64),new(128,128),new(-16,80),new(-.65f,.05f,.76f));
  }
  var clear=Scene();var blocked=Scene(true);var rot=new WorldBasis(1,0,0,0,-1,0,0,0,-1);
  WorldCamera Camera(WorldScene s,float time=0,Vector3? shift=null)=>new(s,rot,shift??new Vector3(0,0,64),time);
  WorldSurface Surface(WorldScene s,int kind=1,float time=0)=>new(Camera(s,time),Vector3.Zero,Vector3.UnitZ,kind,32,32,32);
  const ushort Back=0x0842;var f=new PrimFlags{Gouraud=true};
  HleVertex V(float x,float y,byte r,byte g,byte b)=>new(){X=x,Y=y,R=r,G=g,B=b,U=0,V=0};
  ushort[] Draw(WorldSurface? world,bool masked=false,bool semi=false,int drawX=0,int drawY=0,byte r=152,byte g=144,byte b=128)
  {
   core.FillRect(0,0,64,64,Back);core.SetDrawEnv(new HleDrawEnv{ClipX1=63,ClipY1=63,SetMask=masked});
   var flags=f;flags.WorldSurface=world;flags.DrawOffsetX=drawX;flags.DrawOffsetY=drawY;flags.SemiTrans=semi;
   core.DrawTri(V(4,4,r,g,b),V(60,4,r,g,b),V(4,60,r,g,b),flags);core.DrawTri(V(60,4,r,g,b),V(60,60,r,g,b),V(4,60,r,g,b),flags);
   core.Flush();var p=new ushort[4096];core.ReadVram(0,0,64,64,p);return p;
  }
  float Bright(ushort p)=>(p&31)+((p>>5)&31)+((p>>10)&31);
  float AvgBright(ushort[] p)=>Enumerable.Range(0,p.Length).Where(i=>p[i]!=Back).Select(i=>Bright(p[i])).DefaultIfEmpty(0).Average();
  float AvgChannel(ushort[] p,int shift)=>Enumerable.Range(0,p.Length).Where(i=>p[i]!=Back).Select(i=>(float)((p[i]>>shift)&31)).DefaultIfEmpty(0).Average();
  var original=Draw(null);var lit=Draw(Surface(clear));var shadow=Draw(Surface(blocked));
  Check(!lit.AsSpan().SequenceEqual(original),"native geometry lighting changes tagged solids");
  Check(lit[0]==Back&&shadow[0]==Back,"lighting never adds or moves geometry outside original polygon coverage");
  Check(Enumerable.Range(0,4096).Count(i=>Bright(shadow[i])+2<Bright(lit[i]))>20,"original height occluder darkens receiver pixels (real spatial shadow, not color/contrast mask)");
  Check(Bright(lit[32*64+32])>Bright(shadow[32*64+32]),"known blocker is shadowing the expected ground sample");
  Check(Draw(Surface(clear,1,16)).AsSpan().SequenceEqual(lit),"static terrain light is time-invariant");
  var casterCamera=Camera(clear);
  casterCamera.Casters.Add(new ShadowTriangle(new(-40,-40,12),new(40,-40,12),new(-40,40,12)));
  casterCamera.Casters.Add(new ShadowTriangle(new(40,-40,12),new(40,40,12),new(-40,40,12)));
  var castGround=Draw(new WorldSurface(casterCamera,Vector3.Zero,Vector3.UnitZ,1,32,32,32));
  Check(Enumerable.Range(0,4096).Count(i=>Bright(castGround[i])+2<Bright(lit[i]))>20,"posed rider silhouette casts onto ground receivers");
  var riderClear=Draw(Surface(clear,3));
  var riderWithCaster=Draw(new WorldSurface(casterCamera,Vector3.Zero,Vector3.UnitZ,3,32,32,32));
  Check(riderClear.AsSpan().SequenceEqual(riderWithCaster),"rider receiver cannot black out from unstable dynamic silhouette self-shadowing");
  var riderBlocked=Draw(Surface(blocked,3));
  Check(riderClear.AsSpan().SequenceEqual(riderBlocked),"rider receiver ignores static terrain shadowing that can darken the whole bike");
  Check(AvgBright(riderClear)>.66f*AvgBright(original),"rider receiver lighting keeps enough source brightness to avoid whole-bike dimming");
  var water0=Draw(Surface(clear,2,0));var water1=Draw(Surface(clear,2,3));
  Check(!water0.AsSpan().SequenceEqual(lit),"explicit flat water surface receives reflection/highlight shading");
  Check(!water0.AsSpan().SequenceEqual(water1),"only water shading responds to animation time");
  var waterCycle=Draw(Surface(clear,2,1000));
  Check(water0.Zip(waterCycle).Average(pair=>MathF.Abs(Bright(pair.First)-Bright(pair.Second)))<.1,
      "water clock wrap matches whole texture periods without a discontinuous reset");
  Check(Enumerable.Range(0,4096).All(i=>(water0[i]==Back)==(water1[i]==Back)),"animated water never displaces geometry or polygon boundaries");
  var warmSource=Draw(null,r:96,g:48,b:24);
  var warmWater=Draw(Surface(clear,2,1),r:96,g:48,b:24);
  Check(AvgChannel(warmWater,0)>AvgChannel(warmWater,5)&&AvgChannel(warmWater,5)>AvgChannel(warmWater,10),"water modulation preserves original source hue order instead of forcing cyan");
  Check(AvgBright(warmWater)<AvgBright(warmSource)*1.05f,"water modulation does not over-brighten the original material");
  var islandSource=Draw(null,r:32,g:16,b:112);
  var islandWater=Draw(Surface(clear,4,1),r:32,g:16,b:112);
  Check(AvgChannel(islandWater,10)>AvgChannel(islandWater,0)&&AvgChannel(islandWater,0)>AvgChannel(islandWater,5),"Island underlay preserves original blue/purple hue order");
  Check(AvgBright(islandWater)<AvgBright(islandSource)*1.05f,"Island underlay improvement does not blow out the original ocean color");
  Check(Draw(null).AsSpan().SequenceEqual(original),"untagged HUD/sky/rider/effect-style primitives remain exactly original after water draw");
  Check(Draw(new WorldSurface(Camera(clear),Vector3.Zero,Vector3.UnitZ,0,32,32,32)).AsSpan().SequenceEqual(original),"explicit material kind zero bypasses all lighting");
  var flatMasked=Draw(Surface(clear),true);Check((flatMasked[32*64+32]&0x8000)!=0,"world lighting retains original GPU mask bit");
  Check((Draw(Surface(clear),false)[32*64+32]&0x8000)==0,"unmasked lighting does not create mask bits");
  var invalid=new WorldSurface(Camera(clear),new(0,0,100),Vector3.UnitZ,2,32,32,32);
  Check(Draw(invalid).AsSpan().SequenceEqual(original),"behind-camera plane falls back without stretching, missing polygons, or borrowed coordinates");
  // Equal world positions through a shifted camera/projection center must reproduce water.
  var cam=Camera(clear,3,new Vector3(-10,0,64));var stable=new WorldSurface(cam,Vector3.Zero,Vector3.UnitZ,2,37,32,32);
  var reference=Surface(clear,2,3);bool sameWorld=true;
  for(int x=8;x<57;x+=8)for(int y=8;y<57;y+=8) { stable.AtPixel(x,y,out var wp,out _);reference.AtPixel(x,y,out var expected,out _);sameWorld &= Vector3.Distance(wp,expected)<.0001f; }
  Check(sameWorld,"camera movement and matching principal-point shift preserve exact world sampling positions (view-dependent reflection may legitimately change)");
  var horizonCamera=new WorldCamera(clear,new WorldBasis(1,0,0,0,0,-1,0,1,0),new(0,8,0),0);
  var horizonSurface=new WorldSurface(horizonCamera,Vector3.Zero,Vector3.UnitZ,4,32,32,32,screenFill:true);
  bool continuous=horizonSurface.ProjectiveAtPixel(8,20,out var above)
      && horizonSurface.ProjectiveAtPixel(8,44,out var below)
      && horizonSurface.ProjectiveAtPixel(8,32,out var horizon);
  horizonSurface.ProjectiveAtPixel(8,20,out above);
  horizonSurface.ProjectiveAtPixel(8,44,out below);
  horizonSurface.ProjectiveAtPixel(8,32,out horizon);
  Check(continuous&&Vector4.Distance((above+below)*.5f,horizon)<.00001f&&MathF.Abs(horizon.W)<.00001f,
      "water coordinates remain affine across horizon without fixed-depth substitution");
  var horizonWater=Draw(new WorldSurface(horizonCamera,Vector3.Zero,Vector3.UnitZ,2,32,32,32));
  Check(Enumerable.Range(36,20).Sum(y=>Enumerable.Range(8,16).Count(x=>horizonWater[y*64+x]!=original[y*64+x]))>20,
      "water triangle crossing the horizon keeps shading on its visible half");
  Check(horizonWater[12*64+12]==original[12*64+12],
      "water rays behind the plane retain source shading without borrowed coordinates");
  var horizonBackdrop=Draw(new WorldSurface(horizonCamera,Vector3.Zero,Vector3.UnitZ,4,32,32,32));
  Check(horizonBackdrop[12*64+12]==original[12*64+12],
      "source backdrop above water horizon is never replaced with ocean tint");
  var horizonUnderlay=Draw(horizonSurface);
  Check(horizonUnderlay[12*64+12]==Back&&horizonUnderlay[44*64+12]!=Back,
      "added underlay covers only rays intersecting water ahead of camera");
  var offsetSurface=new WorldSurface(Camera(clear,3),Vector3.Zero,Vector3.UnitZ,2,12,22,32);
  Check(Draw(offsetSurface,drawX:20,drawY:10).AsSpan().SequenceEqual(water1),"draw offsets/split view positions do not move the water pattern in world space");
  var twice=Draw(Surface(blocked));Check(twice.AsSpan().SequenceEqual(shadow),"world maps and per-camera uniforms cannot leak between scene batches");
  // Material selector, not RGB: identical blue-ish colors are unaffected unless explicitly tagged.
  Check(!Draw(Surface(clear,2,1),semi:true).AsSpan().SequenceEqual(Draw(null,semi:true)),"water still respects native semi-transparent draw path");
  Check(WorldSurfaceBindings.LitTriangles>0&&WorldSurfaceBindings.WaterTriangles>0,"renderer recorded both tagged surface paths");
 }
}
