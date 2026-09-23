using System.Runtime.CompilerServices;
using System.Threading;
using RecompOne.Runtime.Context;
using RecompOne.Runtime.Memory;

namespace JetMoto;

/// <summary>
/// Relocates the original CPU visibility/matrix work arrays together. Raising
/// the PS1's 280/200 constants in place corrupts neighboring game structures.
/// This arena leaves 2 MiB RAM mirrors, original assets, VRAM and ordering semantics unchanged. GPU packets use
/// an explicitly registered, independent host command region. It is not a texture replacement hack.
/// </summary>
public static class RenderArena
{
    public const uint MatrixCapacity=1024, SceneCapacity=Widescreen.SceneCapacity;
    public const int ArenaSize=0x440000;
    private const uint Base=MemoryMap.HostScratchBase;
    public const uint SceneList=Base+0x100, MatrixPointers=Base+0x8400,
        ViewMatrices=Base+0xC500, LightMatrices=Base+0x14600,
        PlaneA=Base+0x1C700, PlaneB=Base+0x24800;
    private const uint OldScene=0x801E61A4,OldView=0x801E84AC,OldLight=0x801F0358;
    private static readonly (uint address,uint size)[] Regions =
        [(SceneList,SceneCapacity*32),(MatrixPointers,MatrixCapacity*16),
         (ViewMatrices,MatrixCapacity*32),(LightMatrices,MatrixCapacity*32),
         (PlaneA,MatrixCapacity*32),(PlaneB,MatrixCapacity*32)];
    private sealed class State
    {
        public bool Initialized;
        public readonly List<(uint Start,uint End)> Commands=[];
    }
    public const uint CommandOffset=0x40000,CommandStride=0x80000;
    public const int CommandSlots=8;
    private static int _packetBytesMax,_commandSlots;
    public static uint PacketStart(int slot)=>Base+CommandOffset+(uint)slot*CommandStride+64;
    public static uint PacketLimit(int slot)=>Base+CommandOffset+(uint)(slot+1)*CommandStride-320;
    private static readonly ConditionalWeakTable<PSMemory,State> States = new();
    private static long _views,_emissions,_guardChecks,_expandedViews;
    private static int _visibleMax,_matrixMax,_limitHits;
    public static string Diagnostics => $"renderArena[views={Interlocked.Read(ref _views)},emissions={Interlocked.Read(ref _emissions)},visibleMax={_visibleMax},matrixMax={_matrixMax},aboveOldLimit={Interlocked.Read(ref _expandedViews)},guardChecks={Interlocked.Read(ref _guardChecks)},limitHits={_limitHits},commandSlots={_commandSlots},packetBytesMax={_packetBytesMax}]";
    private static void Ensure(PSMemory mem)
    {
        mem.EnsureHostScratch(ArenaSize);
        mem.EnableHostGpuRange(CommandOffset,CommandStride*CommandSlots);
        var state=States.GetOrCreateValue(mem);
        if (state.Initialized) return;
        foreach(var (p,size) in Regions) for(uint i=0;i<64;i+=4)
        { mem.WriteU32(p-64+i,0xA5C3085Au);mem.WriteU32(p+size+i,0xA5C3085Au); }
        for(int slot=0;slot<CommandSlots;slot++)for(uint i=0;i<64;i+=4)
        {mem.WriteU32(PacketStart(slot)-64+i,0xA5C3085Au);mem.WriteU32(PacketLimit(slot)+256+i,0xA5C3085Au);}
        state.Initialized=true;
    }
    public static void CheckGuards(PSMemory mem)
    {
        Ensure(mem);
        foreach(var (p,size) in Regions) for(uint i=0;i<64;i+=4)
            if(mem.ReadU32(p-64+i)!=0xA5C3085A || mem.ReadU32(p+size+i)!=0xA5C3085A)
                throw new InvalidDataException($"Jet Moto render arena guard damaged at 0x{p:X8}. Stop instead of corrupting game memory.");
        for(int slot=0;slot<CommandSlots;slot++)for(uint i=0;i<64;i+=4)
            if(mem.ReadU32(PacketStart(slot)-64+i)!=0xA5C3085A || mem.ReadU32(PacketLimit(slot)+256+i)!=0xA5C3085A)
                throw new InvalidDataException($"Jet Moto command buffer {slot} guard damaged.");
        Interlocked.Increment(ref _guardChecks);
    }
    public static uint LightBase(PSMemory mem) {Ensure(mem);return LightMatrices;}
    public static uint RedirectViewBase(PSMemory mem,uint address)
    {
        if(address!=OldView && address!=ViewMatrices) return address;
        Ensure(mem);return ViewMatrices;
    }
    public static void Configure(PSMemory mem,uint context)
    {
        if(context!=MemoryMap.ScratchpadBase) throw new InvalidDataException($"Unexpected render context 0x{context:X8}.");
        CheckGuards(mem);
        if(mem.ReadU32(context+0x20)!=OldScene || mem.ReadU32(context+0x18)!=OldView || mem.ReadU32(context+0x1C)!=OldLight)
            throw new InvalidDataException("Render initialization no longer matches the verified original.");
        mem.WriteU32(context+4,MatrixPointers);
        mem.WriteU32(context+0x18,ViewMatrices);mem.WriteU32(context+0x1C,LightMatrices);
        mem.WriteU32(context+0x20,SceneList);mem.WriteU32(context+0x44,PlaneA);mem.WriteU32(context+0x48,PlaneB);
        Interlocked.Increment(ref _views);
    }
    public static void CheckMatrixSlot(uint slot)
    {
        if(slot>=MatrixCapacity) Limit("matrix",slot);
        if(slot>_matrixMax)_matrixMax=(int)slot;
    }
    public static uint AllowMatrixIncrement(uint slot)
    {
        CheckMatrixSlot(slot);
        if(slot>=MatrixCapacity-2)Limit("nested matrix",slot);
        return 1;
    }
    public static uint AllowVisible(uint count)
    {
        if(count>=SceneCapacity)Limit("visible list",count);
        return 1;
    }
    private static void Limit(string what,uint count)
    {
        Interlocked.Increment(ref _limitHits);
        throw new InvalidDataException($"Jet Moto expanded {what} capacity reached ({count}); refusing silent geometry corruption.");
    }
    public static void PrepareEmission(CpuContext c,PSMemory mem)
    {
        CheckGuards(mem);
        if(c.A1!=OldScene && c.A1!=SceneList)throw new InvalidDataException("Unexpected native scene-list argument.");
        if(c.A3!=OldView && c.A3!=ViewMatrices)throw new InvalidDataException("Unexpected native view-matrix argument.");
        uint lighting=mem.ReadU32(c.SP+0x10);
        if(lighting!=OldLight && lighting!=LightMatrices)throw new InvalidDataException("Unexpected native lighting-matrix argument.");
        if(c.A2>SceneCapacity)Limit("emission",c.A2);
        if(c.A2>_visibleMax)_visibleMax=(int)c.A2;
        if(c.A2>280)Interlocked.Increment(ref _expandedViews);
        PrepareCommands(mem,c.A0);
        c.A1=SceneList;c.A3=ViewMatrices;mem.WriteU32(c.SP+0x10,LightMatrices);
        if(RecompOne.Runtime.Pgxp.Pgxp.CpuTracking)
        {RecompOne.Runtime.Pgxp.PgxpCpu.Invalidate(5);RecompOne.Runtime.Pgxp.PgxpCpu.Invalidate(7);}
        Interlocked.Increment(ref _emissions);
    }
    private static void PrepareCommands(PSMemory mem,uint context)
    {
        if((context&3)!=0 || !mem.IsWorkMemoryRange(context,128) || (context&0x1fffffff)>=MemoryMap.RamWindow)
            throw new InvalidDataException("Unexpected GPU-buffer context.");
        var state=States.GetOrCreateValue(mem);
        uint start=mem.ReadU32(context+4),cursor=mem.ReadU32(context+8),end=mem.ReadU32(context+12);
        int slot=-1;
        for(int i=0;i<state.Commands.Count;i++)
            if(start==PacketStart(i) || (start&0x1fffffffu)==(state.Commands[i].Start&0x1fffffffu))
            {slot=i;break;}
        if(slot<0)
        {
            if(start>=end || !mem.IsWorkMemoryRange(start,(long)end-start+256))
                throw new InvalidDataException($"Original GPU allocation failed verification: {start:X8}..{end:X8}.");
            slot=state.Commands.Count;if(slot>=CommandSlots)Limit("command allocations",(uint)slot);
            state.Commands.Add((start,end));_commandSlots=Math.Max(_commandSlots,slot+1);
        }
        var original=state.Commands[slot];
        if(end!=PacketLimit(slot) && (end&0x1fffffffu)!=(original.End&0x1fffffffu))
            throw new InvalidDataException($"Unexpected GPU allocation limit: context={context:X8}, end={end:X8}.");
        uint first=PacketStart(slot),last=PacketLimit(slot),next;
        if(cursor>=first && cursor<=last) next=cursor;
        else if((cursor&0x1fffffffu)>=(original.Start&0x1fffffffu) &&
                (cursor&0x1fffffffu)<=(original.End&0x1fffffffu)+64) next=first;
        else throw new InvalidDataException($"GPU cursor outside its verified allocation: context={context:X8}, cursor={cursor:X8}.");
        // Menu/subview contexts share an allocation and sometimes copy only its
        // cursor. Key by the verified original allocation, NOT context address.
        // Preserve a copied host cursor: resetting it would overwrite queued
        // packets from the other view. Already queued original-RAM 2D packets
        // retain their addresses and links; they are not copied or rehashed.
        mem.WriteU32(context+4,first);mem.WriteU32(context+8,next);mem.WriteU32(context+12,last);
    }
    public static void CheckPacketSpace(uint cursor,uint end)
    {
        if(cursor>end)Limit("draw commands",cursor);
        for(int slot=0;slot<CommandSlots;slot++)if(end==PacketLimit(slot))
        {int used=(int)(cursor-PacketStart(slot));if(used>_packetBytesMax)_packetBytesMax=used;return;}
        throw new InvalidDataException("Unexpected packet bound in expanded renderer.");
    }

}
