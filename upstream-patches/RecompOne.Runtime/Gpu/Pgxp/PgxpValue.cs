using System.Runtime.CompilerServices;

namespace RecompOne.Runtime.Pgxp;

public struct PgxpValue
{
    public float X;
    public float Y;
    public float Z;
    public uint Value;
    public uint Flags;
    public uint Count;
    public int Transform;
}

public static class PgxpFlags
{
    public const uint None = 0u;
    public const uint Valid0 = 1u << 0;
    public const uint Valid1 = 1u << 8;
    public const uint Valid2 = 1u << 16;
    public const uint Valid3 = 1u << 24;
    public const uint ValidLow = Valid0 | Valid1;
    public const uint ValidAll = Valid0 | Valid1 | Valid2 | Valid3;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Matches(in PgxpValue value, uint architectural)
    {
        return value.Value == architectural && (value.Flags & ValidLow) == ValidLow;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Validate(ref PgxpValue value, uint architectural)
    {
        if (value.Value != architectural) value.Flags &= ~ValidAll;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void MaskValidate(ref PgxpValue value, uint architectural, uint mask, uint validMask)
    {
        if ((value.Value & mask) != (architectural & mask)) value.Flags &= ~validMask;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetValue(ref PgxpValue value, uint architectural)
    {
        value.X = (short)architectural;
        value.Y = (short)(architectural >> 16);
        value.Z = 0f;
        value.Value = architectural;
        value.Flags = ValidLow;
        value.Count = 0;
        value.Transform = 0;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void MakeValid(ref PgxpValue value, uint architectural)
    {
        if ((value.Flags & ValidLow) == ValidLow) return;
        
        value.X = (short)architectural;
        value.Y = (short)(architectural >> 16);
        value.Z = 0f;
        value.Value = architectural;
        value.Flags = ValidLow;
        value.Count = 0;
        value.Transform = 0;
    }
    
    public static uint ToTolerance(in PgxpValue value, uint architectural, float tolerance)
    {
        var flags = ValidAll;
        
        if (Math.Abs(value.X - (short)architectural) >= tolerance) flags &= ~Valid0;
        if (Math.Abs(value.Y - (short)(architectural >> 16)) >= tolerance) flags &= ~Valid1;
        
        return flags;
    }
    
    public static double F16Sign(double value)
    {
        var bits = (uint)(long)(value * 65536.0);
        return (int)bits / 65536.0;
    }
    
    public static double F16Unsign(double value)
    {
        return value >= 0.0 ? value : value + 65536.0;
    }
    
    public static double F16Overflow(double value)
    {
        return Math.Floor(value / 65536.0);
    }
}
