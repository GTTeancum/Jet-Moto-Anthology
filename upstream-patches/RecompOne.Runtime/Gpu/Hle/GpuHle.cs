namespace RecompOne.Runtime.Hle;

public static class GpuHle
{
    public static bool Active { get; set; }
    public static IGpuBackend? Backend { get; set; }

    // Set by a source-anchored pause scope while its retained scene is displayed.
    public static bool RetainDisplayMargins { get; set; }
    public static float WideAspect { get; set; }
    public static float OutputAspect { get; set; } = 4f / 3f;

    public static float SourceAspect { get; set; } = 4f / 3f;
    public static int LastDisplayW { get; set; }
    public static int LastDisplayH { get; set; }
    public static float TargetAspect { get; set; } = 4f / 3f;
    public const float BaseAspect = 4f / 3f;

    public struct DispRect
    {
        public int X, Y, W, H;
        public long Stamp;
        public bool Valid;
    }

    private static readonly DispRect[] _rects = new DispRect[2];
    private static readonly DispRect[] _frozen = new DispRect[2];
    private static long _stamp;
    private static long _frozenVersion;
    private static bool _holding;
    
    public static void Hold()
    {
        Array.Copy(_rects, _frozen, _rects.Length);
        _frozenVersion = RectVersion;
        _holding = true;
    }
    
    public static void Release()
    {
        _holding = false;
    }

    public static void NotifyDisplay(int x, int y, int w, int h)
    {
        if (w <= 0 || h <= 0) return;
        var slot = -1;
        for (var i = 0; i < _rects.Length; i++)
            if (_rects[i].Valid && _rects[i].X == x && _rects[i].Y == y)
            {
                slot = i;
                break;
            }

        if (slot < 0)
        {
            slot = 0;
            for (var i = 1; i < _rects.Length; i++)
                if (!_rects[i].Valid || _rects[i].Stamp < _rects[slot].Stamp)
                    slot = i;
        }

        _rects[slot] = new DispRect { X = x, Y = y, W = w, H = h, Stamp = ++_stamp, Valid = true };
        RectVersion++;
    }

    public static long RectVersion { get; private set; }
    
    public static long ViewVersion => _holding ? _frozenVersion : RectVersion;

    public static int RectCount => _rects.Length;

    public static DispRect GetRect(int i)
    {
        return _holding ? _frozen[i] : _rects[i];
    }

    public static int WideMargin(int w)
    {
        if (WideAspect <= 0f) return 0;
        var source = SourceAspect > 0f ? SourceAspect : BaseAspect;
        var wide = (int)MathF.Ceiling(w * WideAspect / source);
        return Math.Max(0, (wide - w + 1) / 2);
    }
}