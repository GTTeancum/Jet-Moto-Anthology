using RecompOne.Runtime;
using RecompOne.Runtime.Hle;
using RecompOne.Runtime.Memory;
using RecompOne.Runtime.Pgxp;
using RecompOne.Runtime.Config;
using System.Reflection;

int passed=0, failed=0;
void Check(bool condition, string name) { if(condition) {passed++; Console.WriteLine("PASS: "+name);} else {failed++;Console.Error.WriteLine("FAIL: "+name);} }
void Test(string name, Action run) { try {run();} catch(Exception ex){Check(false,name+": "+ex);} }
uint Pack(int x,int y)=>PgxpGte.PackXy(x,y);
Pgxp.RequirePerspectiveCorrection();
Test("mandatory game policy",()=> {
    Pgxp.Load();
    Check(Pgxp.RequiredPerspective && Pgxp.Enabled && Pgxp.TextureCorrection && Pgxp.CpuTracking && Pgxp.MemoryTracking,"config reload retains required perspective tracking");
    Check(!Pgxp.Shown && !Pgxp.Culling && !Pgxp.CullingCorrection && !Pgxp.VertexCache && !Pgxp.CacheW,"no user toggle, original culling, no screen-XY cache guessing");
});
var mem = new PSMemory(2*1024*1024);
PgxpValue Vertex(uint packed, float w=400,float dx=.25f,float dy=.375f) {
    PgxpGte.PushVertex((short)packed+dx,(short)(packed>>16)+dy,w,packed,7);
    return PgxpGte.Sxy2;
}
void Put(uint address, in PgxpValue v) { mem.WriteU32(address,v.Value); PgxpMemory.Store(address,in v,v.Value); }
bool Load(uint a,uint val,out float x,out float y,out float w,out bool valid) => PgxpMemory.TryLoad(a,val,out x,out y,out w,out valid,out _,out _);
bool HasDepth(uint a,uint v) => Load(a,v,out _,out _,out _,out var valid) && valid;
Test("memory addressing",()=> {
    var v=Vertex(Pack(12,-7)); Put(0x80001000,in v);
    foreach(uint a in new uint[]{0x1000,0x80001000,0xA0001000,0x80201000,0xA0601000})
        Check(HasDepth(a,v.Value),$"RAM/KSEG/mirror depth at {a:X8}");
    var scratch=Vertex(Pack(60,80),900); Put(0x1F800000,in scratch);
    var ram=Vertex(Pack(20,30),200); Put(0x80000000,in ram);
    Check(Load(0x1F800000,scratch.Value,out _,out _,out float sw,out bool valid)&&valid&&sw==900,"scratchpad has independent precision storage, not RAM alias");
    Check(HasDepth(0x9F800000,scratch.Value)&&HasDepth(0xBF800000,scratch.Value),"scratchpad segment mirrors retain depth");
    PgxpMemory.Store(0x1F801814,in ram,ram.Value);
    Check(!HasDepth(0x1F801814,ram.Value),"MMIO never accepts geometry metadata");
    Check(!HasDepth(0xBFC00000,ram.Value),"BIOS is not a shadow-RAM alias");
    Check(!HasDepth(0x80001001,v.Value),"unaligned packet pointer is rejected");
    Check(!HasDepth(0x80001000,v.Value+1),"different packed XY cannot borrow old metadata");
});
Test("same-value write invalidation",()=> {
    var v=Vertex(Pack(30,40)); const uint a=0x80002000;
    Put(a,in v); mem.WriteU32(a,v.Value); Check(!HasDepth(a,v.Value),"ordinary same-value SW invalidates stale depth");
    for(int byteOffset=0;byteOffset<4;byteOffset++) {
        Put(a,in v); mem.WriteU8(a+(uint)byteOffset,(byte)(v.Value>>(8*byteOffset)));
        Check(!HasDepth(a,v.Value),$"same-value byte write {byteOffset} invalidates the touched coordinate");
    }
    for(int half=0;half<2;half++) {
        Put(a,in v); mem.WriteU16(a+2u*(uint)half,(ushort)(v.Value>>(16*half)));
        Check(!HasDepth(a,v.Value),$"same-value half write {half} invalidates the touched coordinate");
    }
    Put(a,in v);PgxpMemory.InvalidateBytes(a+1,6);
    Check(!HasDepth(a,v.Value),"unaligned multiword write invalidates all touched coordinate halves");
});
Test("GTE->SWC2->LW/SW exact copies",()=> {
    var v=Vertex(Pack(-20,120),615); const uint a=0x1F800020,b=0x80003000;
    mem.WriteU32(a,v.Value); PgxpCpu.Swc2(14,a,v.Value);
    PgxpCpu.Lw(8,a,v.Value);mem.WriteU32(b,v.Value);PgxpCpu.Sw(8,b,v.Value);
    Check(Load(b,v.Value,out float x,out float y,out float w,out bool valid)&&valid&&w==615&&x==-19.75f&&y==120.375f,"scratchpad CPU copy preserves fractional XY and true depth");
    PgxpCpu.Lw(9,0x1F801810,v.Value); mem.WriteU32(b,v.Value);PgxpCpu.Sw(9,b,v.Value);
    Check(!HasDepth(b,v.Value),"MMIO load cannot reuse previous register depth");
});
Test("architectural zero register",()=> {
    var v=Vertex(Pack(0,0),900);const uint a=0x80004500,b=0x80004510;Put(a,in v);
    PgxpCpu.Lw(0,a,0);PgxpCpu.Lh(0,a,0);PgxpCpu.Mfc2(0,14,0);
    mem.WriteU32(b,0);PgxpCpu.Sw(0,b,0);
    Check(!HasDepth(b,0),"discarded loads and MFC2 cannot turn the constant zero register into geometry");
});
Test("split LH/LHU and SH copies",()=> {
    var v=Vertex(Pack(-12,21),700); const uint a=0x80004000,b=0x1F800030;
    Put(a,in v);
    uint lo=unchecked((uint)(int)(short)v.Value), hi=(uint)(ushort)(v.Value>>16);
    PgxpCpu.Lh(8,a,lo);mem.WriteU16(b,(ushort)lo);PgxpCpu.Sh(8,b,lo);
    PgxpCpu.Lh(9,a+2,hi);mem.WriteU16(b+2,(ushort)hi);PgxpCpu.Sh(9,b+2,hi);
    Check(Load(b,v.Value,out float x,out float y,out float w,out bool valid)&&valid&&w==700&&x==-11.75f&&y==21.375f,"signed/unsigned half loads reassemble same-vertex depth");
    var other=Vertex(Pack(-12,21),700);Put(a+4,in other);
    PgxpCpu.Lh(9,a+6,hi);mem.WriteU16(b+2,(ushort)hi);PgxpCpu.Sh(9,b+2,hi);
    Check(!HasDepth(b,v.Value),"different vertices with identical XY/Z cannot combine halfword origins");
});
Test("halfword updates of reused vertex buffers",()=> {
    const uint a=0x80004600,b=0x80004610;
    foreach(bool highFirst in new[]{false,true})
    {
        var old=Vertex(Pack(25,60),300);Put(b,in old);
        var fresh=Vertex(Pack(25,60),900);Put(a,in fresh);
        foreach(int half in highFirst?new[]{1,0}:new[]{0,1})
        {
            uint value=(uint)(ushort)(fresh.Value>>(16*half));
            PgxpCpu.Lh(8,a+2u*(uint)half,value);
            mem.WriteU16(b+2u*(uint)half,(ushort)value);PgxpCpu.Sh(8,b+2u*(uint)half,value);
        }
        Check(Load(b,fresh.Value,out _,out _,out float z,out bool valid)&&valid&&z==900,
            $"rewritten half pair recovers NEW vertex depth (high first={highFirst})");
    }
});
Test("CPU geometry versus scalar provenance",()=> {
    var v=Vertex(Pack(30,40),500);const uint a=0x80005000,b=0x80006000;
    void Store(int r,uint val) {mem.WriteU32(b,val);PgxpCpu.Sw(r,b,val);}
    Put(a,in v);PgxpCpu.Lw(8,a,v.Value);PgxpCpu.Move(9,8,v.Value);Store(9,v.Value);
    Check(HasDepth(b,v.Value),"register move preserves vertex");
    PgxpCpu.Slti(9,8,42,0,v.Value);Store(9,0);Check(!HasDepth(b,0),"SLTI result is scalar, not a projected vertex");
    PgxpCpu.Sltiu(9,8,42,0,v.Value);Store(9,0);Check(!HasDepth(b,0),"SLTIU result is scalar");
    PgxpCpu.Const(10,1);PgxpCpu.Slt(9,8,10,0,v.Value,1);Store(9,0);Check(!HasDepth(b,0),"SLT result is scalar");
    PgxpCpu.Sltu(9,8,10,0,v.Value,1);Store(9,0);Check(!HasDepth(b,0),"SLTU result is scalar");
    PgxpCpu.Mult(8,10,0,v.Value,v.Value,1,true);PgxpCpu.Mflo(9,v.Value,v.Value);Store(9,v.Value);Check(!HasDepth(b,v.Value),"multiplication result cannot inherit an unrelated vertex depth");
    PgxpCpu.Div(8,10,0,v.Value,v.Value,1,true);PgxpCpu.Mflo(9,v.Value,v.Value);Store(9,v.Value);Check(!HasDepth(b,v.Value),"division result cannot inherit an unrelated vertex depth");
    uint lo=v.Value&0xFFFF,hi=v.Value&0xFFFF0000;
    PgxpCpu.Andi(9,8,0xFFFF,lo,v.Value);
    PgxpCpu.Srl(10,8,16,v.Value>>16,v.Value);PgxpCpu.Sll(10,10,16,hi,v.Value>>16);
    PgxpCpu.Bitwise(11,9,10,v.Value,lo,hi);Store(11,v.Value);
    Check(Load(b,v.Value,out float x,out float y,out float w,out bool valid)&&valid&&w==500&&x==30.25f&&y==40.375f,"AND/SRL/SLL/OR reassembly retains coherent origin");
    var v2=Vertex(Pack(30,40),500);Put(a+4,in v2);PgxpCpu.Lw(12,a+4,v2.Value);PgxpCpu.Srl(12,12,16,v2.Value>>16,v2.Value);PgxpCpu.Sll(12,12,16,hi,v2.Value>>16);
    PgxpCpu.Bitwise(11,9,12,v.Value,lo,hi);Store(11,v.Value);Check(!HasDepth(b,v.Value),"OR cannot merge halves from distinct projected vertices");
    PgxpCpu.Andi(9,8,1,0,v.Value);Store(9,0);Check(!HasDepth(b,0),"destructive coordinate masks clear geometry depth");
});
Test("GTE fractional projection without architectural changes",()=> {
    void Init() { for(int i=0;i<32;i++){Gte.WriteControl(i,0);Gte.Write(i,0);}Gte.WriteControl(0,4096);Gte.WriteControl(2,4096);Gte.WriteControl(4,4096);Gte.WriteControl(24,160u<<16);Gte.WriteControl(25,120u<<16);Gte.WriteControl(26,160); }
    Init();Gte.WriteControl(0,4097);Gte.Write(0,Pack(123,43));Gte.Write(1,321);Gte.Rtps(12,false);
    var v=PgxpGte.Sxy2;
    float expectedX=160+(123*4097f/4096f)*160/321;
    float expectedY=120+43f*160/321;
    Check(Math.Abs(v.X-expectedX)<.0001f&&Math.Abs(v.Y-expectedY)<.0001f&&v.Z==321,"fractional transformed position retained before integer IR rounding");
    Check(v.Value==Gte.Read(14)&&Gte.Read(9)==123&&Gte.Read(10)==43&&Gte.Read(11)==321,"architectural GTE integer registers and packed XY are unchanged");
    Init();Gte.Write(0,Pack(0,0));Gte.Write(1,0);Gte.Rtps(12,false);Check(PgxpGte.Sxy2.Z==80&&float.IsFinite(PgxpGte.Sxy2.X),"near-zero depth uses hardware projection limit without infinity");
    Init();Gte.WriteControl(26,0);Gte.Rtps(12,false);Check(float.IsFinite(PgxpGte.Sxy2.X)&&float.IsFinite(PgxpGte.Sxy2.Y),"zero projection distance produces no NaN screen coordinates");
});
var recorder=new Recorder();GpuHle.Backend=recorder;
Test("GPU packet address propagation and primitive safety",()=> {
    uint a=0x80010000; var gpu=new Gpu();
    uint[] Tri() => [0x24808080u,Pack(8,8),0,Pack(40,8),0x011A0020u,Pack(8,40),0x2000];
    void Seed(uint[] words,uint addr,int prefix=0) {
        for(int i=0;i<words.Length;i++)mem.WriteU32(addr+4u*(uint)i,words[i]);
        for(int i=0;i<3;i++){int k=prefix+1+2*i;var v=Vertex(words[k],200+200*i);PgxpMemory.Store(addr+4u*(uint)k,in v,words[k]);}
    }
    bool LastPerspective()=>recorder.Triangles.Last().All(v=>v.HasGteZ)&&recorder.Triangles.Last().Select(v=>v.Z).SequenceEqual(new float[]{200,400,600});
    var t=Tri();Seed(t,a);gpu.WriteGp0Packet(t,a);Check(LastPerspective(),"single GP0 packet forwards exact per-vertex depth");
    var multi=new uint[]{0xE100011A}.Concat(t).Concat(t).ToArray();Seed(multi,a,1);
    for(int i=0;i<3;i++){int k=9+2*i;var v=Vertex(multi[k],200+200*i);PgxpMemory.Store(a+4u*(uint)k,in v,multi[k]);}
    int count=recorder.Triangles.Count;gpu.WriteGp0Packet(multi,a);
    Check(recorder.Triangles.Count==count+2 && LastPerspective()&&recorder.Triangles[^2].All(v=>v.HasGteZ),"multi-command OT packet advances its source address for every word");
    Seed(t,a);gpu.WriteGp0Packet(t.AsSpan(0,3),a);gpu.WriteGp0Packet(t.AsSpan(3),a+12);Check(LastPerspective(),"split contiguous FIFO write retains provenance");
    Seed(t,a);gpu.WriteGp0Packet(t.AsSpan(0,3),a);gpu.WriteGp0Packet(t.AsSpan(3),a+64);Check(recorder.Triangles.Last().All(v=>!v.HasGteZ),"non-contiguous FIFO source uses safe full-triangle fallback");
    Seed(t,a);gpu.WriteGp0Packet(t);Check(recorder.Triangles.Last().All(v=>!v.HasGteZ),"unaddressed 2D packet never guesses from matching screen XY");
    Seed(t,a);var shifted=Vertex(t[3],400,3.5f,0f);PgxpMemory.Store(a+12,in shifted,t[3]);gpu.WriteGp0Packet(t,a);
    Check(LastPerspective() && recorder.Triangles.Last()[1].X==40f,
        "valid depth survives XY tolerance fallback without moving the original screen coordinate");
    Seed(t,a);mem.WriteU32(a+12,t[3]);gpu.WriteGp0Packet(t,a);Check(recorder.Triangles.Last().All(v=>!v.HasGteZ&&v.Z==1),"one missing corner cannot mix W=1 and true depth");
    foreach(float z in new float[]{0,-1,float.NaN,float.PositiveInfinity}) {
        Seed(t,a);var bad=Vertex(t[3],z);PgxpMemory.Store(a+12,in bad,t[3]);gpu.WriteGp0Packet(t,a);Check(recorder.Triangles.Last().All(v=>!v.HasGteZ&&float.IsFinite(v.Z)),"invalid depth "+z+" retains a complete safe polygon");
    }
    uint[] q=[0x2C808080,Pack(8,8),0,Pack(40,8),0x011A0020,Pack(8,40),0x2000,Pack(40,40),0x2020];
    for(int i=0;i<q.Length;i++)mem.WriteU32(a+(uint)i*4,q[i]);
    for(int i=0;i<3;i++){int k=1+2*i;var v=Vertex(q[k],200+200*i);PgxpMemory.Store(a+4u*(uint)k,in v,q[k]);}
    gpu.WriteGp0Packet(q,a);Check(recorder.Triangles[^1].Concat(recorder.Triangles[^2]).All(v=>!v.HasGteZ),"a quad with one missing corner falls back consistently across both triangles");
    Seed(t,a);gpu.WriteGp0Packet(t,a);Check(recorder.Triangles.Last()[0].X==8.25f&&recorder.Triangles.Last()[0].Y==8.375f,"precise subpixel XY reaches the rendering backend");
});
Test("verified world span clipping",()=> {
    const uint address=0x80018000;
    var gpu=new Gpu();
    var scene=new RecompOne.Runtime.Assets.Native.WorldScene("coverage-test",new byte[16],2,
        System.Numerics.Vector2.Zero,System.Numerics.Vector2.One,System.Numerics.Vector2.One,System.Numerics.Vector3.UnitZ);
    var camera=new RecompOne.Runtime.Assets.Native.WorldCamera(scene,RecompOne.Runtime.Assets.Native.WorldBasis.Identity,
        new System.Numerics.Vector3(0,0,10),0);
    RecompOne.Runtime.Assets.Native.WorldSurfaceBindings.Enabled=true;
    RecompOne.Runtime.Assets.Native.WorldSurfaceBindings.Init(2*1024*1024);
    foreach(var corners in new[]{new[]{Pack(42,204),Pack(301,733),Pack(283,250)},new[]{Pack(-700,150),Pack(700,200),Pack(0,250)}})
    {
        uint[] packet=[0x24808080,corners[0],0,corners[1],0x011A0020,corners[2],0x2000];
        foreach(int kind in new[]{0,1,2,3,4})
        {
            for(int i=0;i<packet.Length;i++)mem.WriteU32(address+(uint)i*4,packet[i]);
            RecompOne.Runtime.Assets.Native.WorldSurfaceBindings.Init(2*1024*1024);
            if(kind!=0)RecompOne.Runtime.Assets.Native.WorldSurfaceBindings.Bind(address,packet.Length,
                new(camera,System.Numerics.Vector3.Zero,System.Numerics.Vector3.UnitZ,kind,160,120,160));
            int before=recorder.Triangles.Count;
            gpu.WriteGp0Packet(packet,address);
            bool expected=kind is 1 or 2 or 4;
            Check(recorder.Triangles.Count==before+(expected?1:0),$"oversized primitive kind {kind} retains its intended clipping policy");
            if(expected)Check(recorder.Triangles[^1].Select(v=>(v.X,v.Y)).SequenceEqual(corners.Select(p=>((float)(short)p,(float)(short)(p>>16)))),
                "world clipping preserves original projected geometry without displacement");
        }
    }
    RecompOne.Runtime.Assets.Native.WorldSurfaceBindings.Init(2*1024*1024);
});
Test("reset safety",()=> {
    var v=Vertex(Pack(20,30));Put(0x80001000,in v);_ = new PSMemory();
    Check(!HasDepth(0x80001000,v.Value)&&PgxpGte.Sxy2.Flags==0,"new memory/game session clears precision shadow and GTE FIFO");
});
Console.WriteLine($"RESULT: {passed} passed; {failed} failed.");return failed==0?0:1;

sealed class Recorder:IGpuBackend {
    public List<HleVertex[]> Triangles=new();public bool Ready=>true;
    public void SetDrawEnv(in HleDrawEnv e){}
    public void DrawTri(in HleVertex a,in HleVertex b,in HleVertex c,in PrimFlags f)=>Triangles.Add([a,b,c]);
    public void DrawRect(in HleRect r,in PrimFlags f){}public void DrawLine(in HleVertex a,in HleVertex b,in PrimFlags f){}
    public void FillRect(int x,int y,int w,int h,ushort c){} public void CopyVram(int sx,int sy,int dx,int dy,int w,int h){}
    public void WriteVram(int x,int y,int w,int h,ReadOnlySpan<ushort> p){}public void ReadVram(int x,int y,int w,int h,Span<ushort> p){}
    public int RegisterImage(ReadOnlySpan<byte> p,int w,int h)=>0;public void Flush(){}public void Present(in HleDispEnv d){}
}
