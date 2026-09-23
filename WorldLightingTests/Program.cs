using System.Numerics;
using RecompOne.Runtime.Assets.Native;
using RecompOne.Runtime.Memory;
int pass=0,fail=0;void Check(bool ok,string text){if(ok){pass++;Console.WriteLine("PASS: "+text);}else{fail++;Console.Error.WriteLine("FAIL: "+text);}}
try{
 foreach(float sy in new[]{.5f,.9f,1f,1.25f})foreach(float angle in new[]{0f,.3f,1.5f}){
  float c=MathF.Cos(angle),s=MathF.Sin(angle);var r=new WorldBasis(c,-s,0,s*sy,c*sy,0,0,0,sy);var v=new Vector3(15,-2,7);
  Check(r.IsCameraTransform,"native scaled camera matrix is invertible");
  Check(Vector3.Distance(r.Inverse.Apply(r.Apply(v)),v)<.0001f,"true inverse preserves world point at scale "+sy);
  Check((r*r.Inverse).Distance(WorldBasis.Identity)<.0001f,"row-major matrix composition inverse identity");
 }
 var height=new byte[16];var scene=new WorldScene("unit",height,2,new(-64,-64),new(128,128),new(0,64),new(-1,1,1));
 var basis=new WorldBasis(1,0,0,0,-.9f,0,0,0,-.9f);var camera=new WorldCamera(scene,basis,new(0,0,57.6f),5);
 Check(Vector3.Distance(camera.Eye,new Vector3(0,0,64))<.0001f,"camera eye accounts for original nonunit projection rows");
 var surface=new WorldSurface(camera,Vector3.Zero,Vector3.UnitZ,2,160,120,160);
 foreach(int x in new[]{-100,0,160,320,450})foreach(int y in new[]{0,120,240}){
  bool ok=surface.AtPixel(x,y,out var world,out var inv);var v=camera.Rotation.Apply(world)+camera.Translation;
  Check(ok&&MathF.Abs(world.Z)<.0001f&&MathF.Abs(v.X/v.Z*160+160-x)<.0002&&MathF.Abs(v.Y/v.Z*160+120-y)<.0002&&MathF.Abs(inv*v.Z-1)<.0001,"plane reconstruction reprojects to original screen, including widescreen edges");
 }
 Check(!surface.AtPixel(float.NaN,10,out _,out _),"invalid screen coordinate rejected");
 Check(!new WorldSurface(camera,Vector3.Zero,Vector3.UnitZ,2,160,120,0).AtPixel(160,120,out _,out _),"zero projection rejected");
 Check(!new WorldSurface(camera,new(0,0,100),Vector3.UnitZ,2,160,120,160).AtPixel(160,120,out _,out _),"behind-camera plane rejected");
 WorldSurfaceBindings.Enabled=true;var mem=new PSMemory(0x200000);
 foreach(uint address in new uint[]{0x2000,0x80002000,0xa0002000,0x80602000}){
  Check(WorldSurfaceBindings.Bind(0x80002000,9,surface)&&ReferenceEquals(WorldSurfaceBindings.Resolve(address),surface),"native RAM mirrors preserve exact surface identity");
 }
 Check(WorldSurfaceBindings.Resolve(0x2004)==null,"interior command word cannot impersonate source identity");
 WorldSurfaceBindings.Bind(0x2000,9,surface);mem.WriteU32(0x1ffc,0x9000000);Check(WorldSurfaceBindings.Resolve(0x2000)!=null,"ordering table link does not erase surface material");
 for(uint i=0;i<36;i++){
  WorldSurfaceBindings.Bind(0x2000,9,surface);mem.WriteU8(0x2000+i,mem.ReadU8(0x2000+i));Check(WorldSurfaceBindings.Resolve(0x2000)==null,"same-value payload write invalidates stale world transform "+i);
 }
 foreach(uint a in new uint[]{0x1ffffc,0x1f800000,0x1f801810,0xbfc00000,0x2001})Check(!WorldSurfaceBindings.Bind(a,9,surface),"invalid/cross-boundary/MMIO/scratch/BIOS binding refused "+a.ToString("X8"));
 WorldSurfaceBindings.ConfigureHostScratch(0x10000);Check(WorldSurfaceBindings.Bind(MemoryMap.HostScratchBase+0x100,9,surface)&&WorldSurfaceBindings.Resolve(MemoryMap.HostScratchBase+0x100)!=null,"expanded renderer workspace supports native surface identity");
 WorldSurfaceBindings.Invalidate(MemoryMap.HostScratchBase+0x110,4);Check(WorldSurfaceBindings.Resolve(MemoryMap.HostScratchBase+0x100)==null,"expanded workspace reuse invalidates old surface");
 WorldSurfaceBindings.Bind(0x2000,9,surface);var snapshot=WorldSurfaceBindings.Resolve(0x2000);mem.WriteU32(0x2000,0);Check(ReferenceEquals(snapshot,surface)&&WorldSurfaceBindings.Resolve(0x2000)==null,"queued immutable camera snapshot survives subsequent buffer reuse");
 WorldSurfaceBindings.Init(0x200000);Check(WorldSurfaceBindings.Resolve(0x2000)==null,"new session clears surface provenance");
 WorldSurfaceBindings.Enabled=false;Check(!WorldSurfaceBindings.Bind(0x2000,9,surface)&&WorldSurfaceBindings.Resolve(0x2000)==null,"disabled unconfigured runtime does not alter other games");
}catch(Exception e){Check(false,e.ToString());}
Console.WriteLine($"RESULT: {pass} passed; {fail} failed.");return fail==0?0:1;
