using System.Threading;
using RecompOne.Runtime.Memory;

namespace JetMoto;

/// <summary>
/// SCUS-94309 rider/moto render quality. The 20 rider IDs (200..219) use
/// type-9 switches tagged 1000 with four children. Child zero contains 136
/// source polygons, compared with 49/50, 30/33 and four in the other slots.
/// Apply the choice before the original game copies the selected animated
/// branch, not by overwriting a finished GPU packet or shared VRAM page.
/// </summary>
public static class RiderDetail
{
    private static long _updates, _upgraded, _draws, _drawCorrections, _previews, _poseSeeds;
    private static int _matrixMax, _matrixLimitHits;
    private static int _riderMask;
    public static string Diagnostics => $"riderLod[updates={Interlocked.Read(ref _updates)},upgraded={Interlocked.Read(ref _upgraded)},draws={Interlocked.Read(ref _draws)},drawCorrections={Interlocked.Read(ref _drawCorrections)},preview={Interlocked.Read(ref _previews)},poseSeeds={Interlocked.Read(ref _poseSeeds)},matrixMax={_matrixMax},matrixLimitHits={_matrixLimitHits},ids=0x{Volatile.Read(ref _riderMask):X5}]";

    // Shape check is public for source-format regression tests. Production also
    // requires provenance in an ORIGINAL DMD whose actual load was verified.
    public static bool IsRaceSelector(PSMemory mem, uint node)
    {
        uint p = node & 0x1fffffffu;
        if ((p & 3) != 0 || p < 4 || (long)p + 32 > mem.Ram.Length) return false;
        return mem.ReadU8(node) == 9 && mem.ReadU16(node + 2) is >= 200 and < 220 &&
            mem.ReadU16(node + 4) == 1000 && mem.ReadU8(node + 6) == 4;
    }
    public static uint SelectRaceLod(PSMemory mem, uint node, uint requested)
    {
        if (!IsRaceSelector(mem, node) || !NativeTextures.IsOriginalRiderNode(node, 9)) return requested;
        Interlocked.Increment(ref _updates);
        if (requested != 0) Interlocked.Increment(ref _upgraded);
        Interlocked.Or(ref _riderMask, 1 << (mem.ReadU16(node + 2) - 200));
        return 0;
    }
    public static uint SelectDrawLod(PSMemory mem, uint node, uint requested)
    {
        if (!IsRaceSelector(mem, node) || !NativeTextures.IsOriginalRiderNode(node, 9)) return requested;
        Interlocked.Increment(ref _draws);
        if (requested != 0) Interlocked.Increment(ref _drawCorrections);
        return 0;
    }
    public static uint SelectPreviewDistance(PSMemory mem, uint node, uint distance)
    {
        if (!NativeTextures.IsOriginalRiderNode(node, 2)) return distance;
        // Only PICKRIDE's verified four-level rider chains, not scene LOD nodes.
        if (mem.ReadU8(node) != 2 || mem.ReadU32(node + 16) != 4) return distance;
        Interlocked.Increment(ref _previews);
        return 0;
    }
    public const uint NeutralPose = 0x8018B074u;
    public const uint SinCosTable = 0x8016EBF4u;

    // The introduction renders before normal per-frame animation has run.
    // Its low-detail pre-posed mesh concealed the all-zero articulated pose.
    // Seed ONLY an empty verified rider pose from the game's neutral riding
    // pose, without advancing physics, time, input, or a stunt sequence.
    public static bool InitializeIdlePose(PSMemory mem, uint binding)
    {
        uint p=binding&0x1fffffffu;
        if ((p&3)!=0 || (long)p+0x160>mem.Ram.Length) return false;
        uint selector=mem.ReadU32(binding+4);
        if (!IsRaceSelector(mem,selector) || !NativeTextures.IsOriginalRiderNode(selector,9)) return false;
        for (uint i=4;i<100;i+=4) if (mem.ReadU32(binding+0x54+i)!=0) return false;
        if (mem.ReadU32(SinCosTable)!=0x10000000 || mem.ReadU32(SinCosTable+4096)!=0x00001000) return false;
        bool nonzero=false;
        for (uint i=4;i<100;i+=4)
        {
            int v=(int)mem.ReadU32(NeutralPose+i);
            if (v < -65536 || v > 65536) return false;
            nonzero |= v!=0;
        }
        if (!nonzero) return false;
        Span<uint> joints=stackalloc uint[7];
        for (int j=0;j<7;j++)
        {
            joints[j]=mem.ReadU32(binding+0x38+(uint)j*4);
            if (!NativeTextures.IsOriginalRiderBone(selector,joints[j],j+2)) return false;
        }
        uint translation=mem.ReadU32(binding+0x24);
        if (!NativeTextures.IsOriginalRiderTranslation(selector,translation)) return false;
        // All guards pass before any write. State format matches 80131FE0.
        for (uint i=4;i<100;i+=4) mem.WriteU32(binding+0x54+i,mem.ReadU32(NeutralPose+i));
        for (uint i=0;i<3;i++) mem.WriteU32(translation+4+i*4,unchecked((uint)((int)mem.ReadU32(binding+0x58+i*4)>>9)));
        Span<short> matrix=stackalloc short[9];
        for (int j=0;j<7;j++)
        {
            uint angles=binding+0x64+(uint)j*12;
            Rotation(mem,unchecked((short)((int)mem.ReadU32(angles)>>2)),unchecked((short)((int)mem.ReadU32(angles+4)>>2)),unchecked((short)((int)mem.ReadU32(angles+8)>>2)),matrix);
            for (uint i=0;i<9;i++) mem.WriteU16(joints[j]+4+i*2,unchecked((ushort)matrix[(int)i]));
        }
        Interlocked.Increment(ref _poseSeeds);
        return true;
    }

    // Exact fixed-point arithmetic/order of 800FA748. Reuse the original
    // sin/cos table rather than approximating with host floating-point trig.
    // This avoids disturbing guest registers, GTE state, PGXP provenance or stack.
    public static void Rotation(PSMemory mem, short x, short y, short z, Span<short> output)
    {
        if(output.Length<9) throw new ArgumentException("Nine matrix elements required.");
        (int sin,int cos) Lookup(short angle)
        {
            int a=angle;
            uint packed=mem.ReadU32(SinCosTable+(uint)(Math.Abs(a)&4095)*4);
            int sin=(short)packed,cos=(short)(packed>>16);
            return (a<0?-sin:sin,cos);
        }
        var (sx,cx)=Lookup(x); var (sy,cy)=Lookup(y); var (sz,cz)=Lookup(z);
        int Mul(int a,int b)=>unchecked(a*b)>>12;
        int a=Mul(sy,sx), b=Mul(cy,sx);
        output[0]=unchecked((short)(Mul(cy,cz)+Mul(a,sz)));
        output[1]=unchecked((short)(-Mul(cy,sz)+Mul(a,cz)));
        output[2]=unchecked((short)Mul(sy,cx));
        output[3]=unchecked((short)Mul(sz,cx));
        output[4]=unchecked((short)Mul(cz,cx));
        output[5]=unchecked((short)-sx);
        output[6]=unchecked((short)(-Mul(sy,cz)+Mul(b,sz)));
        output[7]=unchecked((short)(Mul(sy,sz)+Mul(b,cz)));
        output[8]=unchecked((short)Mul(cy,cx));
    }

    public static void ObserveMatrixSlot(uint slot)
    {
        if (!Widescreen.Active) return;
        if (slot>_matrixMax) _matrixMax=(int)slot;
        if(slot>=RenderArena.MatrixCapacity-2 && Interlocked.Increment(ref _matrixLimitHits)==1)
            Console.WriteLine("[JetMoto:rider-detail] WARNING: expanded transform-slot limit reached; retain this log.");
    }

}
