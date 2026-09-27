using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;
using RecompOne.Runtime.Assets.Native;

int checks = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; Console.WriteLine("PASS: " + name); }
var released = new List<string>();
var cache = new ResidentCache<int,string>(10, released.Add);
cache.Add(1,"one",4); cache.Add(2,"two",4); cache.TryGet(1,out _); cache.Add(3,"three",4);
Check(cache.Bytes == 8 && cache.TryGet(1,out _) && !cache.TryGet(2,out _) && released.SequenceEqual(["two"]), "least-recently-used entry is evicted within budget");
cache.Add(4,"large",20);
Check(cache.Bytes == 20 && cache.Count == 1 && cache.TryGet(4,out _), "one oversized image remains usable without retaining other entries");
cache.Add(5,"small",2);
Check(cache.Bytes == 2 && cache.Count == 1 && released.Contains("large"), "oversized resident is released when replaced");
cache.Clear(); Check(cache.Bytes == 0 && cache.Count == 0 && released.Count == 5, "every owned resource is released exactly once");

string png=Path.Combine(AppContext.BaseDirectory,"Fixtures/red.png");
var asset=new NativeTextureAsset("fixture",png,16,16);
var first=asset.GetTexture()!; string hash=Convert.ToHexString(SHA256.HashData(first.Rgba));
NativeTextureAsset.ClearDecodedCache();
var second=asset.GetTexture()!;
Check(!ReferenceEquals(first,second) && Convert.ToHexString(SHA256.HashData(second.Rgba))==hash, "evicted decoded image reloads identical pixels");
Check(first.Rgba.AsSpan().SequenceEqual(second.Rgba), "eviction does not mutate a previously queued image");
var corrupt=new NativeTextureAsset("bad-manifest",png,16,16){ExpectedSha256=new string('0',64)};
Check(corrupt.GetTexture()==null, "deferred authored checksum validation rejects changed content");
var verified=new NativeTextureAsset("valid-manifest",png,16,16){ExpectedSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(png)))};
Check(verified.GetTexture()!=null, "deferred authored checksum validation accepts exact content");

var scene=new WorldScene("test",new byte[16],2,new(-64),new(128),new(-16,80),new(-.65f,.05f,.76f));
var basis=new WorldBasis(1,0,0,0,-.9f,0,0,0,-.9f);
var camera=new WorldCamera(scene,basis,new(0,0,64),0);
camera.Casters.Add(new(new(-20,-20,12),new(20,-20,12),new(-20,20,12)));
camera.Casters.Add(new(new(20,-20,12),new(20,20,12),new(-20,20,12)));
var depths=new ushort[RiderShadowMap.Size*RiderShadowMap.Size];
var pixels=new byte[depths.Length*4];
RiderShadowMap.Rasterize(camera,depths,pixels);
byte[] expected=pixels.ToArray();
Check(expected.Any(x=>x!=0), "shadow fixture contains actual silhouette coverage");
Array.Fill(depths,ushort.MaxValue);Array.Fill(pixels,(byte)255);
RiderShadowMap.Rasterize(camera,depths,pixels);
Check(pixels.AsSpan().SequenceEqual(expected), "reused buffers clear stale depth and reproduce the same silhouette");
var empty=new WorldCamera(scene,basis,new(0,0,64),1);
RiderShadowMap.Rasterize(empty,depths,pixels);
Check(pixels.All(x=>x==0)&&depths.All(x=>x==0), "next empty camera cannot retain previous rider shadows");
long before=GC.GetAllocatedBytesForCurrentThread();var start=Stopwatch.GetTimestamp();
for(int i=0;i<60;i++)RiderShadowMap.Rasterize(camera,depths,pixels);
double elapsed=Stopwatch.GetElapsedTime(start).TotalMilliseconds;
long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
Console.WriteLine($"MEASURE: 60 shadow maps: {allocated} allocated bytes, {elapsed:F2} ms; previous arrays alone required {60L*6*1024*1024} bytes.");
Check(allocated<4096,"steady shadow rasterization allocates no large arrays");
foreach(float sy in new[]{.5f,.9f,1f,1.25f})foreach(float angle in new[]{0f,.3f,1.5f}) {
 float c=MathF.Cos(angle),s=MathF.Sin(angle);var r=new WorldBasis(c,-s,0,s*sy,c*sy,0,0,0,sy);
 var view=new WorldCamera(scene,r,new(10,20,30),0);
 Check(view.InverseRotation.Distance(r.Inverse)==0,"cached inverse exactly matches scaled camera inverse");
}
Console.WriteLine($"PASS: {checks} performance regression checks.");
