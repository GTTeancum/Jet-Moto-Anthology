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

    static int _seen80137428;
    public static void Seen80137428(CpuContext c, IMemory m)
    {
        if (++_seen80137428 == 1)
            System.Console.Error.WriteLine("[Seen] func_80137428 entered");
        else if (_seen80137428 % 3000 == 0)
            System.Console.Error.WriteLine($"[Seen] func_80137428 x{_seen80137428}");
    }

    static int _seen8013AA28;
    public static void Seen8013AA28(CpuContext c, IMemory m)
    {
        if (++_seen8013AA28 == 1)
            System.Console.Error.WriteLine("[Seen] func_8013AA28 entered");
        else if (_seen8013AA28 % 3000 == 0)
            System.Console.Error.WriteLine($"[Seen] func_8013AA28 x{_seen8013AA28}");
    }

    static int _seen8013D7C8;
    public static void Seen8013D7C8(CpuContext c, IMemory m)
    {
        if (++_seen8013D7C8 == 1)
            System.Console.Error.WriteLine("[Seen] func_8013D7C8 entered");
        else if (_seen8013D7C8 % 3000 == 0)
            System.Console.Error.WriteLine($"[Seen] func_8013D7C8 x{_seen8013D7C8}");
    }

    static int _seen8011B3D0;
    public static void Seen8011B3D0(CpuContext c, IMemory m)
    {
        if (++_seen8011B3D0 == 1)
            System.Console.Error.WriteLine("[Seen] func_8011B3D0 entered");
        else if (_seen8011B3D0 % 3000 == 0)
            System.Console.Error.WriteLine($"[Seen] func_8011B3D0 x{_seen8011B3D0}");
    }

    static int _seen80154A5C;
    public static void Seen80154A5C(CpuContext c, IMemory m)
    {
        if (++_seen80154A5C == 1)
            System.Console.Error.WriteLine("[Seen] func_80154A5C entered");
        else if (_seen80154A5C % 3000 == 0)
            System.Console.Error.WriteLine($"[Seen] func_80154A5C x{_seen80154A5C}");
    }

    static int _seen8013C05C;
    public static void Seen8013C05C(CpuContext c, IMemory m)
    {
        if (++_seen8013C05C == 1)
            System.Console.Error.WriteLine("[Seen] func_8013C05C entered");
        else if (_seen8013C05C % 3000 == 0)
            System.Console.Error.WriteLine($"[Seen] func_8013C05C x{_seen8013C05C}");
    }

    static uint _menuStruct;
    static int _menuTicks;
    static int _lastSel = -99;
    static int _lastLock = -99;

    /// <summary>
    /// func_80134D14 is the menu's per-frame tick: it sweeps input into a
    /// struct, then acts on [struct+0x14] (the chosen button, -1 = none) unless
    /// [struct+0x84] (a lockout counter) is holding it off. a0 is the struct.
    /// </summary>
    static readonly System.Collections.Generic.HashSet<ulong> _structModes = new();
    static int _hitsThisTick;

    public static void MenuTickEnter(CpuContext c, IMemory m)
    {
        _menuStruct = c.A0;
        _hitsThisTick = 0;
        try
        {
            uint mode = m.ReadU32(_menuStruct + 0x10u);
            if (_structModes.Add(((ulong)_menuStruct << 32) | mode))
                System.Console.Error.WriteLine(
                    $"[Menu] tick struct=0x{_menuStruct:X8} uses mode={(int)mode}");
        }
        catch { }
    }

    public static void MenuTickExit(CpuContext c, IMemory m)
    {
        _menuTicks++;
        if (_menuStruct == 0) return;
        int sel, lockout;
        try
        {
            sel = (int)m.ReadU32(_menuStruct + 0x14u);
            lockout = (int)m.ReadU32(_menuStruct + 0x84u);
        }
        catch { return; }
        if (_hitsThisTick > 0)
            System.Console.Error.WriteLine(
                $"[Menu] tick #{_menuTicks} had {_hitsThisTick} hit(s) -> selected={sel} " +
                $"lockout={lockout} struct=0x{_menuStruct:X8}");
        if (sel != _lastSel || lockout != _lastLock || _menuTicks % 1800 == 0)
        {
            _lastSel = sel; _lastLock = lockout;
            System.Console.Error.WriteLine(
                $"[Menu] tick #{_menuTicks} struct=0x{_menuStruct:X8} " +
                $"selected={sel} lockout={lockout}");
        }
    }

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
        // RA identifies the caller, which is the quickest way to find which
        // screen code is actually polling input at any moment.
        uint key = (c.RA & 0xFFFFFF) | (_lastA0 << 24);
        if (_seenIds.Add(key))
            System.Console.Error.WriteLine(
                $"[Query] id 0x{_lastA0:X2} mode {_lastA1} asked from RA=0x{c.RA:X8}");
    }

    public static void AfterButtonQuery(CpuContext c, IMemory m)
    {
        _q1++;
        if ((c.V0 & 0xFF) != 0)
        {
            _hitsThisTick++;
            System.Console.Error.WriteLine(
                $"[Query] HIT id=0x{_lastA0:X2} mode={_lastA1} " +
                $"(menuStruct=0x{_menuStruct:X8})");
        }
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
