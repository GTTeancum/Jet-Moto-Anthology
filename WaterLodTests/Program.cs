using System.Buffers.Binary;
using JetMoto;

int passed=0;
void Check(bool condition,string label){if(!condition)throw new Exception(label);Console.WriteLine("PASS: "+label);passed++;}
byte[] Fixture(){
    var b=new byte[256];
    void U(int o,uint n)=>BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(o,4),n);
    U(12,0x80000000);U(24,1);U(28,0x80000040);
    b[64]=1;b[84]=2;U(88,0x80000070);U(92,0x800000C0);
    b[112]=2;U(128,1);U(132,0x80000098);
    U(152,160000);U(156,0);U(160,0x800000B0);
    b[176]=0;b[192]=0;
    return b;
}
var source=Fixture();
Check(WaterLodPolicy.FindSelectors(source,new HashSet<int>{176}).SetEquals([112]),"mixed root retains only its verified water selector");
Check(WaterLodPolicy.FindSelectors(source,new HashSet<int>{192}).Count==0,"non-water child does not qualify a selector");
var unknown=Fixture();unknown[176]=6;
Check(WaterLodPolicy.FindSelectors(unknown,new HashSet<int>{176}).Count==0,"unknown selector descendants fail closed even if listed as water");
var malformed=Fixture();BinaryPrimitives.WriteUInt32LittleEndian(malformed.AsSpan(160,4),0xFFFFFFFF);
Check(WaterLodPolicy.FindSelectors(malformed,new HashSet<int>{176}).Count==0,"out-of-range source pointer fails closed");
var cycle=Fixture();BinaryPrimitives.WriteUInt32LittleEndian(cycle.AsSpan(160,4),0x80000070);
Check(WaterLodPolicy.FindSelectors(cycle,new HashSet<int>{176}).Count==0,"recursive selector fails closed");
var invalidRange=Fixture();BinaryPrimitives.WriteUInt32LittleEndian(invalidRange.AsSpan(156,4),160000);
Check(WaterLodPolicy.FindSelectors(invalidRange,new HashSet<int>{176}).Count==0,"empty native distance interval fails closed");
Check(WaterLodPolicy.FindSelectors(new byte[8],new HashSet<int>()).Count==0,"short source fails closed");
Check(WaterLodPolicy.ExtendDistance(160000*16-1)<160000,"four-times linear range includes its inner endpoint");
Check(WaterLodPolicy.ExtendDistance(160000*16)==160000,"four-times linear range preserves exclusive outer endpoint");
Check(WaterLodPolicy.ExtendDistance(uint.MaxValue)==268435455,"distance scaling cannot overflow");
Check(source.AsSpan().SequenceEqual(Fixture()),"source graph is never modified");
var animated=Fixture();Array.Resize(ref animated,384);animated[176]=3;animated[187]=2;
BinaryPrimitives.WriteUInt32LittleEndian(animated.AsSpan(208,4),0x8000012C);
BinaryPrimitives.WriteUInt32LittleEndian(animated.AsSpan(212,4),0x80000140);
Check(WaterLodPolicy.FindSelectors(animated,new HashSet<int>{300,320}).SetEquals([112]),"all original animation variants must qualify as water");
Check(WaterLodPolicy.FindSelectors(animated,new HashSet<int>{300}).Count==0,"one non-water animation variant rejects the whole selector");
Console.WriteLine($"RESULT: {passed} passed; 0 failed.");
if(args.Length==2){
    byte[] original=File.ReadAllBytes(args[0]);
    using var stream=File.OpenRead(args[1]);
    using var gzip=new System.IO.Compression.GZipStream(stream,System.IO.Compression.CompressionMode.Decompress);
    using var catalog=System.Text.Json.JsonDocument.Parse(gzip);
    var meshes=catalog.RootElement.GetProperty("meshes").EnumerateArray().Where(m=>{
        var polys=m.GetProperty("polygons");int offset=m.GetProperty("offset").GetInt32();
        return polys.GetArrayLength()>0&&polys.EnumerateArray().All(p=>p[1].GetInt32()==2)
            &&polys.GetArrayLength()==BinaryPrimitives.ReadUInt32LittleEndian(original.AsSpan(offset+16,4));
    }).Select(m=>m.GetProperty("offset").GetInt32()).ToHashSet();
    var selectors=WaterLodPolicy.FindSelectors(original,meshes,Console.WriteLine);
    Console.WriteLine($"SOURCE: {meshes.Count} water-only meshes, {selectors.Count} eligible selectors.");
}
