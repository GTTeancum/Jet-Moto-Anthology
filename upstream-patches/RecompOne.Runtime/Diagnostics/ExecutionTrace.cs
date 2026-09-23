using System.Runtime.CompilerServices;

namespace RecompOne.Runtime.Diagnostics;

/// <summary>Optional lightweight PC breadcrumbs for recompilation bring-up. No game-state changes.</summary>
public static class ExecutionTrace
{
    public static bool Enabled;
    public static volatile bool StopRequested;
    private static uint _lastAddress;
    private static uint _lastFunction;
    private static readonly uint[] _recent = new uint[64];
    private static long _sequence;
    public static uint LastAddress => Volatile.Read(ref _lastAddress);
    public static uint LastFunction => Volatile.Read(ref _lastFunction);
    public sealed class StopSignal : Exception;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Enter(uint address)
    {
        if (StopRequested) throw new StopSignal();
        if (!Enabled) return;
        Volatile.Write(ref _lastAddress, address);
        Volatile.Write(ref _lastFunction, address);
        long index = _sequence;
        Volatile.Write(ref _recent[(int)(index & 63)], address);
        Volatile.Write(ref _sequence, index + 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Block(uint address)
    {
        if (StopRequested) throw new StopSignal();
        if (Enabled) Volatile.Write(ref _lastAddress, address);
    }

    public static uint[] RecentFunctions()
    {
        long end = Volatile.Read(ref _sequence);
        int count = (int)Math.Min(end, _recent.Length);
        var result = new uint[count];
        for (int i = 0; i < count; i++)
            result[i] = Volatile.Read(ref _recent[(int)((end - count + i) & 63)]);
        return result;
    }
}
