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
 var riderBounds=new RiderBounds();
 riderBounds.Include(new(10,20,30));riderBounds.Include(new(-4,22,15));riderBounds.Include(new(float.NaN,0,0));
 Check(riderBounds.Min==new Vector3(-4,20,15)&&riderBounds.Max==new Vector3(10,22,30)&&riderBounds.Samples==2,"rider bounds preserve finite world positions and reject corrupt geometry");
 camera.Riders[200]=riderBounds;
 var otherCamera=new WorldCamera(scene,basis,Vector3.Zero,6);
 Check(otherCamera.Riders.Count==0&&camera.Riders.Count==1,"rider ownership stays local to each camera snapshot");
 var ocean=new WorldScene("ISLAND1",height,2,new(-64,-64),new(128,128),new(0,64),new(-1,1,1),flatWaterLevel:-.125f,nativeBackdropLevel:-120f);
 Check(ocean.BackdropWaterLevel==-.125f,"verified flat ocean level takes priority over deep backdrop geometry for ripple sampling");
 var backdropOnly=new WorldScene("ISLAND3",height,2,new(-64,-64),new(128,128),new(0,64),new(-1,1,1),nativeBackdropLevel:-120f);
 Check(backdropOnly.FlatWaterLevel==null&&backdropOnly.BackdropWaterLevel==-120f&&scene.BackdropWaterLevel==null,"source backdrop level remains available without inventing a near-water level");
 WorldCamera Moving(float time,float x,int actors=1,int slot=0,WorldScene? world=null) {
  var frame=new WorldCamera(world??scene,basis,Vector3.Zero,time,slot);
  for(int actor=0;actor<actors;actor++) {
   var b=new RiderBounds();b.Include(new(x,actor*10,3));b.Include(new(x,actor*10,5));frame.Riders[200+actor]=b;
  }
  return frame;
 }
 var paths=new RiderMotionHistory();
 Check(paths.Observe(Moving(0,0,20)).Count==0,"new actors do not leave uninitialized path segments");
 var movingFrame=Moving(.1f,6,20);var path=paths.Observe(movingFrame);
 Check(path.Count==80&&path.Select(s=>s.RiderId).Distinct().Count()==20,"all twenty racers receive independent distance-sampled paths");
 Check(ReferenceEquals(path,paths.Observe(movingFrame)),"repeated batches do not advance a camera's path history");
 Check(path.All(s=>MathF.Abs(Vector3.Distance(s.A,s.B)-1.5f)<.0001f&&s.BornB>s.BornA),"path samples carry physical spacing and interpolated birth times");
 paths.Observe(Moving(.2f,6,20));
 for(int step=3;step<=20;step++)paths.Observe(Moving(step*.1f,6,20));
 Check(paths.Observe(Moving(2.1f,6,20)).Count==0,"stationary racers do not refresh old path lifetime");
 Check(paths.Observe(Moving(2.2f,6000,20)).Count==0,"teleports do not bridge across the course");
 paths.Observe(Moving(2.3f,6006,20));
 Check(paths.Observe(Moving(2.7f,6012,20)).Count==0,"reacquired actors do not connect an unobserved interval");
 paths.Observe(Moving(2.8f,6018,20));
 Check(paths.Observe(Moving(0,0,20)).Count==0,"clock reset clears stale paths");
 paths.Observe(Moving(.1f,6,20));
 Check(paths.Observe(Moving(.2f,12,20,world:ocean)).Count==0,"track change clears the previous scene's paths");
 paths.Observe(Moving(.3f,18,20,world:ocean));
 Check(paths.Observe(Moving(.4f,24,20,slot:1,world:ocean)).Count==0,"different camera slots cannot cross-connect riders");
 var dense=new RiderMotionHistory();var sparse=new RiderMotionHistory();
 dense.Observe(Moving(0,0));sparse.Observe(Moving(0,0));
 for(int step=1;step<=10;step++)dense.Observe(Moving(step*.01f,step*.6f));
 var densePath=dense.Observe(Moving(.1f,6));var sparsePath=sparse.Observe(Moving(.1f,6));
 Check(densePath.Count==sparsePath.Count&&densePath.Zip(sparsePath).All(p=>Vector3.Distance(p.First.B,p.Second.B)<.0001f),"path spacing is independent of observation frequency for the same motion");
 Check(sparse.Observe(Moving(2,0,actors:0)).Count==0,"unobserved actors expire without retaining stale trails");
 var bounded=new RiderMotionHistory();bounded.Observe(Moving(0,0));
 for(int step=1;step<=100;step++)bounded.Observe(Moving(step*.05f,step*45f));
 Check(bounded.Observe(Moving(5,4500)).Count<=320,"high-speed history has a strict per-racer storage bound");
 byte[] wakePixels=new byte[WaterWakeField.Size*WaterWakeField.Size*4];
 var wakeCamera=new WorldCamera(ocean,basis,new(0,0,57.6f),.25f);
 RiderPathSegment[] wakePath=[new(200,new(-20,0,4),new(0,0,4),0,.1f,0,20)];
 Check(WaterWakeField.Rasterize(wakeCamera,wakePath,wakePixels)==1&&wakePixels.Any(p=>p!=0),"near-water moving path produces a world-space wake field");
 var wakeOrigin=WaterWakeField.Origin(wakeCamera);
 byte WakeAt(float x,float y,int channel=0) {
  int px=(int)((x-wakeOrigin.X)*WaterWakeField.Size/WaterWakeField.Extent),py=(int)((y-wakeOrigin.Y)*WaterWakeField.Size/WaterWakeField.Extent);
  return wakePixels[(py*WaterWakeField.Size+px)*4+channel];
 }
 Check(WakeAt(-10,0,1)>0&&WakeAt(-10,20)==0&&WakeAt(20,0)==0,"world-space wake follows the recorded trail and leaves remote receivers clear");
 Check(WaterWakeField.Rasterize(wakeCamera,[new(200,new(-20,0,25),new(0,0,25),0,.1f,0,20)],wakePixels)==0&&wakePixels.All(p=>p==0),"airborne paths do not project foam onto distant water");
 Check(WaterWakeField.Rasterize(wakeCamera,[new(200,new(-20,0,-5),new(0,0,-5),0,.1f,0,20)],wakePixels)==0,"below-surface paths do not become surface foam");
 Check(WaterWakeField.Rasterize(new WorldCamera(ocean,basis,Vector3.Zero,3),wakePath,wakePixels)==0,"expired paths leave no wake field");
 Check(WaterWakeField.Rasterize(new WorldCamera(scene,basis,Vector3.Zero,.25f),wakePath,wakePixels)==0,"unknown water level cannot create an invented receiver");
 var spray=RiderSpray.Evaluate(wakeCamera,wakePath);
 var roadHeight=new byte[16];
 for(int i=0;i<16;i+=4){roadHeight[i]=8;roadHeight[i+2]=255;}
 var road=new WorldScene("road",roadHeight,2,new(-64,-64),new(128,128),new(0,64),Vector3.UnitZ,flatWaterLevel:-.125f);
 var roadCamera=new WorldCamera(road,basis,Vector3.Zero,.25f);
 Check(RiderSpray.Evaluate(roadCamera,wakePath).Count==0,"solid road rejects water spray despite emitter proximity to water height");
 Check(WaterWakeField.Rasterize(roadCamera,wakePath,wakePixels)==0,"solid road rejects water foam at its birth location");
 Check(!ocean.AllowsWaterEmission(new(500,0,4)),"unknown terrain outside the source map cannot emit water effects");
 Check(spray.Count>0&&spray.All(p=>p.RiderId==200&&p.Position.Z>ocean.FlatWaterLevel&&p.Opacity>0&&p.Opacity<=1),"world-space spray rises above verified water with finite coverage");
 var sprayLater=RiderSpray.Evaluate(new WorldCamera(ocean,basis,Vector3.Zero,.3f),wakePath);
 Check(sprayLater.Count>0&&spray[0].Position!=sprayLater[0].Position,"spray follows ballistic motion independent of the camera");
 Check(sprayLater.Max(p=>p.Position.Z)>4&&sprayLater.Max(p=>p.Position.Z)<12,"water fan rises through the hover-bike height range without moving its surface origin");
 Check(RiderSpray.Evaluate(wakeCamera,wakePath).SequenceEqual(spray),"spray evaluation is deterministic for repeated camera snapshots");
 Check(RiderSpray.Evaluate(new WorldCamera(ocean,basis,Vector3.Zero,2),wakePath).Count==0,"airborne spray expires without a native particle removal event");
 Check(RiderSpray.Evaluate(wakeCamera,[new(200,new(-20,0,25),new(0,0,25),0,.1f,0,20)]).Count==0,"airborne racers do not emit water spray onto distant water");
 Check(RiderSpray.Evaluate(new WorldCamera(scene,basis,Vector3.Zero,.25f),wakePath).Count==0,"spray requires verified water-level provenance");
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
