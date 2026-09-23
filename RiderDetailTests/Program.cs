using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using JetMoto;
using RecompOne.Runtime;
using RecompOne.Runtime.Assets.Native;
using RecompOne.Runtime.Cdrom;
using RecompOne.Runtime.Context;
using RecompOne.Runtime.Diagnostics;
using RecompOne.Runtime.Memory;
using RecompOne.Runtime.Pgxp;
using RecompOne.Runtime.Sdk;
using Original=Recompiled.Jet_Moto_main;
using R=RecompOne.Runtime.Runtime;

int passed=0,failed=0;
void Check(bool ok,string name){if(ok){passed++;Console.WriteLine("PASS: "+name);}else{failed++;Console.Error.WriteLine("FAIL: "+name);}}
void Test(string name,Action action){try{action();}catch(Exception e){Check(false,name+": "+e);}}
void Throws(Action a,string name){bool rejected=false;try{a();}catch(ArgumentException){rejected=true;}catch(InvalidOperationException){rejected=true;}catch(InvalidDataException){rejected=true;}Check(rejected,name);}
Pgxp.RequirePerspectiveCorrection();ExecutionTrace.StopRequested=false;ExecutionTrace.Enabled=false;
NativeTextureBindings.OriginalAssetsOnly=true;
var mem=new PSMemory(0x200000);
const uint H=MemoryMap.HostScratchBase;
Test("bounded independent workspace",()=>{
    Check(!mem.TryGpuWordAddress(H+RenderArena.CommandOffset,out _),"bit23 end marker unchanged before opt-in");
    mem.WriteU32(0x100,0x12345678);mem.WriteU32(0x1f800100,0xABCDEF01);
    mem.EnsureHostScratch(RenderArena.ArenaSize);mem.EnableHostGpuRange(RenderArena.CommandOffset,RenderArena.CommandStride*RenderArena.CommandSlots);
    mem.WriteU32(H+0x100,0xAABBCCDD);
    Check(mem.Ram.Length==0x200000 && mem.ReadU32(0x80200100)==0x12345678 && mem.ReadU32(0xa0600100)==0x12345678,"retail 2MiB and original 8MiB alias window preserved");
    Check(mem.ReadU32(0x1f800100)==0xABCDEF01 && mem.ReadU32(0x80800100)==0xAABBCCDD,"host, RAM and scratchpad independent; host KSEG alias consistent");
    Check(mem.IsWorkMemoryRange(H+RenderArena.ArenaSize-4,4)&&!mem.IsWorkMemoryRange(H+RenderArena.ArenaSize-4,8),"exact allocated host boundary");
    Check(!mem.IsWorkMemoryRange(0x1f801810,4)&&!mem.IsWorkMemoryRange(H,-1),"MMIO and negative lengths rejected");
    Check(mem.TryWords(H+0x100,1,out var span)&&span[0]==0xAABBCCDD,"fast host GPU packet span uses independent bytes");
    Check(!mem.TryWords(H+RenderArena.ArenaSize-4,2,out _),"fast packet span cannot cross host boundary");
    Check(!mem.TryWords(H+4,-1,out _)&&!mem.TryWords(4,-1,out _),"negative host and original RAM packet lengths rejected");
    Throws(()=>mem.EnsureHostScratch(RenderArena.ArenaSize+4),"workspace cannot resize under live pointers");
    Throws(()=>mem.EnableHostGpuRange(0,RenderArena.CommandStride),"registered command range cannot change silently");
    foreach(uint stop in new uint[]{0xffffff,0x800001,H+0x100,H+RenderArena.CommandOffset-4,H+RenderArena.ArenaSize,0xc50000})
        Check(!mem.TryGpuWordAddress(stop,out _),$"end marker or unregistered host address {stop:X6} rejected");
    for(int slot=0;slot<8;slot++)Check(mem.TryGpuWordAddress(RenderArena.PacketStart(slot),out uint p)&&p==RenderArena.PacketStart(slot),$"registered command slot {slot} resolves without RAM masking");
    Check(mem.TryGpuWordAddress(0xa0200101,out uint q)&&q==0x100,"legacy ordering link physical mask/alignment preserved");
});
Test("native asset and depth provenance in host packets",()=>{
    var asset=new NativeTextureAsset("synthetic-original-id","unused.png",16,16);var material=new NativeTextureMaterial(asset,0,0,16,16,0,0);
    uint p=RenderArena.PacketStart(0)+4;
    for(uint i=0;i<48;i++){
        Check(NativeTextureBindings.Bind(p,12,material),"bind original-ID host packet");
        mem.WriteU8(p+i,mem.ReadU8(p+i));Check(NativeTextureBindings.Resolve(p)==null,$"host payload byte {i} invalidates stale material even when unchanged");
    }
    NativeTextureBindings.Bind(p,9,material);mem.WriteU32(p-4,0x09ffffff);
    Check(ReferenceEquals(NativeTextureBindings.Resolve(p),material),"host ordering header cannot invalidate payload identity");
    Check(NativeTextureBindings.Resolve((p&0x1fffff))==null,"host native material cannot alias a RAM command");
    Check(!NativeTextureBindings.Bind(H+RenderArena.ArenaSize-4,9,material),"native payload cannot straddle host end");
    uint xy=PgxpGte.PackXy(-7,32);PgxpGte.PushVertex(-6.75f,32.375f,731,xy,9);var vertex=PgxpGte.Sxy2;
    uint a=RenderArena.ViewMatrices+64,b=RenderArena.PacketStart(1)+12;
    mem.WriteU32(a,xy);PgxpMemory.Store(a,in vertex,xy);
    PgxpCpu.Lw(8,a,xy);mem.WriteU32(b,xy);PgxpCpu.Sw(8,b,xy);
    bool valid=PgxpMemory.TryLoad(b,xy,out float x,out float y,out float z,out bool depth,out _,out _);
    Check(valid&&depth&&x==-6.75f&&y==32.375f&&z==731,"host matrix/CPU copy/host GPU packet retains exact subpixel/depth provenance");
    Check(!PgxpMemory.TryLoad(b&0x1fffff,xy,out _,out _,out _,out _,out _,out _),"host depth cannot alias original RAM");
    mem.WriteU16(b,(ushort)xy);Check(!PgxpMemory.TryLoad(b,xy,out _,out _,out _,out bool d,out _,out _)||!d,"same-value host halfword overwrite invalidates old full-vertex depth");
});
Test("real SDK and DMA linked-list paths",()=>{
    uint host=RenderArena.PacketStart(0),ot=0x80006000;
    void Packet(uint mode){mem.WriteU32(ot,host&0xffffff);mem.WriteU32(host,0x01ffffff);mem.WriteU32(host+4,0xe1000000|mode);}
    Packet(0x152);LibGpu.DrawOTag(new CpuContext{A0=ot},mem);
    Check((R.Gpu!.ReadStat()&0x1ff)==0x152,"SDK DrawOTag follows original RAM OT into real host command and executes it");
    Packet(0x6b);new Dma(mem,R.Gpu!,R.Spu!,R.Mdec!,()=>{}).Run(2,ot,0,0x01000401);
    Check((R.Gpu!.ReadStat()&0x1ff)==0x6b,"hardware DMA linked-list path executes registered host GPU command");
    mem.WriteU32(ot,0x800001);R.Gpu!.WriteGp0(0xe1000004);LibGpu.DrawOTag(new CpuContext{A0=ot},mem);
    Check((R.Gpu.ReadStat()&15)==4,"unregistered bit23 sentinel still terminates SDK list");
});
Test("render workspace and shared menu/race command contexts",()=>{
    const uint ctx=0x1f800000;
    void Init(){mem.WriteU32(ctx+0x20,0x801e61a4);mem.WriteU32(ctx+0x18,0x801e84ac);mem.WriteU32(ctx+0x1c,0x801f0358);RenderArena.Configure(mem,ctx);}
    Init();Check(mem.ReadU32(ctx+0x20)==RenderArena.SceneList&&mem.ReadU32(ctx+4)==RenderArena.MatrixPointers,"relocates both scene list and matrix-pointer array");
    Check(mem.ReadU32(ctx+0x18)==RenderArena.ViewMatrices&&mem.ReadU32(ctx+0x1c)==RenderArena.LightMatrices&&mem.ReadU32(ctx+0x44)==RenderArena.PlaneA&&mem.ReadU32(ctx+0x48)==RenderArena.PlaneB,"view/light/plane storage relocated together, not constants-only patch");
    for(uint i=0;i<1024;i++){mem.WriteU32(RenderArena.SceneList+32*i,i);mem.WriteU32(RenderArena.ViewMatrices+32*i,0x40000000+i);}
    RenderArena.CheckGuards(mem);Check(mem.ReadU32(RenderArena.SceneList+1023*32)==1023,"all 1024 visible slots fit with canaries intact");
    Check(RenderArena.AllowVisible(1023)==1&&RenderArena.AllowMatrixIncrement(1000)==1,"above-original scene/matrix counts allowed in allocated storage");
    Throws(()=>RenderArena.AllowVisible(1024),"scene overflow fails explicitly rather than corrupting next array");
    Throws(()=>RenderArena.AllowMatrixIncrement(1022),"nested matrix overflow retains reserved safety slots");
    mem.WriteU32(RenderArena.SceneList-4,0);Throws(()=>RenderArena.CheckGuards(mem),"damaged scene canary detected");mem.WriteU32(RenderArena.SceneList-4,0xA5C3085A);
    void Set(uint context,uint start,uint cursor,uint end){mem.WriteU32(context+4,start);mem.WriteU32(context+8,cursor);mem.WriteU32(context+12,end);}
    void Emit(uint context){var c=new CpuContext{A0=context,A1=0x801e61a4,A2=320,A3=0x801e84ac,SP=0x801fe000};mem.WriteU32(c.SP+16,0x801f0358);RenderArena.PrepareEmission(c,mem);Check(c.A1==RenderArena.SceneList&&c.A3==RenderArena.ViewMatrices&&mem.ReadU32(c.SP+16)==RenderArena.LightMatrices,"emission arguments point to expanded work arrays");}
    Set(0x80007000,0x80100000,0x80100100,0x80112000);Emit(0x80007000);
    uint first=mem.ReadU32(0x80007004),end=mem.ReadU32(0x8000700c);Check(first==RenderArena.PacketStart(0)&&end==RenderArena.PacketLimit(0),"first verified original allocation maps to bounded 512KiB command slot");
    Set(0x80007100,0x80100000,first+3000,0x80112000);Emit(0x80007100);
    Check(mem.ReadU32(0x80007104)==first&&mem.ReadU32(0x80007108)==first+3000,"menu/subview original base plus copied host cursor shares allocation without overwriting queued geometry");
    Set(0x80007200,0x80120000,0x80120000,0x80132000);Emit(0x80007200);
    Check(mem.ReadU32(0x80007204)==RenderArena.PacketStart(1),"second frame/camera original allocation gets independent host slot");
    Set(0x80007000,0x80100000,0x80100000,0x80112000);Emit(0x80007000);
    Check(mem.ReadU32(0x80007008)==first,"reinitialized original frame buffer starts existing host allocation afresh");
    RenderArena.CheckPacketSpace(first+130000,end);Check(true,"130KB draw stream exceeds old ~74KB bound without hitting new limit");
    Throws(()=>RenderArena.CheckPacketSpace(end+4,end),"expanded packet overflow cannot silently drop last riders");
    RenderArena.CheckGuards(mem);
});
if(args.Length==1) Test("original-disc LOD, pose and rotation checks",()=>{
    using var fs=DiscFs.Open(args[0]);NativeTextures.Configure(args[0],AppContext.BaseDirectory);
    byte[] exe=fs.ReadFile("SCUS_943.09");uint load=BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan(0x18,4));
    byte[] trig=exe.AsSpan(checked((int)(RiderDetail.SinCosTable-load+0x800)),4096*4).ToArray();mem.LoadBytes(RiderDetail.SinCosTable,trig);
    var rng=new Random(8008);var matrix=new short[9];bool rotations=true;
    for(int i=0;i<512;i++){
        short x=(short)rng.Next(short.MinValue,short.MaxValue+1),y=(short)rng.Next(short.MinValue,short.MaxValue+1),z=(short)rng.Next(short.MinValue,short.MaxValue+1);
        mem.WriteU16(0x80004000,(ushort)x);mem.WriteU16(0x80004002,(ushort)y);mem.WriteU16(0x80004004,(ushort)z);
        Original.RotMatrixYXZ(new CpuContext{A0=0x80004000,A1=0x80004100,SP=0x801ff000},mem);RiderDetail.Rotation(mem,x,y,z,matrix);
        for(uint k=0;k<9;k++)if(matrix[k]!=(short)mem.ReadU16(0x80004100+k*2))rotations=false;
    }
    Check(rotations,"512 random signed-angle matrices / 4608 elements bit-exact against original recompiled RotMatrixYXZ");
    using var index=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"original-rider-index.json")));
    int selectors=0,previews=0,seeded=0;const uint dest=0x80020000,binding=0x801a5000;
    foreach(var model in index.RootElement.EnumerateArray()){
        string name=model.GetProperty("source").GetString()!;byte[] data=fs.ReadFile(name);
        mem.LoadBytes(0x80001000,Encoding.ASCII.GetBytes(name+"\0"));var cpu=new CpuContext{V0=(uint)data.Length};
        using(var scope=NativeTextures.ObserveLoad(cpu,mem,dest,0x80001000)){mem.LoadBytes(dest,data);}
        foreach(var node in model.GetProperty("riders").EnumerateArray()){
            uint address=dest+node.GetProperty("offset").GetUInt32();selectors++;
            Check(RiderDetail.IsRaceSelector(mem,address)&&NativeTextures.IsOriginalRiderNode(address,9),$"{name} rider {node.GetProperty("id").GetInt32()}: native load provenance and four-level shape verified");
            Check(Enumerable.Range(0,4).All(l=>RiderDetail.SelectRaceLod(mem,address,(uint)l)==0&&RiderDetail.SelectDrawLod(mem,address,(uint)l)==0),"all four budget choices resolve to highest LOD, including draw fallback");
            Check(node.GetProperty("polygons")[0].GetInt32()==136&&node.GetProperty("polygons")[3].GetInt32()==4,"verified highest136 vs lowest4 polygon source meshes");
            mem.ZeroRange(binding,0x160);mem.WriteU32(binding+4,address);uint translation=dest+node.GetProperty("translation").GetUInt32();mem.WriteU32(binding+0x24,translation);
            uint[] joints=new uint[7];for(int j=0;j<7;j++){joints[j]=dest+node.GetProperty("joints").GetProperty((j+2).ToString()).GetUInt32();mem.WriteU32(binding+0x38+4u*(uint)j,joints[j]);}
            for(uint i=0;i<24;i++)mem.WriteU32(RiderDetail.NeutralPose+4+4*i,unchecked((uint)((i%2==0?1:-1)*(900+(int)i*73))));
            byte[] before=mem.Ram.Slice((int)(binding&0x1fffff),0x160).ToArray();mem.WriteU32(binding+0x38,0x80008000);
            Check(!RiderDetail.InitializeIdlePose(mem,binding)&&mem.ReadU32(binding+0x58)==0,"invalid bone cannot partially seed an empty pose");mem.WriteU32(binding+0x38,joints[0]);
            bool started=RiderDetail.InitializeIdlePose(mem,binding);Check(started,"empty articulated riding pose initialized from original neutral-pose format");if(started)seeded++;
            short[] expected=joints.SelectMany(j=>Enumerable.Range(0,9).Select(k=>(short)mem.ReadU16(j+4+(uint)k*2))).ToArray();
            uint[] position=Enumerable.Range(0,3).Select(k=>mem.ReadU32(translation+4+(uint)k*4)).ToArray();
            Original.func_80131FE0(new CpuContext{A0=binding,SP=0x801ff000},mem);
            Check(expected.SequenceEqual(joints.SelectMany(j=>Enumerable.Range(0,9).Select(k=>(short)mem.ReadU16(j+4+(uint)k*2))))&&position.SequenceEqual(Enumerable.Range(0,3).Select(k=>mem.ReadU32(translation+4+(uint)k*4))),"seeded joint matrices and translation bit-exact against original pose-application function");
            mem.WriteU32(binding+0x58,0x400);Check(!RiderDetail.InitializeIdlePose(mem,binding)&&mem.ReadU32(binding+0x58)==0x400,"normal animation/nonempty pose never overwritten");
            var other=model.GetProperty("riders")[node.GetProperty("id").GetInt32()==200?1:0];uint foreign=dest+other.GetProperty("joints").GetProperty("2").GetUInt32();
            Check(!NativeTextures.IsOriginalRiderBone(address,foreign,2),"another rider's bone in same original bank is rejected");
        }
        foreach(var node in model.GetProperty("previews").EnumerateArray()){
            uint address=dest+node.GetProperty("offset").GetUInt32();previews++;
            Check(RiderDetail.SelectPreviewDistance(mem,address,1224)==0,"verified PICKRIDE four-range chain retains highest preview");
        }
    }
    Check(selectors==200&&seeded==200&&previews==20,"10 original race banks / 200 riders and 20 menu previews covered");
    mem.ZeroRange(0x80008000,64);mem.WriteU8(0x80008000,9);mem.WriteU16(0x80008002,200);mem.WriteU16(0x80008004,1000);mem.WriteU8(0x80008006,4);
    Check(RiderDetail.SelectRaceLod(mem,0x80008000,3)==3,"lookalike node outside original loaded DMD cannot trigger override");
    Check(RiderDetail.SelectPreviewDistance(mem,0x80008000,729)==729,"unrelated scene/accessory LOD distances unchanged");
});else Console.WriteLine("Original-disc checks not run: pass the supported CUE as the only argument.");
Console.WriteLine($"RESULT rider-detail/render-arena: {passed} passed; {failed} failed.");return failed==0?0:1;
