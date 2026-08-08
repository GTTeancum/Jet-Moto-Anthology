using RecompOne.Runtime.Context;
using RecompOne.Runtime.Memory;

namespace Recompiled;

/// <summary>
/// Diagnostic hook on the game's pad parser (func_800EF098).
///
/// The pad buffer at 0x801EA0D8 is demonstrably correct — holding Cross gives
/// 0xFFBF4100, and this function's own arithmetic
/// (<c>(buf[3] | buf[2] &lt;&lt; 8) ^ 0xFFFF</c>) yields 0x4000 from it — yet the
/// game never acts on input. Two possibilities: the parser is not being called
/// in menu states, or it is called and something downstream ignores the result.
/// This distinguishes them by reporting call count and the word it stores at
/// gp+0x70.
/// </summary>
public static class InputProbe
{
    static int _calls;
    static uint _last = 0xDEADBEEF;

    static uint _edgeSeen;

    static uint _endEdgeSeen;
    static int _endCalls;

    /// <summary>
    /// End of the whole input frame (func_800EEE60), after the accumulator has
    /// had its chance to run. Reading gp+0x6C from the parser hook was too
    /// early -- the parser runs before the accumulator in the same frame.
    /// </summary>
    public static void AfterInputFrame(CpuContext c, IMemory m)
    {
        _endCalls++;
        uint buttons, edge, mode;
        try
        {
            buttons = m.ReadU32(c.GP + 0x70u);
            edge = m.ReadU32(c.GP + 0x6Cu);
            mode = m.ReadU8(c.GP + 0x7Cu);
        }
        catch { return; }
        _endEdgeSeen |= edge;
        if (buttons != 0 || edge != 0)
            System.Console.Error.WriteLine(
                $"[InputFrame] #{_endCalls} buttons=0x{buttons:X8} edge=0x{edge:X8} " +
                $"mode(gp+0x7C)={mode} edgeEver=0x{_endEdgeSeen:X8}");
        else if (_endCalls % 1200 == 0)
            System.Console.Error.WriteLine(
                $"[InputFrame] #{_endCalls} idle mode(gp+0x7C)={mode} " +
                $"edgeEver=0x{_endEdgeSeen:X8}");
    }

    static int _q1, _q2;

    /// <summary>Menus query "was this pressed" through these; are they called?</summary>
    public static void AfterQuery(CpuContext c, IMemory m)
    {
        if (++_q1 % 300 == 1)
            System.Console.Error.WriteLine($"[Query] func_800EF26C called {_q1}x, returned 0x{c.V0:X8}");
    }

    public static void AfterQuery2(CpuContext c, IMemory m)
    {
        if (++_q2 % 300 == 1)
            System.Console.Error.WriteLine($"[Query] func_800EF2AC called {_q2}x, returned 0x{c.V0:X8}");
    }

    public static void AfterPadParse(CpuContext c, IMemory m)
    {
        _calls++;
        uint buttons, edge;
        try
        {
            buttons = m.ReadU32(c.GP + 0x70u);   // current buttons
            edge = m.ReadU32(c.GP + 0x6Cu);      // "pressed since last cleared"
        }
        catch { return; }

        // The edge word is an accumulator that a consumer clears, so it can be
        // non-zero for a single frame. Sampling it once a second misses it
        // almost always -- hence checking every frame and remembering.
        _edgeSeen |= edge;

        if (buttons != _last)
        {
            _last = buttons;
            System.Console.Error.WriteLine(
                $"[PadProbe] #{_calls} buttons=0x{buttons:X8} edge=0x{edge:X8} " +
                $"edgeEverSeen=0x{_edgeSeen:X8}");
        }
        else if (_calls % 900 == 0)
        {
            System.Console.Error.WriteLine(
                $"[PadProbe] #{_calls} steady buttons=0x{buttons:X8} " +
                $"edgeEverSeen=0x{_edgeSeen:X8}");
        }
    }
}
