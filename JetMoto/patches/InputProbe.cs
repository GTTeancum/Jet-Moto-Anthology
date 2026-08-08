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

    /// <summary>
    /// func_800EF9D8(button, mode) is the game's "was this pressed" query --
    /// the boot state machine calls it as (0x0B, 0x02). It reaches the edge
    /// word through func_800EF4CC. Logging both says whether the menus are
    /// asking, and what answer they get.
    /// </summary>
    static int _strIn, _strOut;

    /// <summary>
    /// func_800E0CDC is the STR movie player (it calls StSetRing/StSetStream).
    /// Trapping mid-title showed the stack inside it ~80 frames deep. If entries
    /// and exits balance it is being re-entered per frame legitimately; if
    /// entries run away from exits the game never leaves the movie.
    /// </summary>
    public static void StrEnter(CpuContext c, IMemory m)
    {
        _strIn++;
        if (_strIn <= 5 || _strIn % 200 == 0)
            System.Console.Error.WriteLine($"[STR] enter #{_strIn} (exits {_strOut}, depth {_strIn - _strOut})");
    }

    public static void StrExit(CpuContext c, IMemory m)
    {
        _strOut++;
        if (_strOut <= 5 || _strOut % 200 == 0)
            System.Console.Error.WriteLine($"[STR] exit  #{_strOut} (depth {_strIn - _strOut})");
    }

    static int _tw, _c1, _c2;

    /// <summary>
    /// func_80134FBC holds the interactive title wait loop -- the one that
    /// polls action 0x0B with mode 2. No mode-2 query has ever been observed,
    /// so the loop appears never to run. These say whether it, or either of
    /// its two callers, is reached at all.
    /// </summary>
    public static void EnterTitleWait(CpuContext c, IMemory m)
    {
        if (++_tw <= 3)
            System.Console.Error.WriteLine($"[Flow] func_80134FBC (title wait) entered #{_tw}");
    }

    public static void EnterCaller1(CpuContext c, IMemory m)
    {
        if (++_c1 <= 3)
            System.Console.Error.WriteLine($"[Flow] func_801395B8 entered #{_c1}");
    }

    public static void EnterCaller2(CpuContext c, IMemory m)
    {
        if (++_c2 <= 3)
            System.Console.Error.WriteLine($"[Flow] func_8013963C entered #{_c2}");
    }

    static uint _lastA0, _lastA1;
    static readonly System.Collections.Generic.HashSet<uint> _seenIds = new();

    /// <summary>
    /// a0 is a button id and a1 a mode; the function clobbers both, so they
    /// have to be captured before it runs.
    /// </summary>
    public static void BeforeButtonQuery(CpuContext c, IMemory m)
    {
        _lastA0 = c.A0 & 0xFF;
        _lastA1 = c.A1 & 0xFFFF;
        if (_seenIds.Add(_lastA0))
            System.Console.Error.WriteLine(
                $"[Query] first sighting of button id 0x{_lastA0:X2} (mode {_lastA1})");
    }

    public static void AfterButtonQuery(CpuContext c, IMemory m)
    {
        _q1++;
        if ((c.V0 & 0xFF) != 0)
            System.Console.Error.WriteLine(
                $"[Query] HIT id=0x{_lastA0:X2} mode={_lastA1} -> 0x{c.V0 & 0xFF:X2}");
    }

    public static void AfterEdgeRead(CpuContext c, IMemory m)
    {
        _q2++;
        if (c.V0 != 0 || _q2 % 600 == 1)
            System.Console.Error.WriteLine(
                $"[Query] func_800EF4CC #{_q2} -> 0x{c.V0:X8}");
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
