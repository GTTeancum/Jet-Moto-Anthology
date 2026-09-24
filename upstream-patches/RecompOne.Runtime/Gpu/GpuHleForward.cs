using RecompOne.Runtime.Hle;

namespace RecompOne.Runtime;

public sealed partial class Gpu
{
    private static bool HleOn => GpuHle.Active && GpuHle.Backend is { Ready: true };
    private static readonly bool AuditBackground = Environment.GetEnvironmentVariable("JETMOTO_AUDIT_UNLIT") == "1";
    private static readonly HashSet<string> BackgroundRecords = [];
    private static readonly long SpanAuditStart=long.TryParse(Environment.GetEnvironmentVariable("JETMOTO_AUDIT_FRAME_START"),out var spanStart)?spanStart:long.MaxValue;
    private static readonly long SpanAuditEnd=long.TryParse(Environment.GetEnvironmentVariable("JETMOTO_AUDIT_FRAME_END"),out var spanEnd)?spanEnd:-1;
    private static int _spanAuditCount;
    private void AuditDraw(string description)
    {
        if (!AuditBackground || Interrupts.VBlankCount is <4700 or >4706) return;
        string record=$"fifo={_fifoBase:X8} {description}";
        if(BackgroundRecords.Add(record))Console.WriteLine("[JetMoto:background] "+record);
    }

    private int CurTPage()
    {
        return ((_texPageX / 64) & 0xf) | (((_texPageY / 256) & 1) << 4)
                                        | ((_blendMode & 3) << 5) | ((_texDepth & 3) << 7);
    }

    private HleDrawEnv CurEnv()
    {
        return new HleDrawEnv
        {
            ClipX0 = _drawAreaLeft, ClipY0 = _drawAreaTop, ClipX1 = _drawAreaRight, ClipY1 = _drawAreaBottom,
            TwMaskX = _texWinMaskX, TwMaskY = _texWinMaskY, TwOffX = _texWinOffX, TwOffY = _texWinOffY,
            // Preserve _dither for guest GPUSTAT readback only; never forward it for rendering.
            SetMask = _setMask, CheckMask = _checkMask, Dither = false
        };
    }

    private static HleVertex HV(in Vert v, bool invalidW)
    {
        var x = v.Precise ? v.Px : v.X;
        var y = v.Precise ? v.Py : v.Y;

        return new HleVertex
        {
            X = x,
            Y = y,
            Z = invalidW ? 1f : v.Pw,
            HasGteZ = !invalidW,
            Depth = v.Transform != 0 ? v.Pw : 0f,
            R = (byte)v.R, G = (byte)v.G, B = (byte)v.B, U = (short)v.U, V = (short)v.V,
            Transform = v.Transform
        };
    }

    private PrimFlags PrimOf(bool tex, bool semi, bool raw, int clut, bool gouraud = false)
    {
        return new PrimFlags
        {
            Textured = tex, SemiTrans = semi, RawTexture = raw, Gouraud = gouraud, TPage = (ushort)CurTPage(),
            Clut = (ushort)clut,
            NativeTexture = tex ? Assets.Native.NativeTextureBindings.Resolve(_fifoBase) : null,
            WorldSurface = Assets.Native.WorldSurfaceBindings.Resolve(_fifoBase),
            DrawOffsetX=_drawOffsetX,DrawOffsetY=_drawOffsetY
        };
    }

    private void HleTri(in Vert a, in Vert b, in Vert c, bool invalidW, bool tex, bool gouraud, bool semi, bool raw, int clut)
    {
        var spanX = Math.Max(a.X, Math.Max(b.X, c.X)) - Math.Min(a.X, Math.Min(b.X, c.X));
        var spanY = Math.Max(a.Y, Math.Max(b.Y, c.Y)) - Math.Min(a.Y, Math.Min(b.Y, c.Y));
        var primitive=PrimOf(tex,semi,raw,clut,gouraud);
        // The host rasterizer clips large, source-verified world triangles.
        // Applying the PS1 span rejection here punches camera-dependent holes
        // in nearby terrain/water. Keep guest limits for unverified primitives.
        bool worldClip=primitive.WorldSurface is {Kind:1 or 2 or 4};
        if ((spanX > 1023 || spanY > 511) && !worldClip)
        {
            if(Interrupts.VBlankCount>=SpanAuditStart&&Interrupts.VBlankCount<=SpanAuditEnd&&_spanAuditCount++<2000)
            {
                var flags=primitive;
                Console.WriteLine("[JetMoto:span-reject] "+System.Text.Json.JsonSerializer.Serialize(new {
                    vblank=Interrupts.VBlankCount,kind=flags.WorldSurface?.Kind,material=flags.NativeTexture?.Asset.Key,
                    offset=new[]{_drawOffsetX,_drawOffsetY},span=new[]{spanX,spanY},invalidW,
                    vertices=new[]{new[]{a.Precise?a.Px:a.X,a.Precise?a.Py:a.Y,a.Pw},
                        new[]{b.Precise?b.Px:b.X,b.Precise?b.Py:b.Y,b.Pw},
                        new[]{c.Precise?c.Px:c.X,c.Precise?c.Py:c.Y,c.Pw}}
                }));
            }
            return;
        }
        
        var be = GpuHle.Backend!;
        be.SetDrawEnv(CurEnv());
        if(AuditBackground && spanX*spanY>10000 && Assets.Native.WorldSurfaceBindings.Resolve(_fifoBase)==null)
            AuditDraw($"triangle tex={tex} semi={semi} points={a.X},{a.Y}/{b.X},{b.Y}/{c.X},{c.Y} rgb={a.R},{a.G},{a.B}");
        be.DrawTri(HV(a, invalidW), HV(b, invalidW), HV(c, invalidW), primitive);
    }

    private void HleRect(int x, int y, int w, int h, int u, int v, int clut, int r, int g, int b, bool tex, bool semi,
        bool raw)
    {
        var be = GpuHle.Backend!;
        if(AuditBackground && w*h>10000)AuditDraw($"rect {x},{y},{w},{h} tex={tex} rgb={r},{g},{b}");
        be.SetDrawEnv(CurEnv());
        var flags = PrimOf(tex, semi, raw, clut);
        be.DrawRect(
            new HleRect
            {
                X = x, Y = y, W = w, H = h, U = (short)u, V = (short)v, R = (byte)r, G = (byte)g, B = (byte)b
            },
            flags);
        if (!tex && !raw && Assets.Native.WorldSurfaceBindings.ResolveRect(x,y,w,h) is { } water && water.H > 0)
            be.DrawRect(
                new HleRect { X = x, Y = water.Y, W = w, H = water.H, R = 128, G = 128, B = 128 },
                new PrimFlags { WorldSurface = water.Surface, DrawOffsetX = _drawOffsetX, DrawOffsetY = _drawOffsetY });
    }

    private void HleLine(int x0, int y0, int r0, int g0, int b0, int x1, int y1, int r1, int g1, int b1, bool semi,
        bool gouraud)
    {
        if (Math.Abs(x1 - x0) > 1023 || Math.Abs(y1 - y0) > 511) return;

        var be = GpuHle.Backend!;
        be.SetDrawEnv(CurEnv());
        be.DrawLine(
            new HleVertex { X = x0, Y = y0, R = (byte)r0, G = (byte)g0, B = (byte)b0 },
            new HleVertex { X = x1, Y = y1, R = (byte)r1, G = (byte)g1, B = (byte)b1 },
            PrimOf(false, semi, false, 0, gouraud));
    }

    private void HleFill(int x, int y, int w, int h, ushort color)
    {
        if(AuditBackground)AuditDraw($"fill {x},{y},{w},{h} rgb15={color:X4}");
        var be=GpuHle.Backend!;
        be.FillRect(x, y, w, h, color);
        if (Assets.Native.WorldSurfaceBindings.ResolveFill(x,y,w,h,color) is not { } surface) return;
        be.SetDrawEnv(CurEnv());
        be.DrawRect(new HleRect{X=x,Y=y,W=w,H=h,R=128,G=128,B=128},new PrimFlags{WorldSurface=surface,DrawOffsetX=_drawOffsetX,DrawOffsetY=_drawOffsetY});
    }

    private void HleCopy(int sx, int sy, int dx, int dy, int w, int h)
    {
        GpuHle.Backend!.CopyVram(sx, sy, dx, dy, w, h);
    }

    private ushort[] _readBuf = Array.Empty<ushort>();

    private void HleReadback(int x, int y, int w, int h)
    {
        var n = w * h;
        if (_readBuf.Length < n) _readBuf = new ushort[n];
        var buf = _readBuf;

        if (Host.GpuJobs.Claimed && !Host.GpuJobs.IsOwner)
        {
            Host.GpuJobs.Run(() => GpuHle.Backend!.ReadVram(x, y, w, h, buf));
        }
        else
        {
            GpuHle.Backend!.ReadVram(x, y, w, h, buf);
        }

        for (var row = 0; row < h; row++)
        {
            var dst = ((y + row) & (VramHeight - 1)) * VramWidth;
            for (var col = 0; col < w; col++)
                Vram[dst + ((x + col) & (VramWidth - 1))] = _readBuf[row * w + col];
        }

        Assets.Textures.VramTracker.MarkCpuWrite(x, y, w, h);
    }

    //img load
    private ushort[] _hleLoad = Array.Empty<ushort>();
    private bool _hleLoadActive;
    private int _hleLoadPos;

    private void HleLoadBegin()
    {
        _hleLoadActive = HleOn;
        if (!_hleLoadActive) return;
        var n = _loadW * _loadH;
        if (_hleLoad.Length < n) _hleLoad = new ushort[n];
        _hleLoadPos = 0;
    }

    private void HleLoadPut(ushort value)
    {
        if (_hleLoadActive && _hleLoadPos < _hleLoad.Length) _hleLoad[_hleLoadPos++] = value;
    }

    private void HleLoadFlush()
    {
        if (!_hleLoadActive) return;
        GpuHle.Backend!.WriteVram(_loadX, _loadY, _loadW, _loadH, _hleLoad.AsSpan(0, _loadW * _loadH));
        _hleLoadActive = false;
    }
}
