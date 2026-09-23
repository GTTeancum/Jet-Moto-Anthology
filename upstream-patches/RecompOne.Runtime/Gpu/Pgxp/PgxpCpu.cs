namespace RecompOne.Runtime.Pgxp;

public static class PgxpCpu
{

    private static readonly PgxpValue[] _gpr = new PgxpValue[32];
    private static PgxpValue _hi;
    private static PgxpValue _lo;
    
    static PgxpCpu()
    {
        Reset();
    }
    
    public static void Reset()
    {
        Array.Clear(_gpr);
        
        _gpr[0].X = 0f;
        _gpr[0].Y = 0f;
        _gpr[0].Z = 0f;
        _gpr[0].Value = 0u;
        _gpr[0].Flags = PgxpFlags.ValidLow;
        
        _hi = default;
        _lo = default;
    }
    
    public static void Mfc2(int rt, int rd, uint value)
    {
        if (!Pgxp.CpuTracking || rt == 0) return;
        
        ref var src = ref PgxpGte.Data(rd);
        PgxpFlags.Validate(ref src, value);
        
        _gpr[rt] = src;
        _gpr[rt].Value = value;
    }
    
    public static void Mtc2(int rd, int rt, uint value)
    {
        if (!Pgxp.CpuTracking) return;
        
        ref var src = ref _gpr[rt];
        PgxpFlags.Validate(ref src, value);
        
        ref var dest = ref PgxpGte.Data(rd);
        dest = src;
        dest.Value = value;
    }
    public static void Lw(int rt, uint address, uint value)
    {
        if (!Pgxp.CpuTracking || rt == 0) return;
        
        PgxpMemory.LoadInto(address, value, ref _gpr[rt]);
    }
    public static void Sw(int rt, uint address, uint value)
    {
        if (!Pgxp.CpuTracking) return;
        
        ref var src = ref _gpr[rt];
        PgxpFlags.Validate(ref src, value);
        
        PgxpMemory.Store(address, in src, value);
    }

    public static void Sh(int rt, uint address, uint value)
    {
        if (!Pgxp.CpuTracking) return;
        
        ref var src = ref _gpr[rt];
        PgxpFlags.MaskValidate(ref src, value, 0xFFFFu, PgxpFlags.Valid0);
        
        PgxpMemory.StoreHalf(address, in src, (ushort)value);
    }

    public static void Lh(int rt, uint address, uint value)
    {
        if (!Pgxp.CpuTracking || rt == 0) return;
        
        PgxpMemory.LoadHalf(address, value, ref _gpr[rt]);
    }
    
    public static void Lwc2(int rt, uint address, uint value)
    {
        if (!Pgxp.CpuTracking) return;
        
        PgxpMemory.LoadInto(address, value, ref PgxpGte.Data(rt));
    }
    
    public static void Swc2(int rt, uint address, uint value)
    {
        if (!Pgxp.CpuTracking) return;
        
        ref var src = ref PgxpGte.Data(rt);
        PgxpFlags.Validate(ref src, value);
        
        PgxpMemory.Store(address, in src, value);
    }
    
    public static void Move(int rd, int rs, uint value)
    {
        if (rd == 0) return;
        
        ref var src = ref _gpr[rs];
        PgxpFlags.Validate(ref src, value);
        
        _gpr[rd] = src;
        _gpr[rd].Value = value;
    }
    
    public static void Const(int rt, uint value)
    {
        if (rt == 0) return;
        
        PgxpFlags.SetValue(ref _gpr[rt], value);
        _gpr[rt].Transform = 0;
    }
    
    public static void Lui(int rt, uint value)
    {
        if (rt == 0) return;
        
        ref var dest = ref _gpr[rt];
        dest.X = 0f;
        dest.Y = (short)(value >> 16);
        dest.Z = 0f;
        dest.Value = value;
        dest.Flags = PgxpFlags.ValidLow;
        dest.Transform = 0;
    }
    
    public static void Addi(int rt, int rs, short imm, uint rtVal, uint rsVal)
    {
        if (rt == 0) return;
        
        ref var src = ref _gpr[rs];
        PgxpFlags.Validate(ref src, rsVal);
        
        var ret = src;
        
        double x = PgxpFlags.F16Unsign(ret.X);
        x += (ushort)imm;
        
        var of = x > ushort.MaxValue ? 1.0 : x < 0.0 ? -1.0 : 0.0;
        x = PgxpFlags.F16Sign(x);
        
        double y = ret.Y + (imm < 0 ? -1 : 0) + of;
        y += y > short.MaxValue ? -(ushort.MaxValue + 1) : y < short.MinValue ? ushort.MaxValue + 1 : 0;
        
        ret.X = (float)x;
        ret.Y = (float)y;
        ret.Transform = src.Transform;
        ret.Value = rtVal;
        
        _gpr[rt] = ret;
    }
    
    public static void Add(int rd, int rs, int rt, uint rdVal, uint rsVal, uint rtVal)
    {
        if (rd == 0) return;
        
        ref var a = ref _gpr[rs];
        ref var b = ref _gpr[rt];
        PgxpFlags.Validate(ref a, rsVal);
        PgxpFlags.Validate(ref b, rtVal);
        Pair(ref a, ref b, rsVal, rtVal);
        
        var ret = a;
        
        double x = PgxpFlags.F16Unsign(ret.X) + PgxpFlags.F16Unsign(b.X);
        var of = x > ushort.MaxValue ? 1.0 : x < 0.0 ? -1.0 : 0.0;
        x = PgxpFlags.F16Sign(x);
        
        double y = ret.Y + b.Y + of;
        y += y > short.MaxValue ? -(ushort.MaxValue + 1) : y < short.MinValue ? ushort.MaxValue + 1 : 0;
        
        ret.X = (float)x;
        ret.Y = (float)y;
        ret.Transform = a.Transform != 0 ? a.Transform : b.Transform;
        ret.Value = rdVal;
        if (Pgxp.RequiredPerspective)
        {
            bool aw = (a.Flags & PgxpFlags.Valid2) != 0, bw = (b.Flags & PgxpFlags.Valid2) != 0;
            if (aw && bw && !SameOrigin(in a, in b)) ret.Flags &= ~PgxpFlags.Valid2;
            else if (!aw && bw)
            { ret.Z = b.Z; ret.Count = b.Count; ret.Transform = b.Transform; ret.Flags |= PgxpFlags.Valid2; }
        }
        
        _gpr[rd] = ret;
    }
    
    public static void Sub(int rd, int rs, int rt, uint rdVal, uint rsVal, uint rtVal)
    {
        if (rd == 0) return;
        
        ref var a = ref _gpr[rs];
        ref var b = ref _gpr[rt];
        PgxpFlags.Validate(ref a, rsVal);
        PgxpFlags.Validate(ref b, rtVal);
        Pair(ref a, ref b, rsVal, rtVal);
        
        var ret = a;
        
        double x = PgxpFlags.F16Unsign(ret.X) - PgxpFlags.F16Unsign(b.X);
        var of = x > ushort.MaxValue ? 1.0 : x < 0.0 ? -1.0 : 0.0;
        x = PgxpFlags.F16Sign(x);
        
        double y = ret.Y - (b.Y - of);
        y += y > short.MaxValue ? -(ushort.MaxValue + 1) : y < short.MinValue ? ushort.MaxValue + 1 : 0;
        
        ret.X = (float)x;
        ret.Y = (float)y;
        ret.Transform = a.Transform != 0 ? a.Transform : b.Transform;
        ret.Value = rdVal;
        if (Pgxp.RequiredPerspective)
        {
            bool aw = (a.Flags & PgxpFlags.Valid2) != 0, bw = (b.Flags & PgxpFlags.Valid2) != 0;
            if (aw && bw && !SameOrigin(in a, in b)) ret.Flags &= ~PgxpFlags.Valid2;

        }
        
        _gpr[rd] = ret;
    }
    
    public static void Bitwise(int rd, int rs, int rt, uint rdVal, uint rsVal, uint rtVal)
    {
        if (rd == 0) return;
        
        ref var a = ref _gpr[rs];
        ref var b = ref _gpr[rt];
        PgxpFlags.Validate(ref a, rsVal);
        PgxpFlags.Validate(ref b, rtVal);
        Pair(ref a, ref b, rsVal, rtVal);
        
        var ret = default(PgxpValue);
        ret.Flags = PgxpFlags.ValidLow;
        
        var low = (ushort)rdVal;
        if (low == 0) ret.X = 0f;
        else if (low == (ushort)rsVal) ret.X = a.X;
        else if (low == (ushort)rtVal) ret.X = b.X;
        else ret.X = (short)low;
        
        var high = (ushort)(rdVal >> 16);
        if (high == 0) ret.Y = 0f;
        else if (high == (ushort)(rsVal >> 16)) ret.Y = a.Y;
        else if (high == (ushort)(rtVal >> 16)) ret.Y = b.Y;
        else ret.Y = (short)high;
        
        bool aw = (a.Flags & PgxpFlags.Valid2) != 0, bw = (b.Flags & PgxpFlags.Valid2) != 0;
        if ((aw || bw) && (!Pgxp.RequiredPerspective || !aw || !bw || SameOrigin(in a, in b)))
        {
            var origin = aw ? a : b;
            // A bitwise result may copy/mask coordinate halves, not invent new ones.
            bool lowCopied = low == 0 || low == (ushort)rsVal || low == (ushort)rtVal;
            bool highCopied = high == 0 || high == (ushort)(rsVal >> 16) || high == (ushort)(rtVal >> 16);
            if (!Pgxp.RequiredPerspective || (lowCopied && highCopied))
            {
                ret.Z = origin.Z;
                ret.Count = origin.Count;
                ret.Transform = origin.Transform;
                ret.Flags |= PgxpFlags.Valid2;
            }
        }

        ret.Value = rdVal;
        _gpr[rd] = ret;
    }
    
    public static void Andi(int rt, int rs, ushort imm, uint rtVal, uint rsVal)
    {
        if (rt == 0) return;
        
        ref var src = ref _gpr[rs];
        PgxpFlags.Validate(ref src, rsVal);
        
        var ret = src;
        ret.Y = 0f;
        
        switch (imm)
        {
            case 0:
                ret.X = 0f;
                break;
            case 0xFFFF:
                break;
            default:
                ret.X = (short)rtVal;
                ret.Flags |= PgxpFlags.Valid0;
                break;
        }
        
        ret.Flags |= PgxpFlags.Valid1;
        ret.Transform = src.Transform;
        if (Pgxp.RequiredPerspective && imm != 0xFFFF && rtVal != rsVal) ret.Flags &= ~PgxpFlags.Valid2;
        ret.Value = rtVal;
        
        _gpr[rt] = ret;
    }
    
    public static void Ori(int rt, int rs, ushort imm, uint rtVal, uint rsVal)
    {
        if (rt == 0) return;
        
        ref var src = ref _gpr[rs];
        PgxpFlags.Validate(ref src, rsVal);
        
        var ret = src;
        
        if (imm != 0)
        {
            ret.X = (short)rtVal;
            ret.Flags |= PgxpFlags.Valid0;
        }
        
        ret.Transform = src.Transform;
        if (Pgxp.RequiredPerspective && rtVal != rsVal) ret.Flags &= ~PgxpFlags.Valid2;
        ret.Value = rtVal;
        _gpr[rt] = ret;
    }
    
    public static void Slti(int rt, int rs, short imm, uint rtVal, uint rsVal)
    {
        if (rt == 0) return;
        
        ref var src = ref _gpr[rs];
        PgxpFlags.Validate(ref src, rsVal);
        
        var ret = src;
        ret.Y = 0f;
        ret.X = src.X < imm ? 1f : 0f;
        ret.Flags |= PgxpFlags.Valid1;
        ret.Transform = 0;
        ret.Value = rtVal;
        
        if (Pgxp.RequiredPerspective) PgxpFlags.SetValue(ref ret, rtVal);
        _gpr[rt] = ret;
    }
    
    public static void Sltiu(int rt, int rs, ushort imm, uint rtVal, uint rsVal)
    {
        if (rt == 0) return;
        
        ref var src = ref _gpr[rs];
        PgxpFlags.Validate(ref src, rsVal);
        
        var ret = src;
        ret.Y = 0f;
        ret.X = PgxpFlags.F16Unsign(src.X) < imm ? 1f : 0f;
        ret.Flags |= PgxpFlags.Valid1;
        ret.Transform = 0;
        ret.Value = rtVal;
        
        if (Pgxp.RequiredPerspective) PgxpFlags.SetValue(ref ret, rtVal);
        _gpr[rt] = ret;
    }
    
    public static void Slt(int rd, int rs, int rt, uint rdVal, uint rsVal, uint rtVal)
    {
        if (rd == 0) return;
        
        ref var a = ref _gpr[rs];
        ref var b = ref _gpr[rt];
        PgxpFlags.Validate(ref a, rsVal);
        PgxpFlags.Validate(ref b, rtVal);
        Pair(ref a, ref b, rsVal, rtVal);
        
        var ret = a;
        ret.Y = 0f;
        ret.X = a.Y < b.Y ? 1f :
            PgxpFlags.F16Unsign(a.X) < PgxpFlags.F16Unsign(b.X) ? 1f : 0f;
        ret.Transform = 0;
        ret.Value = rdVal;
        
        if (Pgxp.RequiredPerspective) PgxpFlags.SetValue(ref ret, rdVal);
        _gpr[rd] = ret;
    }
    
    public static void Sltu(int rd, int rs, int rt, uint rdVal, uint rsVal, uint rtVal)
    {
        if (rd == 0) return;
        
        ref var a = ref _gpr[rs];
        ref var b = ref _gpr[rt];
        PgxpFlags.Validate(ref a, rsVal);
        PgxpFlags.Validate(ref b, rtVal);
        Pair(ref a, ref b, rsVal, rtVal);
        
        var ret = a;
        ret.Y = 0f;
        ret.X = PgxpFlags.F16Unsign(a.Y) < PgxpFlags.F16Unsign(b.Y) ? 1f :
            PgxpFlags.F16Unsign(a.X) < PgxpFlags.F16Unsign(b.X) ? 1f : 0f;
        ret.Transform = 0;
        ret.Value = rdVal;
        
        if (Pgxp.RequiredPerspective) PgxpFlags.SetValue(ref ret, rdVal);
        _gpr[rd] = ret;
    }
    
    public static void Sll(int rd, int rt, int sa, uint rdVal, uint rtVal)
    {
        if (rd == 0) return;
        
        ref var src = ref _gpr[rt];
        PgxpFlags.Validate(ref src, rtVal);
        
        var ret = src;
        var x = PgxpFlags.F16Unsign(src.X);
        var y = PgxpFlags.F16Unsign(src.Y);
        
        if (sa == 16)
        {
            y = PgxpFlags.F16Sign(x);
            x = 0.0;
        }
        else if (sa > 16)
        {
            y = PgxpFlags.F16Sign(x * (1 << (sa - 16)));
            x = 0.0;
        }
        else
        {
            x *= 1 << sa;
            y *= 1 << sa;
            y += PgxpFlags.F16Overflow(x);
            x = PgxpFlags.F16Sign(x);
            y = PgxpFlags.F16Sign(y);
        }
        
        ret.X = (float)x;
        ret.Y = (float)y;
        ret.Transform = src.Transform;
        ret.Value = rdVal;
        
        _gpr[rd] = ret;
    }
    
    public static void Srl(int rd, int rt, int sa, uint rdVal, uint rtVal)
    {
        Shift(rd, rt, sa, rdVal, rtVal, false);
    }
    
    public static void Sra(int rd, int rt, int sa, uint rdVal, uint rtVal)
    {
        Shift(rd, rt, sa, rdVal, rtVal, true);
    }
    
    private static void Shift(int rd, int rt, int sa, uint rdVal, uint rtVal, bool arithmetic)
    {
        if (rd == 0) return;
        
        ref var src = ref _gpr[rt];
        PgxpFlags.Validate(ref src, rtVal);
        
        var ret = src;
        double x = src.X;
        var y = arithmetic ? src.Y : PgxpFlags.F16Unsign(src.Y);
        
        var ix = (uint)(int)(short)rtVal;
        var sign = (short)(ix >> 16);
        var iy = (rtVal & 0xFFFF0000u) | (ushort)sign;
        
        var dx = (short)((int)ix >> sa);
        var dyLow = arithmetic ? (short)((int)iy >> sa) : (short)(iy >> sa);
        var dyHigh = arithmetic ? (short)((int)iy >> sa >> 16) : (short)(iy >> sa >> 16);
        
        if (dx != sign) x /= 1u << sa;
        else x = dx;
        
        if (dyLow != sign)
        {
            if (sa == 16) x = y;
            else if (sa < 16)
            {
                x += y * (1 << (16 - sa));
                if (src.X < 0f) x += 1 << (16 - sa);
            }
            else x += y / (1 << (sa - 16));
        }
        
        if (dyHigh is 0 or -1) y = dyHigh;
        else y /= 1u << sa;
        
        ret.X = (float)PgxpFlags.F16Sign(x);
        ret.Y = (float)PgxpFlags.F16Sign(y);
        ret.Transform = src.Transform;
        ret.Value = rdVal;
        
        _gpr[rd] = ret;
    }
    
    public static void Mult(int rs, int rt, uint hiVal, uint loVal, uint rsVal, uint rtVal, bool signed)
    {
        ref var a = ref _gpr[rs];
        ref var b = ref _gpr[rt];
        PgxpFlags.Validate(ref a, rsVal);
        PgxpFlags.Validate(ref b, rtVal);
        Pair(ref a, ref b, rsVal, rtVal);
        
        if (Pgxp.RequiredPerspective)
        {
            PgxpFlags.SetValue(ref _lo, loVal);
            PgxpFlags.SetValue(ref _hi, hiVal);
            return; // integer scalar multiplication/division is not a new GTE vertex
        }
        _lo = a;
        _hi = a;
        
        var ay = signed ? a.Y : PgxpFlags.F16Unsign(a.Y);
        var by = signed ? b.Y : PgxpFlags.F16Unsign(b.Y);
        
        var xx = PgxpFlags.F16Unsign(a.X) * PgxpFlags.F16Unsign(b.X);
        var xy = PgxpFlags.F16Unsign(a.X) * by;
        var yx = ay * PgxpFlags.F16Unsign(b.X);
        var yy = ay * by;
        
        var lx = xx;
        var ly = PgxpFlags.F16Overflow(xx) + xy + yx;
        var hx = PgxpFlags.F16Overflow(ly) + yy;
        var hy = PgxpFlags.F16Overflow(hx);
        
        _lo.X = (float)PgxpFlags.F16Sign(lx);
        _lo.Y = (float)PgxpFlags.F16Sign(ly);
        _hi.X = (float)PgxpFlags.F16Sign(hx);
        _hi.Y = (float)PgxpFlags.F16Sign(hy);
        
        _lo.Transform = 0;
        _hi.Transform = 0;
        _lo.Value = loVal;
        _hi.Value = hiVal;
    }
    
    public static void Div(int rs, int rt, uint hiVal, uint loVal, uint rsVal, uint rtVal, bool signed)
    {
        ref var a = ref _gpr[rs];
        ref var b = ref _gpr[rt];
        PgxpFlags.Validate(ref a, rsVal);
        PgxpFlags.Validate(ref b, rtVal);
        Pair(ref a, ref b, rsVal, rtVal);
        
        if (Pgxp.RequiredPerspective)
        {
            PgxpFlags.SetValue(ref _lo, loVal);
            PgxpFlags.SetValue(ref _hi, hiVal);
            return; // integer scalar multiplication/division is not a new GTE vertex
        }
        _lo = a;
        _hi = a;
        
        var vs = PgxpFlags.F16Unsign(a.X) + (signed ? a.Y : PgxpFlags.F16Unsign(a.Y)) * 65536.0;
        var vt = PgxpFlags.F16Unsign(b.X) + (signed ? b.Y : PgxpFlags.F16Unsign(b.Y)) * 65536.0;
        
        if (vt == 0.0)
        {
            _lo.Flags = PgxpFlags.None;
            _hi.Flags = PgxpFlags.None;
            _lo.Transform = 0;
            _hi.Transform = 0;
            _lo.Value = loVal;
            _hi.Value = hiVal;
            return;
        }
        
        var lo = vs / vt;
        _lo.Y = (float)PgxpFlags.F16Sign(PgxpFlags.F16Overflow(lo));
        _lo.X = (float)PgxpFlags.F16Sign(lo);
        
        var hi = vs % vt;
        _hi.Y = (float)PgxpFlags.F16Sign(PgxpFlags.F16Overflow(hi));
        _hi.X = (float)PgxpFlags.F16Sign(hi);
        
        _lo.Transform = 0;
        _hi.Transform = 0;
        _lo.Value = loVal;
        _hi.Value = hiVal;
    }
    
    public static void Mfhi(int rd, uint rdVal, uint hiVal)
    {
        if (rd == 0) return;
        
        PgxpFlags.Validate(ref _hi, hiVal);
        _gpr[rd] = _hi;
        _gpr[rd].Value = rdVal;
    }
    
    public static void Mthi(int rs, uint hiVal, uint rsVal)
    {
        PgxpFlags.Validate(ref _gpr[rs], rsVal);
        _hi = _gpr[rs];
        _hi.Value = hiVal;
    }
    
    public static void Mflo(int rd, uint rdVal, uint loVal)
    {
        if (rd == 0) return;
        
        PgxpFlags.Validate(ref _lo, loVal);
        _gpr[rd] = _lo;
        _gpr[rd].Value = rdVal;
    }
    
    public static void Mtlo(int rs, uint loVal, uint rsVal)
    {
        PgxpFlags.Validate(ref _gpr[rs], rsVal);
        _lo = _gpr[rs];
        _lo.Value = loVal;
    }
    
    private static bool SameOrigin(in PgxpValue a, in PgxpValue b) =>
        a.Count == b.Count && a.Z == b.Z && a.Transform == b.Transform;

    private static void Pair(ref PgxpValue a, ref PgxpValue b, uint aVal, uint bVal)
    {
        var validA = (a.Flags & PgxpFlags.ValidLow) == PgxpFlags.ValidLow;
        var validB = (b.Flags & PgxpFlags.ValidLow) == PgxpFlags.ValidLow;
        if (validA == validB) return;
        
        PgxpFlags.MakeValid(ref a, aVal);
        PgxpFlags.MakeValid(ref b, bVal);
    }
    
    public static void Invalidate(int rt)
    {
        if (rt == 0) return;
        
        _gpr[rt].Flags = PgxpFlags.None;
    }

    public static void InvalidateMem(uint address, uint value)
    {
        if (!Pgxp.CpuTracking) return;
        
        PgxpMemory.Invalidate(address, value);
    }
}
