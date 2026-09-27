using System.Runtime.InteropServices;
using Silk.NET.OpenGL;

namespace RecompOne.Runtime.Hle;

public sealed class GlCore : IGpuBackend
{
    [StructLayout(LayoutKind.Sequential)]
    private struct GlVertex
    {
        public float X, Y;
        public float R, G, B;
        public float Clut, Texpage;
        public float U, V;
        public float W;
        public float WX,WY,WZ,WQ;
        public float NX,NY,NZ,SurfaceKind;
    }

    private const int MaxVerts = 0x40000;

    private readonly GL _gl;
    private readonly IGlVram _vram;
    private readonly List<uint> _images = [];
    private readonly Dictionary<int, Assets.Native.NativeTextureAsset> _nativeImages = [];
    private sealed record NativeGpuTexture(uint Handle, bool Coverage);
    private readonly Assets.Native.ResidentCache<Assets.Native.NativeTextureAsset, NativeGpuTexture> _nativeTextures;
    private bool _preparingTexture;
    public (long Bytes, long PeakBytes, long Evictions) NativeCacheStats =>
        (_nativeTextures.Bytes, _nativeTextures.PeakBytes, _nativeTextures.Evictions);
    private readonly GlDisplayRt?[] _rts = new GlDisplayRt?[2];
    private long _rtStamp;
    private long _frame;
    private readonly string? _captureDirectory = Environment.GetEnvironmentVariable("JETMOTO_CAPTURE_DIR");
    private readonly bool _captureRaw=Environment.GetEnvironmentVariable("JETMOTO_CAPTURE_RAW")=="1";
    private long _lastCapture = -1;
    private readonly bool _captureRaceRelative=Environment.GetEnvironmentVariable("JETMOTO_CAPTURE_RACE_RELATIVE")=="1";
    private long _captureRaceStart=long.MinValue;
    private readonly int _effectCaptureFrames = int.TryParse(Environment.GetEnvironmentVariable("JETMOTO_CAPTURE_EFFECT_FRAMES"), out int frames) ? Math.Clamp(frames, 0, 8) : 0;
    private long _effectDrawVblank = -1, _lastEffectCaptureFrame = -1;
    private int _effectFrameTriangles, _effectCaptures;
    private bool _effectCaptureStarted;
    private int _wakeDraws, _wakeVisibleDraws, _wakeMinLight=255, _wakeMaxLight;
    private readonly long _auditStart=long.TryParse(Environment.GetEnvironmentVariable("JETMOTO_AUDIT_FRAME_START"),out long auditStart) ? auditStart : long.MaxValue;
    private readonly long _auditEnd=long.TryParse(Environment.GetEnvironmentVariable("JETMOTO_AUDIT_FRAME_END"),out long auditEnd) ? auditEnd : -1;
    private readonly Dictionary<string,int[]> _auditWater=[],_auditEffects=[];
    private Assets.Native.WorldCamera? _auditCamera;
    private int _auditUnderlays;
    private readonly float _auditPointX=float.TryParse(Environment.GetEnvironmentVariable("JETMOTO_AUDIT_POINT_X"),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out float auditX) ? auditX : float.NaN;
    private readonly float _auditPointY=float.TryParse(Environment.GetEnvironmentVariable("JETMOTO_AUDIT_POINT_Y"),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out float auditY) ? auditY : float.NaN;
    private readonly List<object> _auditPointHits=[];
    private readonly bool _traceGeometry=Environment.GetEnvironmentVariable("JETMOTO_TRACE_GEOMETRY")=="1";
    private readonly long _traceStart=long.TryParse(Environment.GetEnvironmentVariable("JETMOTO_CAPTURE_START"),out long traceStart) ? Math.Max(0,traceStart-60) : 0;
    private readonly long _traceEnd=long.TryParse(Environment.GetEnvironmentVariable("JETMOTO_CAPTURE_END"),out long traceEnd) ? traceEnd : long.MaxValue;
    private sealed class GeometryTrace(long frame)
    {
        public long Frame=frame;
        public readonly List<object> Triangles=[];
        public bool Truncated;
    }
    private readonly Dictionary<GlDisplayRt,GeometryTrace> _geometryTargets=[];
    private readonly List<object> _geometryPending=[];
    private bool _geometryPendingTruncated;
    private long _geometryOrder;
    private GlDisplayRt? _geometryLastTarget;
    private void TraceFlush(GlDisplayRt? target)
    {
        if(!_traceGeometry)return;
        foreach(var old in _geometryTargets.Keys.Where(t=>!_rts.Contains(t)).ToArray())_geometryTargets.Remove(old);
        if(target!=null)
        {
            // A game image can span several host frame counters. The native
            // double-buffer switch, not AdvanceFrame, starts its draw sequence.
            if(!_geometryTargets.TryGetValue(target,out var trace)||!ReferenceEquals(target,_geometryLastTarget))
                _geometryTargets[target]=trace=new GeometryTrace(_frame);
            trace.Frame=_frame;
            _geometryLastTarget=target;
            int remaining=Math.Max(0,20000-trace.Triangles.Count);
            trace.Triangles.AddRange(_geometryPending.Take(remaining));
            trace.Truncated|=_geometryPendingTruncated||_geometryPending.Count>remaining;
        }
        _geometryPending.Clear();_geometryPendingTruncated=false;
    }
    private void TraceTriangle(in HleVertex a,in HleVertex b,in HleVertex c,in PrimFlags flags,string primitive="triangle")
    {
        if(!_traceGeometry)return;
        if(_captureRaceRelative&&_captureRaceStart==long.MinValue)return;
        long clock=Interrupts.VBlankCount-(_captureRaceRelative?_captureRaceStart:0);
        if(clock<_traceStart||clock>_traceEnd)return;
        if(_geometryPending.Count>=20000){_geometryPendingTruncated=true;return;}
        static float[] Vertex(in HleVertex v)=>[v.X,v.Y,v.Z,v.HasGteZ?1:0,v.U,v.V];
        var surface=flags.WorldSurface;
        _geometryPending.Add(new {order=_geometryOrder++,primitive,kind=surface?.Kind,material=flags.NativeTexture?.Asset.Key,
            semi=flags.SemiTrans,blend=flags.BlendMode,textured=flags.Textured,ot=flags.OtIndex,
            offset=new[]{flags.DrawOffsetX,flags.DrawOffsetY},clip=new[]{_env.ClipX0,_env.ClipY0,_env.ClipX1,_env.ClipY1},
            plane=surface is null ? null : new[]{surface.Point.X,surface.Point.Y,surface.Point.Z},
            vertices=new[]{Vertex(a),Vertex(b),Vertex(c)}});
    }
    private bool AuditFrame=>Interrupts.VBlankCount>=_auditStart && Interrupts.VBlankCount<=_auditEnd;

    private void AuditPoint(in HleVertex a,in HleVertex b,in HleVertex c,in PrimFlags flags)
    {
        if(!AuditFrame || !float.IsFinite(_auditPointX+_auditPointY) || _auditPointHits.Count>=128)return;
        float x=_auditPointX+flags.DrawOffsetX,y=_auditPointY+flags.DrawOffsetY;
        // Record geometric coverage before clipping: the render target expands
        // the native horizontal scissor for Hor+ margins during Flush.
        static float Edge(float ax,float ay,float bx,float by,float px,float py)=>(bx-ax)*(py-ay)-(by-ay)*(px-ax);
        float area=Edge(a.X,a.Y,b.X,b.Y,c.X,c.Y);
        if(!float.IsFinite(area)||MathF.Abs(area)<.0001f)return;
        float u=Edge(b.X,b.Y,c.X,c.Y,x,y)/area,v=Edge(c.X,c.Y,a.X,a.Y,x,y)/area,w=1-u-v;
        if(u<0||v<0||w<0)return;
        float? inverseDepth=null;
        if(flags.WorldSurface is {} surface && surface.ProjectiveAtPixel(_auditPointX,_auditPointY,out var q))inverseDepth=q.W;
        _auditPointHits.Add(new{order=_auditPointHits.Count,kind=flags.WorldSurface?.Kind,
            material=flags.NativeTexture?.Asset.Key,textured=flags.Textured,semi=flags.SemiTrans,blend=flags.BlendMode,
            ot=flags.OtIndex,page=flags.TPage,inverseDepth,clip=new[]{_env.ClipX0,_env.ClipY0,_env.ClipX1,_env.ClipY1},
            rgb=new[]{a.R*u+b.R*v+c.R*w,a.G*u+b.G*v+c.G*w,a.B*u+b.B*v+c.B*w}});
    }

    private void AuditPrimitive(in PrimFlags flags,float minX,float minY,float maxX,float maxY)
    {
        if(!AuditFrame || _emittingAirborne)return;
        bool effect=flags.NativeTexture?.Asset.AllowsEffectCoverage==true;
        bool water=flags.WorldSurface is {Kind:2 or 4} && !effect;
        if(!effect && !water)return;
        var records=effect ? _auditEffects : _auditWater;
        string key=flags.NativeTexture?.Asset.Key ?? "untextured-water";
        if(!records.TryGetValue(key,out var counts))records[key]=counts=new int[2];
        counts[0]++;
        if(maxX>=_env.ClipX0 && minX<=_env.ClipX1 && maxY>=_env.ClipY0 && minY<=_env.ClipY1)counts[1]++;
        if(water){_auditCamera=flags.WorldSurface!.Camera;if(flags.WorldSurface.ScreenFill)_auditUnderlays++;}
    }
    
    public void AdvanceFrame()
    {
        if(AuditFrame || _auditWater.Count>0 || _auditEffects.Count>0)
        {
            var eye=_auditCamera?.Eye;
            Console.WriteLine("[JetMoto:frame-audit] "+System.Text.Json.JsonSerializer.Serialize(new {
                vblank=Interrupts.VBlankCount,renderFrame=_frame,water=_auditWater,effects=_auditEffects,
                pointHits=_auditPointHits,
                underlays=_auditUnderlays,eye=eye is {} p ? new[]{p.X,p.Y,p.Z}:null,time=_auditCamera?.Time,
                riders=_auditCamera?.Riders.OrderBy(r=>r.Key).Select(r=>new {
                    id=r.Key,min=new[]{r.Value.Min.X,r.Value.Min.Y,r.Value.Min.Z},
                    max=new[]{r.Value.Max.X,r.Value.Max.Y,r.Value.Max.Z},samples=r.Value.Samples}),
                motionTime=_motionCamera?.Time,motionView=_motionCamera?.ViewSlot,
                pathSegments=_motionSegments.GroupBy(s=>s.RiderId).ToDictionary(g=>g.Key,g=>g.Count()),
                worldWakeEnabled=_worldWakeEnabled,wakeRasterSegments=_wakeRasterSegments,
                wakeProbe=_worldWakeEnabled && _auditCamera!=null ? DescribeWakeProbe() : null,
                sprayProbe=_worldWakeEnabled && _auditCamera!=null ? DescribeSprayProbe() : null,
                note="counts are submitted and viewport-overlapping primitives, not visible pixels or racer attribution"}));
            _auditWater.Clear();_auditEffects.Clear();_auditUnderlays=0;_auditCamera=null;
            _auditPointHits.Clear();
        }
        _frame++;
        if (_diagnoseSurfaces && _frame % 120 == 0)
        {
            Console.WriteLine($"[JetMoto:wake-window] vblank={Interrupts.VBlankCount} renderFrame={_frame} submitted={_wakeDraws} visible={_wakeVisibleDraws} lightMin={(_wakeDraws==0 ? -1 : _wakeMinLight)} lightMax={(_wakeDraws==0 ? -1 : _wakeMaxLight)}");
            _wakeDraws=_wakeVisibleDraws=_wakeMaxLight=0;_wakeMinLight=255;
        }
    }


    private uint _vao, _vbo, _presentVao, _presentVbo, _progPrim, _progPresent, _progPresent24;
    private uint _presentFbo, _presentTex;
    private int _presentW, _presentH;
    private bool _presentNearest;

    private uint _postProg, _postFbo, _postTex;
    private int _postW, _postH, _postVersion = -1;
    private int _uPostTexSize, _uPostOutputSize, _uPostTime, _uPostFrame;
    private int _postFrame, _postParamVersion = -1;
    private (string Name, float Value)[] _postParams = [];
    private int[] _postParamLoc = [];
    private readonly System.Diagnostics.Stopwatch _postClock = System.Diagnostics.Stopwatch.StartNew();

    private readonly GlVertex[] _verts = new GlVertex[MaxVerts];
    private int _count;
    private float _drawMinX, _drawMinY, _drawMaxX, _drawMaxY;

    private HleDrawEnv _env;

    private GlDisplayRt? _kTarget;
    private bool _kSamplesVram;
    private int _kTexX0, _kTexY0, _kTexX1, _kTexY1;
    private bool _vramDirty;
    private int _vdX0, _vdY0, _vdX1, _vdY1;
    private bool _kTransparent;
    private int _kImage = -1;
    private int _kBlend, _kSetMask, _kCheckMask;
    private int _kTwAndX, _kTwAndY, _kTwOrX, _kTwOrY;
    private int _kClipX0, _kClipY0, _kClipX1, _kClipY1;
    private uint _kRepTex, _kRepClut;
    private float _kRepX, _kRepY, _kRepW, _kRepH;
    private int _kRepClutCount;
    private int _uTexWindow, _uBlend, _uBlendOpaque, _uSetMask, _uCheckMask, _uPosBias, _uFbInv;
    private int _uRepRect, _uRepClutCount, _uRepCoverage;
    private int _uCutoutBlend;
    private bool _kCutout, _pendingCutout;
    private Assets.Native.WorldCamera? _worldCamera;
    private readonly Dictionary<int, Assets.Native.RiderMotionHistory> _riderMotion = [];
    private Assets.Native.WorldCamera? _motionCamera;
    private IReadOnlyList<Assets.Native.RiderPathSegment> _motionSegments = [];
    private IReadOnlyList<Assets.Native.SprayParticle> _sprayParticles = [];
    private readonly HashSet<int> _sprayActors = [];
    private readonly Dictionary<int,int> _spraySubmitted = [];
    private Assets.Native.WorldSurface? _sprayProjection;
    private bool _emittingWorldSpray, _pendingWorldSpray, _kWorldSpray;
    private readonly Dictionary<Assets.Native.WorldScene,uint> _worldMaps=[];
    private uint _waterDetail;
    private readonly bool _worldWakeEnabled = Environment.GetEnvironmentVariable("JETMOTO_WORLD_WAKE") == "1";
    private uint _waterWake;
    private Assets.Native.WorldCamera? _wakeCamera;
    private Assets.Native.WorldSurface? _wakeSurface;
    private byte[]? _wakePixels;
    private int _uWakeBounds, _uWakeEnabled, _wakeRasterSegments;
    private uint _riderShadow;
    private ushort[]? _shadowDepths;
    private byte[]? _shadowPixels;
    private Assets.Native.WorldCamera? _shadowCamera;
    private int _uWorldBounds,_uWorldHeightRange,_uWorldLight,_uWorldEye,_uWorldTime,_uEffectTime,_uWaterTint;
    // Reference path exists only for pixel equivalence and timing comparisons.
    private bool _kRepCoverage, _pendingRepCoverage;
    private bool _kAirborne, _pendingAirborne, _emittingAirborne;
    private bool _kWaterWake, _pendingWaterWake;
    private int _uPresentOrigin, _uPresentSize, _uPresentTexSize, _uPresent24Origin, _uPresent24Size;

    public bool Ready { get; private set; }

    private readonly bool _legacy;
    private int _uVramSize, _uDestSize, _uSemiTrans, _uBlendMode;

    public GlCore(GL gl, IGlVram vram, bool legacy = false)
    {
        _gl = gl;
        _vram = vram;
        _legacy = legacy;
        _nativeTextures = new(
            Assets.Native.ResidentCache<Assets.Native.NativeTextureAsset, NativeGpuTexture>.BudgetFromEnvironment("JETMOTO_GPU_TEXTURE_MB", 128),
            texture => _gl.DeleteTexture(texture.Handle));
    }

    public unsafe void InitGl()
    {
        // Jet Moto: neither the guest draw-mode bit nor host GL may add dithering.
        _gl.Disable(EnableCap.Dither);
        _vram.Init();

        var primVs = _legacy ? GlShaders.PrimVs120 : GlShaders.PrimVs;
        var primFs = _legacy ? GlShaders.PrimFs120 : GlShaders.PrimFs;
        var fullVs = _legacy ? GlShaders.FullscreenVs120 : GlShaders.FullscreenVs;
        var presentFs = _legacy ? GlShaders.PresentFs120 : GlShaders.PresentFs;
        var present24Fs = _legacy ? GlShaders.Present24Fs120 : GlShaders.Present24Fs;

        _progPrim = GlShaders.BuildPrim(_gl, primVs, primFs, "prim");
        _progPresent = GlShaders.BuildFullscreen(_gl, fullVs, presentFs, "present");
        _progPresent24 = GlShaders.BuildFullscreen(_gl, fullVs, present24Fs, "present24");
        if (_progPrim == 0 || _progPresent == 0 || _progPresent24 == 0) return;

        _uVramSize = _gl.GetUniformLocation(_progPrim, "uVramSize");
        _uDestSize = _gl.GetUniformLocation(_progPrim, "uDestSize");
        _uSemiTrans = _gl.GetUniformLocation(_progPrim, "uSemiTrans");
        _uBlendMode = _gl.GetUniformLocation(_progPrim, "uBlendMode");

        _uTexWindow = _gl.GetUniformLocation(_progPrim, "uTexWindow");
        _uBlend = _gl.GetUniformLocation(_progPrim, "uBlend");
        _uBlendOpaque = _gl.GetUniformLocation(_progPrim, "uBlendOpaque");
        _uSetMask = _gl.GetUniformLocation(_progPrim, "uSetMask");
        _uCheckMask = _gl.GetUniformLocation(_progPrim, "uCheckMask");
        _uPosBias = _gl.GetUniformLocation(_progPrim, "uPosBias");
        _uFbInv = _gl.GetUniformLocation(_progPrim, "uFbInv");
        _uRepRect = _gl.GetUniformLocation(_progPrim, "uRepRect");
        _uRepCoverage = _gl.GetUniformLocation(_progPrim, "uRepCoverage");
        _uCutoutBlend = _gl.GetUniformLocation(_progPrim, "uCutoutBlend");
        _uRepClutCount = _gl.GetUniformLocation(_progPrim, "uRepClutCount");

        _uWorldBounds=_gl.GetUniformLocation(_progPrim,"uWorldBounds");
        _uWorldHeightRange=_gl.GetUniformLocation(_progPrim,"uWorldHeightRange");
        _uWorldLight=_gl.GetUniformLocation(_progPrim,"uWorldLight");
        _uWorldEye=_gl.GetUniformLocation(_progPrim,"uWorldEye");
        _uWorldTime=_gl.GetUniformLocation(_progPrim,"uWorldTime");
        _uEffectTime=_gl.GetUniformLocation(_progPrim,"uEffectTime");
        _uWaterTint=_gl.GetUniformLocation(_progPrim,"uWaterTint");
        _uWakeBounds=_gl.GetUniformLocation(_progPrim,"uWakeBounds");
        _uWakeEnabled=_gl.GetUniformLocation(_progPrim,"uWakeEnabled");
        _gl.UseProgram(_progPrim);
        _gl.Uniform1(_gl.GetUniformLocation(_progPrim,"uWorldHeight"),5);
        _gl.Uniform1(_gl.GetUniformLocation(_progPrim,"uWaterDetail"),6);
        _gl.Uniform1(_gl.GetUniformLocation(_progPrim,"uRiderShadow"),7);
        _gl.Uniform1(_gl.GetUniformLocation(_progPrim,"uWaterWake"),8);
        _gl.Uniform1(_gl.GetUniformLocation(_progPrim, "uVram"), 0);
        _gl.Uniform1(_gl.GetUniformLocation(_progPrim, "uDest"), 1);
        _gl.Uniform1(_gl.GetUniformLocation(_progPrim, "uExtTex"), 2);
        _gl.Uniform1(_gl.GetUniformLocation(_progPrim, "uRepTex"), 3);
        _gl.Uniform1(_gl.GetUniformLocation(_progPrim, "uRepClut"), 4);
        SetScaleUniform(_progPrim);
        if (_uVramSize >= 0) _gl.Uniform2(_uVramSize, (float)GlVram.Width, GlVram.Height);

        _uPresentOrigin = _gl.GetUniformLocation(_progPresent, "uOrigin");
        _uPresentSize = _gl.GetUniformLocation(_progPresent, "uSize");
        _uPresentTexSize = _gl.GetUniformLocation(_progPresent, "uTexSize");
        _gl.UseProgram(_progPresent);
        _gl.Uniform1(_gl.GetUniformLocation(_progPresent, "uVram"), 0);

        _uPresent24Origin = _gl.GetUniformLocation(_progPresent24, "uOrigin");
        _uPresent24Size = _gl.GetUniformLocation(_progPresent24, "uSize");
        _gl.UseProgram(_progPresent24);
        _gl.Uniform1(_gl.GetUniformLocation(_progPresent24, "uVram"), 0);
        SetScaleUniform(_progPresent24);
        var uVramSize24 = _gl.GetUniformLocation(_progPresent24, "uVramSize");
        if (uVramSize24 >= 0) _gl.Uniform2(uVramSize24, (float)GlVram.Width, GlVram.Height);

        _vao = _gl.GenVertexArray();
        _vbo = _gl.GenBuffer();
        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(MaxVerts * sizeof(GlVertex)), null,
            BufferUsageARB.DynamicDraw);
        var stride = (uint)sizeof(GlVertex);
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, (void*)0);
        _gl.EnableVertexAttribArray(1);
        _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, (void*)8);
        _gl.EnableVertexAttribArray(2);
        _gl.VertexAttribPointer(2, 1, VertexAttribPointerType.Float, false, stride, (void*)20);
        _gl.EnableVertexAttribArray(3);
        _gl.VertexAttribPointer(3, 1, VertexAttribPointerType.Float, false, stride, (void*)24);
        _gl.EnableVertexAttribArray(4);
        _gl.VertexAttribPointer(4, 2, VertexAttribPointerType.Float, false, stride, (void*)28);
        _gl.EnableVertexAttribArray(5);
        _gl.VertexAttribPointer(5, 1, VertexAttribPointerType.Float, false, stride, (void*)36);
        _gl.EnableVertexAttribArray(6);_gl.VertexAttribPointer(6,4,VertexAttribPointerType.Float,false,stride,(void*)40);
        _gl.EnableVertexAttribArray(7);_gl.VertexAttribPointer(7,4,VertexAttribPointerType.Float,false,stride,(void*)56);

        // fullscreen quad for present, real vbo since gl_VertexID without arrays does not draw on mesa for some reason?? or i did it wrong?
        _presentVao = _gl.GenVertexArray();
        _presentVbo = _gl.GenBuffer();
        _gl.BindVertexArray(_presentVao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _presentVbo);
        float[] quad = { -1f, -1f, 1f, -1f, -1f, 1f, 1f, 1f };
        fixed (float* qp = quad)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(quad.Length * sizeof(float)), qp,
                BufferUsageARB.StaticDraw);
        }

        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), (void*)0);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);

        _presentTex = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _presentTex);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        _presentFbo = _gl.GenFramebuffer();
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _presentFbo);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
            TextureTarget.Texture2D, _presentTex, 0);
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

        _kClipX1 = 1023;
        _kClipY1 = 511;
        Ready = true;
    }

    public void SetDrawEnv(in HleDrawEnv env)
    {
        _env = env;
    }

    private const int FbSlackW = 64;
    private const int FbSlackH = 32;

    private int _clsClipX0 = int.MinValue, _clsClipY0, _clsClipX1, _clsClipY1;
    private long _clsVersion = -1;
    private float _clsWideAspect = float.NaN;
    private GlDisplayRt? _clsResult;

    private void InvalidateClassify()
    {
        _clsClipX0 = int.MinValue;
    }

    private GlDisplayRt? Classify()
    {
        if (_clsClipX0 == _env.ClipX0 && _clsClipY0 == _env.ClipY0 && _clsClipX1 == _env.ClipX1 &&
            _clsClipY1 == _env.ClipY1 && _clsVersion == GpuHle.ViewVersion && _clsWideAspect == GpuHle.WideAspect)
        {
            if (_clsResult != null) _clsResult.Stamp = ++_rtStamp;
            return _clsResult;
        }

        _clsClipX0 = _env.ClipX0;
        _clsClipY0 = _env.ClipY0;
        _clsClipX1 = _env.ClipX1;
        _clsClipY1 = _env.ClipY1;
        _clsVersion = GpuHle.ViewVersion;
        _clsWideAspect = GpuHle.WideAspect;
        _clsResult = ClassifySlow();
        return _clsResult;
    }

    private GlDisplayRt? ClassifySlow()
    {
        int clipX = _env.ClipX0, clipY = _env.ClipY0;
        int clipW = _env.ClipX1 - _env.ClipX0 + 1, clipH = _env.ClipY1 - _env.ClipY0 + 1;
        if (clipW <= 0 || clipH <= 0) return null;

        long bestStamp = -1;
        int fbX = 0, fbY = 0, fbW = 0, fbH = 0;
        for (var i = 0; i < GpuHle.RectCount; i++)
        {
            var r = GpuHle.GetRect(i);
            if (!r.Valid || r.W <= 0 || r.H <= 0 || r.Stamp <= bestStamp) continue;

            var clipInside = clipX >= r.X && clipX + clipW <= r.X + r.W && clipY >= r.Y && clipY + clipH <= r.Y + r.H;
            var clipIsFb = clipX <= r.X && clipX + clipW >= r.X + r.W && clipY <= r.Y && clipY + clipH >= r.Y + r.H &&
                           clipW - r.W <= FbSlackW && clipH - r.H <= FbSlackH;
            if (clipInside)
            {
                bestStamp = r.Stamp;
                fbX = r.X;
                fbY = r.Y;
                fbW = r.W;
                fbH = r.H;
            }
            else if (clipIsFb)
            {
                bestStamp = r.Stamp;
                fbX = clipX;
                fbY = clipY;
                fbW = clipW;
                fbH = clipH;
            }
        }

        return bestStamp < 0 ? null : GetOrCreateRt(fbX, fbY, fbW, fbH);
    }

    private GlDisplayRt GetOrCreateRt(int fbX, int fbY, int fbW, int fbH)
    {
        var slot = -1;
        for (var i = 0; i < _rts.Length; i++)
            if (_rts[i] is { } rt && rt.X == fbX && rt.Y == fbY)
            {
                var sameW = rt.W == fbW;
                var fitsH = rt.H >= fbH && rt.H - fbH <= FbSlackH;
                if (sameW && fitsH && rt.Margin == GpuHle.WideMargin(rt.W))
                {
                    rt.Stamp = ++_rtStamp;
                    return rt;
                }

                slot = i;
                break;
            }

        if (slot < 0)
        {
            slot = 0;
            for (var i = 1; i < _rts.Length; i++)
            {
                if (_rts[i] == null)
                {
                    slot = i;
                    break;
                }

                if (_rts[slot] != null && _rts[i]!.Stamp < _rts[slot]!.Stamp) slot = i;
            }
        }

        if (_rts[slot] is { } old)
        {
            // A mode/size transition must finish the old batch before deleting its FBO.
            if (_count > 0) Flush();
            if (old.Dirty) Writeback(old);
            old.Destroy(_gl);
            InvalidateClassify();
        }

        var fresh = new GlDisplayRt
        {
            X = fbX, Y = fbY, W = fbW, H = fbH, Margin = GpuHle.WideMargin(fbW), Stamp = ++_rtStamp,
            LastDrawFrame = _frame
        };
        fresh.Create(_gl);
        _rts[slot] = fresh;
        SyncRtFromVram(fresh, fbX, fbY, fbW, fbH);
        return fresh;
    }

    private void Writeback(GlDisplayRt rt)
    {
        var s = GlVram.Scale;
        _gl.Disable(EnableCap.ScissorTest);
        _gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, rt.Fbo);
        _gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, _vram.Fbo);
        _gl.BlitFramebuffer(rt.Margin * s, 0, (rt.Margin + rt.W) * s, rt.H * s,
            rt.X * s, rt.Y * s, (rt.X + rt.W) * s, (rt.Y + rt.H) * s,
            ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        rt.Dirty = false;
        Assets.Textures.VramTracker.MarkGpuWrite(rt.X, rt.Y, rt.W, rt.H);
    }

    private void SyncRtFromVram(GlDisplayRt rt, int rx, int ry, int rw, int rh)
    {
        int x0 = Math.Max(rx, rt.X), y0 = Math.Max(ry, rt.Y);
        int x1 = Math.Min(rx + rw, rt.X + rt.W), y1 = Math.Min(ry + rh, rt.Y + rt.H);
        if (x0 >= x1 || y0 >= y1) return;
        var s = GlVram.Scale;
        _gl.Disable(EnableCap.ScissorTest);
        _gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _vram.Fbo);
        _gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, rt.Fbo);
        _gl.BlitFramebuffer(x0 * s, y0 * s, x1 * s, y1 * s,
            (x0 - rt.X + rt.Margin) * s, (y0 - rt.Y) * s, (x1 - rt.X + rt.Margin) * s, (y1 - rt.Y) * s,
            ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    private void WritebackDirtyIntersecting(int x, int y, int w, int h)
    {
        foreach (var rt in _rts)
            if (rt is { Dirty: true } && rt.Intersects(x, y, w, h))
                Writeback(rt);
    }

    private void SyncRtsFromVram(int x, int y, int w, int h)
    {
        foreach (var rt in _rts)
            if (rt != null && rt.Intersects(x, y, w, h))
                SyncRtFromVram(rt, x, y, w, h);
    }

    private void CheckTextureFeedback(in PrimFlags f)
    {
        if (!f.Textured || f.UseImage) return;
        var px = (f.TPage & 0xF) * 64;
        var py = ((f.TPage >> 4) & 1) * 256;
        var depth = (f.TPage >> 7) & 3;
        var pw = depth == 0 ? 64 : depth == 1 ? 128 : 256;
        foreach (var rt in _rts)
            if (rt is { Dirty: true } && rt.Intersects(px, py, pw, 256))
            {
                Flush();
                Writeback(rt);
            }
    }

    private bool DesiredMatches(bool transparent, int blend, int image)
    {
        int twAndX = ~(_env.TwMaskX * 8) & 0xFF, twAndY = ~(_env.TwMaskY * 8) & 0xFF;
        int twOrX = (_env.TwOffX & _env.TwMaskX) * 8, twOrY = (_env.TwOffY & _env.TwMaskY) * 8;
        return _kRepTex == _pendingRepTex && _kRepClut == _pendingRepClut && _kRepCoverage == _pendingRepCoverage && _kCutout == _pendingCutout && _kAirborne == _pendingAirborne && _kWaterWake == _pendingWaterWake && _kWorldSpray == _pendingWorldSpray
                                          && (_pendingRepTex == 0 || (_kRepX == _pendingRepX && _kRepY == _pendingRepY
                                              && _kRepW == _pendingRepW && _kRepH == _pendingRepH))
                                          && _kTransparent == transparent && _kBlend == blend && _kImage == image
                                          && _kSetMask == (_env.SetMask ? 1 : 0) &&
                                          _kCheckMask == (_env.CheckMask ? 1 : 0)
                                          && _kTwAndX == twAndX && _kTwAndY == twAndY && _kTwOrX == twOrX &&
                                          _kTwOrY == twOrY
                                          && _kClipX0 == _env.ClipX0 && _kClipY0 == _env.ClipY0 &&
                                          _kClipX1 == _env.ClipX1 && _kClipY1 == _env.ClipY1;
    }

    private float _kViewShiftX;

    private static bool IsHalfView(GlDisplayRt rt, int x0, int x1) =>
        x1 - x0 + 1 == rt.W / 2 && (x0 == rt.X || x0 == rt.X + rt.W / 2);

    // Side-by-side gameplay needs a new centre for each camera, not a stretched
    // image or a one-sided extension. Keep the shared divider fixed. All geometry
    // and HUD positions inside that viewport receive only this translation.
    private float ViewShift(GlDisplayRt? rt)
    {
        if (rt is not { Margin: > 0 } || !IsHalfView(rt, _env.ClipX0, _env.ClipX1)) return 0;
        float source = GpuHle.SourceAspect > 0 ? GpuHle.SourceAspect : GpuHle.BaseAspect;
        float shift = rt.W * (GpuHle.WideAspect / source - 1f) / 4f;
        return _env.ClipX0 == rt.X ? -shift : shift;
    }

    private bool IsSplitDivider(float x0, float y0, float x1, float y1, in PrimFlags f)
    {
        if (_kTarget is not { Margin: > 0 } rt || !IsHalfView(rt, _env.ClipX0, _env.ClipX1) ||
            f.Textured || f.UseImage || f.SemiTrans || f.WorldSurface != null ||
            y0 > Math.Max(rt.Y, _env.ClipY0) || y1 < Math.Min(rt.Y + rt.H, _env.ClipY1 + 1)) return false;
        int centre = rt.X + rt.W / 2;
        // The original viewport-edge border is screen furniture, not camera
        // geometry. Its two one-pixel halves must meet at the shared divider.
        return _env.ClipX0 == rt.X ? x0 >= centre - 1 && x1 <= centre && x1 > x0
            : x0 >= centre && x1 <= centre + 1 && x1 > x0;
    }

    private void Begin(in PrimFlags f, int vertsNeeded)
    {
        var nextCamera=f.NativeTexture?.Asset.AllowsEffectCoverage==true ? null : f.WorldSurface?.Camera;
        if(_count>0 && !ReferenceEquals(_worldCamera,nextCamera))Flush();
        _worldCamera=nextCamera;
        var transparent = f.SemiTrans;
        var blend = f.BlendMode;
        var image = f.UseImage ? f.Image : -1;
        var target = Classify();
        if (_count > 0 && (target != _kTarget || !DesiredMatches(transparent, blend, image))) Flush();
        // Legacy GL composites against a destination snapshot. Each coverage
        // primitive must see earlier overlapping particles, not a stale batch.
        if (_legacy && _count > 0 && (_kRepCoverage || _pendingRepCoverage)) Flush();
        if (_count > 0 && (_kCutout || _pendingCutout)) Flush();
        if (_count + vertsNeeded > MaxVerts) Flush();
        CheckTextureFeedback(f);

        if (target != null) ClearMargin(target);

        _kTarget = target;
        _kViewShiftX = ViewShift(target);
        _kImage = image;
        _kTransparent = transparent;
        _kBlend = blend;
        _kSetMask = _env.SetMask ? 1 : 0;
        _kCheckMask = _env.CheckMask ? 1 : 0;
        _kTwAndX = ~(_env.TwMaskX * 8) & 0xFF;
        _kTwAndY = ~(_env.TwMaskY * 8) & 0xFF;
        _kTwOrX = (_env.TwOffX & _env.TwMaskX) * 8;
        _kTwOrY = (_env.TwOffY & _env.TwMaskY) * 8;
        _kClipX0 = _env.ClipX0;
        _kClipY0 = _env.ClipY0;
        _kClipX1 = _env.ClipX1;
        _kClipY1 = _env.ClipY1;
        _kRepTex = _pendingRepTex;
        _kRepCoverage = _pendingRepCoverage;
        _kCutout = _pendingCutout;
        _kAirborne = _pendingAirborne;
        _kWorldSpray = _pendingWorldSpray;
        _kWaterWake = _pendingWaterWake;
        _kRepClut = _pendingRepClut;
        _kRepClutCount = _pendingRepClutCount;
        _kRepX = _pendingRepX;
        _kRepY = _pendingRepY;
        _kRepW = _pendingRepW;
        _kRepH = _pendingRepH;

        if (f.Textured && !f.UseImage && _pendingRepTex == 0)
        {
            var depth = (f.TPage >> 7) & 3;
            AddTexRect((f.TPage & 0xF) * 64, ((f.TPage >> 4) & 1) * 256,
                depth == 0 ? 64 : depth == 1 ? 128 : 256, 256);
            if (depth < 2)
                AddTexRect((f.Clut & 0x3F) * 16, (f.Clut >> 6) & 0x1FF, depth == 0 ? 16 : 256, 1);
        }
    }

    private void AddTexRect(int x, int y, int w, int h)
    {
        if (!_kSamplesVram)
        {
            _kSamplesVram = true;
            _kTexX0 = x;
            _kTexY0 = y;
            _kTexX1 = x + w;
            _kTexY1 = y + h;
            return;
        }

        if (x < _kTexX0) _kTexX0 = x;
        if (y < _kTexY0) _kTexY0 = y;
        if (x + w > _kTexX1) _kTexX1 = x + w;
        if (y + h > _kTexY1) _kTexY1 = y + h;
    }

    private bool SampledRegionIsDirty()
    {
        return _vramDirty && _kTexX0 < _vdX1 && _vdX0 < _kTexX1 && _kTexY0 < _vdY1 && _vdY0 < _kTexY1;
    }

    private uint _pendingRepTex, _pendingRepClut;
    private readonly bool _diagnoseSurfaces = Environment.GetEnvironmentVariable("JETMOTO_DIAG_SURFACES") == "1";
    private int _effectDiagnosticCount;
    private readonly long _effectDiagnosticStart = long.TryParse(Environment.GetEnvironmentVariable("JETMOTO_DIAG_EFFECT_START"), out long start) ? start : 0;
    private readonly long _effectDiagnosticEnd = long.TryParse(Environment.GetEnvironmentVariable("JETMOTO_DIAG_EFFECT_END"), out long end) ? end : long.MaxValue;
    private int _streakDiagnosticCount;
    private readonly HashSet<string> _streakDiagnosticMaterials=[];
    private int _pendingRepClutCount = 16;
    private float _pendingRepX, _pendingRepY, _pendingRepW = 1, _pendingRepH = 1;

    private readonly Dictionary<Assets.ReplacementTexture, uint> _repTextures = [];
    private readonly Dictionary<Assets.ReplacementClut, uint> _repCluts = [];

    private void ResolveReplacement(in PrimFlags f, int uMin, int vMin, int uMax, int vMax)
    {
        _pendingRepTex = 0;
        _pendingRepClut = 0;
        _pendingRepCoverage = false;
        _pendingCutout = false;
        _pendingAirborne = false;
        _pendingWorldSpray = false;
        _pendingWaterWake = false;

        if (!f.Textured || f.UseImage) return;

        int twAndX = ~(_env.TwMaskX * 8) & 0xFF, twAndY = ~(_env.TwMaskY * 8) & 0xFF;
        int twOrX = (_env.TwOffX & _env.TwMaskX) * 8, twOrY = (_env.TwOffY & _env.TwMaskY) * 8;

        // Build06: the original model material selects the image, not a live VRAM hash.
        if (f.NativeTexture is {} native && native.Accepts(f.TPage, f.Clut, uMin, vMin, uMax, vMax,
                twAndX, twAndY, twOrX, twOrY) && EnsureNativeTexture(native.Asset) is {} replacement)
        {
            _pendingRepTex = replacement.Handle;
            _pendingRepCoverage = replacement.Coverage;
            _pendingCutout = native.Asset.SmoothCutout && !replacement.Coverage;
            _pendingAirborne = replacement.Coverage && _emittingAirborne;
            _pendingWorldSpray = replacement.Coverage && _emittingWorldSpray;
            _pendingWaterWake = replacement.Coverage && native.Asset.WaterSpray && !_emittingAirborne && !_emittingWorldSpray;
            _pendingRepX = native.U0; _pendingRepY = native.V0;
            _pendingRepW = native.Width; _pendingRepH = native.Height;
            // Native effects reveal successive atlas slices as their geometry grows.
            // Authored coverage sprites contain a complete plume, not that atlas.
            if (replacement.Coverage && native.Asset.SourceWidth == 64 && native.Asset.SourceHeight == 64
                && uMax > uMin && vMax > vMin)
            {
                _pendingRepX = uMin; _pendingRepY = vMin;
                _pendingRepW = uMax-uMin; _pendingRepH = vMax-vMin;
            }
            return;
        }
        if (Assets.Native.NativeTextureBindings.OriginalAssetsOnly)
        {
            if (f.NativeTexture != null) System.Threading.Interlocked.Increment(ref Assets.Native.NativeTextureBindings.RejectedDraws);
            return; // Original hardware sampling, NEVER VRAM replacement matching.
        }

        if (!Assets.Textures.TextureResolver.Resolve(f.TPage, f.Clut, uMin, vMin, uMax, vMax,
                twAndX, twAndY, twOrX, twOrY, out var res))
            return;

        if (res.Texture is { Mode: Assets.TextureMode.Rgba } tex)
        {
            _pendingRepTex = EnsureRepTexture(tex);
            _pendingRepX = res.Rect.U0;
            _pendingRepY = res.Rect.V0;
            _pendingRepW = res.Rect.W;
            _pendingRepH = res.Rect.H;
        }

        if (_pendingRepTex == 0 && res.Clut is { } clut && res.Rect.ClutCount > 0 && clut.Count == res.Rect.ClutCount)
        {
            _pendingRepClut = EnsureRepClut(clut);
            _pendingRepClutCount = clut.Count;
        }
    }

    private unsafe uint EnsureRepTexture(Assets.ReplacementTexture tex)
    {
        if (_repTextures.TryGetValue(tex, out var handle)) return handle;

        _gl.ActiveTexture(TextureUnit.Texture7);
        handle = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, handle);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)(tex.Nearest ? GLEnum.Nearest : GLEnum.Linear));
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)(tex.Nearest ? GLEnum.Nearest : GLEnum.Linear));
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        _gl.TexImage2D<byte>(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)tex.Width, (uint)tex.Height, 0,
            PixelFormat.Rgba, PixelType.UnsignedByte, tex.Rgba);
        _gl.ActiveTexture(TextureUnit.Texture0);

        _repTextures[tex] = handle;
        return handle;
    }

    private NativeGpuTexture? EnsureNativeTexture(Assets.Native.NativeTextureAsset asset)
    {
        if (_nativeTextures.TryGet(asset, out var cached)) return cached;
        // Finish any batch holding a handle before eviction. Recorded frames keep
        // asset identities, so an evicted image can be reloaded safely on replay.
        Flush();
        var image = asset.GetTexture();
        if (image == null) return null;
        _gl.ActiveTexture(TextureUnit.Texture7);
        uint handle = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, handle);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)(image.Nearest ? GLEnum.Nearest : GLEnum.Linear));
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)(image.Nearest ? GLEnum.Nearest : GLEnum.Linear));
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        _gl.TexImage2D<byte>(TextureTarget.Texture2D, 0, InternalFormat.Rgba8,
            (uint)image.Width, (uint)image.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, image.Rgba);
        _gl.ActiveTexture(TextureUnit.Texture0);
        var entry = new NativeGpuTexture(handle, image.Coverage);
        _nativeTextures.Add(asset, entry, (long)image.Width * image.Height * 4);
        Diagnostics.PerformanceDiagnostics.Texture(_preparingTexture, _nativeTextures.Bytes, _nativeTextures.PeakBytes, _nativeTextures.Evictions);
        return entry;
    }

    public bool PrepareNativeTexture(Assets.Native.NativeTextureAsset asset)
    {
        bool previous = _preparingTexture; _preparingTexture = true;
        try { return EnsureNativeTexture(asset) != null; }
        finally { _preparingTexture = previous; }
    }
    public int RegisterNativeImage(Assets.Native.NativeTextureAsset asset)
    {
        if (!PrepareNativeTexture(asset)) return -1;
        int id = _images.Count;
        _images.Add(0); // Stable identity; the residency cache owns its GL handle.
        _nativeImages.Add(id, asset);
        return id;
    }

    private unsafe uint EnsureRepClut(Assets.ReplacementClut clut)
    {
        if (_repCluts.TryGetValue(clut, out var handle)) return handle;

        _gl.ActiveTexture(TextureUnit.Texture7);
        handle = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, handle);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        _gl.TexImage2D<byte>(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)clut.Count, 1, 0,
            PixelFormat.Rgba, PixelType.UnsignedByte, clut.Rgba);
        _gl.ActiveTexture(TextureUnit.Texture0);

        _repCluts[clut] = handle;
        return handle;
    }

    private uint WorldMap(Assets.Native.WorldScene scene)
    {
        if(_worldMaps.TryGetValue(scene,out uint texture))return texture;
        texture=_gl.GenTexture();_gl.ActiveTexture(TextureUnit.Texture5);_gl.BindTexture(TextureTarget.Texture2D,texture);
        _gl.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureMinFilter,(int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureMagFilter,(int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureWrapS,(int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureWrapT,(int)GLEnum.ClampToEdge);
        _gl.TexImage2D<byte>(TextureTarget.Texture2D,0,InternalFormat.Rgba8,(uint)scene.MapSize,(uint)scene.MapSize,0,PixelFormat.Rgba,PixelType.UnsignedByte,scene.HeightRgba);
        _worldMaps.Add(scene,texture);_gl.ActiveTexture(TextureUnit.Texture0);return texture;
    }
    private void UpdateRiderMotion(Assets.Native.WorldCamera camera)
    {
        if (!ReferenceEquals(camera, _motionCamera))
        {
            if (!_riderMotion.TryGetValue(camera.ViewSlot, out var motion))
                _riderMotion[camera.ViewSlot] = motion = new Assets.Native.RiderMotionHistory();
            _motionSegments = motion.Observe(camera);
            _sprayParticles = _worldWakeEnabled ? Assets.Native.RiderSpray.Evaluate(camera,_motionSegments) : [];
            _sprayActors.Clear();
            _spraySubmitted.Clear();
            _motionCamera = camera;
        }
    }
    private void SetWorldUniforms()
    {
        if(_worldCamera is not {} camera)return;
        UpdateRiderMotion(camera);
        BindWaterDetail();
        BindRiderShadow(camera);
        BindWaterWake(camera);
        var scene=camera.Scene;uint texture=WorldMap(scene);
        _gl.ActiveTexture(TextureUnit.Texture5);_gl.BindTexture(TextureTarget.Texture2D,texture);
        _gl.Uniform4(_uWorldBounds,scene.Origin.X,scene.Origin.Y,scene.Extent.X,scene.Extent.Y);
        _gl.Uniform2(_uWorldHeightRange,scene.HeightRange.X,scene.HeightRange.Y);
        _gl.Uniform3(_uWorldLight,scene.Sunlight.X,scene.Sunlight.Y,scene.Sunlight.Z);
        _gl.Uniform3(_uWorldEye,camera.Eye.X,camera.Eye.Y,camera.Eye.Z);
        if(_uWaterTint>=0)_gl.Uniform3(_uWaterTint,scene.WaterTint.X,scene.WaterTint.Y,scene.WaterTint.Z);
        _gl.Uniform1(_uWorldTime,camera.Time);_gl.ActiveTexture(TextureUnit.Texture0);
    }

    private unsafe void BindWaterDetail()
    {
        _gl.ActiveTexture(TextureUnit.Texture6);
        if (_waterDetail == 0)
        {
            _waterDetail = _gl.GenTexture();
            _gl.BindTexture(TextureTarget.Texture2D, _waterDetail);
            var data = Assets.Native.WaterDetailData.Create();
            fixed (byte* p = data)
                _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8,
                    Assets.Native.WaterDetailData.Size, Assets.Native.WaterDetailData.Size, 0,
                    PixelFormat.Rgba, PixelType.UnsignedByte, p);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.LinearMipmapLinear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.Repeat);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.Repeat);
            _gl.GenerateMipmap(TextureTarget.Texture2D);
            // Oblique water footprints are long and narrow. Isotropic mipmaps
            // erase resolvable cross-ripple detail along with the distant axis.
            bool anisotropic;
            if (_legacy)
            {
                var extensions=(_gl.GetStringS(StringName.Extensions) ?? "").Split(' ',StringSplitOptions.RemoveEmptyEntries);
                anisotropic=extensions.Contains("GL_EXT_texture_filter_anisotropic") || extensions.Contains("GL_ARB_texture_filter_anisotropic");
            }
            else
            {
                anisotropic=false;
                int count=_gl.GetInteger(GLEnum.NumExtensions);
                for(uint i=0;i<count;i++)
                    if(_gl.GetStringS(StringName.Extensions,i) is "GL_EXT_texture_filter_anisotropic" or "GL_ARB_texture_filter_anisotropic")
                    {anisotropic=true;break;}
            }
            float samples=1;
            if(anisotropic)
            {
                samples=Math.Clamp(_gl.GetFloat((GLEnum)0x84FF),1,16);
                _gl.TexParameter(TextureTarget.Texture2D,(TextureParameterName)0x84FE,samples);
            }
            Console.WriteLine($"[JetMoto:water-detail] mipmapped anisotropy={samples}");
        }
        else _gl.BindTexture(TextureTarget.Texture2D, _waterDetail);
    }

    private void BindWaterWake(Assets.Native.WorldCamera camera)
    {
        bool enabled = _worldWakeEnabled && camera.Scene.FlatWaterLevel.HasValue;
        _gl.Uniform1(_uWakeEnabled, enabled ? 1f : 0f);
        if (!enabled) return;
        _gl.ActiveTexture(TextureUnit.Texture8);
        if (_waterWake == 0)
        {
            _waterWake = _gl.GenTexture();
            _gl.BindTexture(TextureTarget.Texture2D, _waterWake);
            _gl.TexImage2D<byte>(TextureTarget.Texture2D, 0, InternalFormat.Rgba8,
                Assets.Native.WaterWakeField.Size, Assets.Native.WaterWakeField.Size, 0,
                PixelFormat.Rgba, PixelType.UnsignedByte, ReadOnlySpan<byte>.Empty);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        }
        else _gl.BindTexture(TextureTarget.Texture2D, _waterWake);
        if (!ReferenceEquals(camera, _wakeCamera))
        {
            _wakePixels ??= new byte[Assets.Native.WaterWakeField.Size * Assets.Native.WaterWakeField.Size * 4];
            long wakeStart = Diagnostics.PerformanceDiagnostics.Start();
            _wakeRasterSegments = Assets.Native.WaterWakeField.Rasterize(camera, _motionSegments, _wakePixels);
            Diagnostics.PerformanceDiagnostics.Wake(wakeStart);
            _gl.TexSubImage2D<byte>(TextureTarget.Texture2D, 0, 0, 0,
                Assets.Native.WaterWakeField.Size, Assets.Native.WaterWakeField.Size,
                PixelFormat.Rgba, PixelType.UnsignedByte, _wakePixels);
            _wakeCamera = camera;
        }
        var origin = Assets.Native.WaterWakeField.Origin(camera);
        _gl.Uniform4(_uWakeBounds, origin.X, origin.Y, Assets.Native.WaterWakeField.Extent, camera.Scene.FlatWaterLevel!.Value);
    }

    private object? DescribeWakeProbe()
    {
        if (_wakeCamera is not {} camera || _wakeSurface is not {} surface || _wakePixels==null
            || !ReferenceEquals(camera,surface.Camera) || camera.Scene.FlatWaterLevel is not {} level) return null;
        var origin=Assets.Native.WaterWakeField.Origin(camera);
        var probes=new List<object>();
        foreach(var rider in camera.Riders.OrderBy(p=>System.Numerics.Vector3.DistanceSquared((p.Value.Min+p.Value.Max)*.5f,camera.Eye)).Take(2))
        {
            var path=_motionSegments.Where(s=>s.RiderId==rider.Key).OrderByDescending(s=>s.BornB).FirstOrDefault();
            var delta=path.B-path.A;delta.Z=0;
            if(delta.LengthSquared()<.0001f)continue;
            var heading=System.Numerics.Vector3.Normalize(delta);
            var side=new System.Numerics.Vector3(-heading.Y,heading.X,0);
            foreach(float lateral in new[]{-1.5f,0f,1.5f})
            {
                var point=path.B-heading*6+side*lateral;point.Z=level;
                var view=camera.Rotation.Apply(point)+camera.Translation;
                int x=(int)((point.X-origin.X)*Assets.Native.WaterWakeField.Size/Assets.Native.WaterWakeField.Extent);
                int y=(int)((point.Y-origin.Y)*Assets.Native.WaterWakeField.Size/Assets.Native.WaterWakeField.Extent);
                int[]? coverage=x>=0&&y>=0&&x<Assets.Native.WaterWakeField.Size&&y<Assets.Native.WaterWakeField.Size
                    ? [_wakePixels[(y*Assets.Native.WaterWakeField.Size+x)*4],_wakePixels[(y*Assets.Native.WaterWakeField.Size+x)*4+1]] : null;
                probes.Add(new{id=rider.Key,lateral,world=new[]{point.X,point.Y,point.Z},coverage,
                    pixel=view.Z>0 ? new[]{surface.CenterX+view.X/view.Z*surface.Projection,surface.CenterY+view.Y/view.Z*surface.Projection} : null});
            }
        }
        return new{time=camera.Time,projection=surface.Projection,center=new[]{surface.CenterX,surface.CenterY},samples=probes};
    }

    private unsafe void BindRiderShadow(Assets.Native.WorldCamera camera)
    {
        _gl.ActiveTexture(TextureUnit.Texture7);
        if (_riderShadow == 0)
        {
            _riderShadow = _gl.GenTexture();
            _gl.BindTexture(TextureTarget.Texture2D, _riderShadow);
            _gl.TexImage2D<byte>(TextureTarget.Texture2D, 0, InternalFormat.Rgba8,
                Assets.Native.RiderShadowMap.Size, Assets.Native.RiderShadowMap.Size, 0,
                PixelFormat.Rgba, PixelType.UnsignedByte, ReadOnlySpan<byte>.Empty);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        }
        else _gl.BindTexture(TextureTarget.Texture2D, _riderShadow);
        if (ReferenceEquals(_shadowCamera, camera)) return;
        _shadowDepths ??= new ushort[Assets.Native.RiderShadowMap.Size * Assets.Native.RiderShadowMap.Size];
        _shadowPixels ??= new byte[Assets.Native.RiderShadowMap.Size * Assets.Native.RiderShadowMap.Size * 4];
        long shadowStart = Diagnostics.PerformanceDiagnostics.Start();
        Assets.Native.RiderShadowMap.Rasterize(camera, _shadowDepths, _shadowPixels);
        Diagnostics.PerformanceDiagnostics.Shadow(shadowStart);
        _gl.TexSubImage2D<byte>(TextureTarget.Texture2D, 0, 0, 0,
            Assets.Native.RiderShadowMap.Size, Assets.Native.RiderShadowMap.Size,
            PixelFormat.Rgba, PixelType.UnsignedByte, _shadowPixels);
        _shadowCamera = camera;
    }

    // All primitives are undithered. The shader's former dither flag is not emitted.
    private GlVertex V(in HleVertex v, in PrimFlags f)
    {
        float x = v.X + _kViewShiftX;
        var raw = f.Textured && f.RawTexture;
        float cr = raw ? 128f : v.R, cg = raw ? 128f : v.G, cb = raw ? 128f : v.B;
        var tpage = f.UseImage ? 0x4000 : f.Textured ? f.TPage & 0x1FF : 0x8000;
        if (_pendingRepTex != 0) tpage |= 0x2000;
        else if (_pendingRepClut != 0) tpage |= 0x1000;
        if (_count == 0)
        {
            _drawMinX = _drawMaxX = x;
            _drawMinY = _drawMaxY = v.Y;
        }
        else
        {
            if (x < _drawMinX) _drawMinX = x;
            if (x > _drawMaxX) _drawMaxX = x;
            if (v.Y < _drawMinY) _drawMinY = v.Y;
            if (v.Y > _drawMaxY) _drawMaxY = v.Y;
        }

        var output = new GlVertex
        {
            X = x, Y = v.Y,
            R = cr, G = cg, B = cb,
            Clut = f.Clut & 0x7FFF,
            Texpage = tpage,
            U = v.U, V = v.V,
            W = v.HasGteZ && float.IsFinite(v.Z) && v.Z > 0f ? v.Z : 1f
        };
        // Source-identified effects cannot inherit a static ocean/terrain plane.
        var surface=f.NativeTexture?.Asset.AllowsEffectCoverage==true ? null : f.WorldSurface;
        if(surface is { Kind: 2 or 4 } fill && fill.ProjectiveAtPixel(v.X-f.DrawOffsetX,v.Y-f.DrawOffsetY,out var worldQ))
        {
            if(_worldWakeEnabled && AuditFrame)_wakeSurface=fill;
            output.WX=worldQ.X;output.WY=worldQ.Y;output.WZ=worldQ.Z;output.WQ=worldQ.W;
            output.NX=fill.Normal.X;output.NY=fill.Normal.Y;output.NZ=fill.Normal.Z;
            // Original additive crests carry radiance, not another base-water layer.
            output.SurfaceKind=fill.ScreenFill ? 4.25f :
                fill.Kind==2 && f.SemiTrans && f.BlendMode==1 ? 2.25f : fill.Kind;
        }
        else if(surface is {} world && world.AtPixel(v.X-f.DrawOffsetX,v.Y-f.DrawOffsetY,out var position,out float inverseDepth))
        {
            output.WX=position.X*inverseDepth;output.WY=position.Y*inverseDepth;output.WZ=position.Z*inverseDepth;output.WQ=inverseDepth;
            output.NX=world.Normal.X;output.NY=world.Normal.Y;output.NZ=world.Normal.Z;output.SurfaceKind=world.Kind;
        }
        return output;
    }

    private object? DescribeSprayProbe()
    {
        if(_motionCamera is not {} camera || _sprayProjection is not {} surface)return null;
        return _sprayParticles.GroupBy(p=>p.RiderId).Select(group=>
        {
            int projected=0,inViewport=0;float minY=float.PositiveInfinity,maxY=float.NegativeInfinity,maxRadius=0,maxOpacity=0;
            foreach(var particle in group)
            {
                var view=camera.Rotation.Apply(particle.Position)+camera.Translation;
                if(view.Z<=.1f)continue;
                float x=surface.CenterX+view.X/view.Z*surface.Projection;
                float y=surface.CenterY+view.Y/view.Z*surface.Projection;
                float radius=particle.Radius*surface.Projection/view.Z;
                if(!float.IsFinite(x+y+radius))continue;
                projected++;minY=MathF.Min(minY,y);maxY=MathF.Max(maxY,y);
                maxRadius=MathF.Max(maxRadius,radius);maxOpacity=MathF.Max(maxOpacity,particle.Opacity);
                if(x+radius*2>=0 && x-radius*2<=320 && y+radius*2>=0 && y-radius*2<=240)inViewport++;
            }
            return new {id=group.Key,particles=group.Count(),projected,inNativeViewport=inViewport,
                submitted=_spraySubmitted.GetValueOrDefault(group.Key),
                minY=projected>0 ? (float?)minY:null,maxY=projected>0 ? (float?)maxY:null,maxRadius,maxOpacity};
        }).ToArray();
    }

    private void EmitRiderSpray(Assets.Native.WorldSurface rider,int offsetX,int offsetY)
    {
        var camera=rider.Camera;
        if(camera.Scene.WaterSprayMaterial is not {} material || !camera.Scene.FlatWaterLevel.HasValue)return;
        if(ReferenceEquals(camera,_motionCamera) && _sprayActors.Contains(rider.RiderId))return;
        Flush();
        UpdateRiderMotion(camera);
        if(!_sprayActors.Add(rider.RiderId))return;
        _sprayProjection=rider;
        var flags=new PrimFlags{Textured=true,SemiTrans=true,TPage=material.TPage,Clut=material.Clut,NativeTexture=material};
        // Generated full-sprite UVs do not belong to the native atlas window.
        var savedEnvironment=_env;
        _env.TwMaskX=_env.TwMaskY=_env.TwOffX=_env.TwOffY=0;
        _emittingWorldSpray=true;
        try
        {
            foreach(var particle in _sprayParticles.Where(p=>p.RiderId==rider.RiderId)
                .OrderByDescending(p=>(camera.Rotation.Apply(p.Position)+camera.Translation).Z))
            {
                var view=camera.Rotation.Apply(particle.Position)+camera.Translation;
                if(view.Z<.1f)continue;
                var center=new System.Numerics.Vector2(rider.CenterX+view.X/view.Z*rider.Projection+offsetX,
                    rider.CenterY+view.Y/view.Z*rider.Projection+offsetY);
                float radius=particle.Radius*rider.Projection/view.Z;
                if(!float.IsFinite(radius)||radius<=0)continue;
                // Fade at the near plane instead of allowing expanding opaque streaks.
                float sizeFade=Math.Clamp((10-radius)/6,0,1);
                if(sizeFade<=0)continue;
                var next=camera.Rotation.Apply(particle.Position+particle.Velocity*.015f)+camera.Translation;
                if(next.Z<.1f)continue;
                var along=new System.Numerics.Vector2(next.X/next.Z-view.X/view.Z,next.Y/next.Z-view.Y/view.Z);
                along=along.LengthSquared()>.000001f ? System.Numerics.Vector2.Normalize(along) : new(0,-1);
                var across=new System.Numerics.Vector2(-along.Y,along.X)*radius;
                along*=radius*1.8f;
                if(center.X+radius*2<_env.ClipX0||center.X-radius*2>_env.ClipX1||center.Y+radius*2<_env.ClipY0||center.Y-radius*2>_env.ClipY1)continue;
                byte fade=(byte)Math.Clamp(particle.Opacity*sizeFade*128,1,128);
                HleVertex Vertex(System.Numerics.Vector2 p,float u,float v)=>new(){X=p.X,Y=p.Y,Z=view.Z,HasGteZ=true,U=u,V=v,R=fade,G=fade,B=fade};
                var a=Vertex(center-across-along,0,0);var b=Vertex(center+across-along,63,0);
                var c=Vertex(center-across+along,0,63);var d=Vertex(center+across+along,63,63);
                DrawTri(a,b,c,flags);DrawTri(b,d,c,flags);
                _spraySubmitted[rider.RiderId]=_spraySubmitted.GetValueOrDefault(rider.RiderId)+1;
            }
        }
        finally
        {
            try { Flush(); }
            finally { _env=savedEnvironment;_emittingWorldSpray=false; }
        }
    }

    public void DrawTri(in HleVertex a, in HleVertex b, in HleVertex c, in PrimFlags f)
    {
        if(_worldWakeEnabled && !_emittingWorldSpray)
        {
            if(f.WorldSurface is {Kind:3,RiderId:>=200 and <220} rider)EmitRiderSpray(rider,f.DrawOffsetX,f.DrawOffsetY);
            if(f.NativeTexture?.Asset.WaterSpray==true && _motionCamera?.Scene is {FlatWaterLevel:not null,WaterSprayMaterial:{} material}
                && ReferenceEquals(f.NativeTexture.Asset,material.Asset))return;
        }
        AuditPrimitive(f,Math.Min(a.X,Math.Min(b.X,c.X)),Math.Min(a.Y,Math.Min(b.Y,c.Y)),
            Math.Max(a.X,Math.Max(b.X,c.X)),Math.Max(a.Y,Math.Max(b.Y,c.Y)));
        AuditPoint(a,b,c,f);
        ResolveReplacement(f,
            (int)Math.Min(a.U, Math.Min(b.U, c.U)), (int)Math.Min(a.V, Math.Min(b.V, c.V)),
            (int)Math.Max(a.U, Math.Max(b.U, c.U)), (int)Math.Max(a.V, Math.Max(b.V, c.V)));
        Begin(f, 3);
        TraceTriangle(a,b,c,f);
        int start = _count;
        var wakeA=a;var wakeB=b;var wakeC=c;
        // The retained pause scene is dimmed by a full-screen polygon, not a tile.
        // Extend only its screen-corner vertices; menu text and world geometry keep
        // their original coordinates. Each half of the quad is handled identically.
        if (GpuHle.RetainDisplayMargins && _kTarget is { Margin: > 0 } pauseRt &&
            f.SemiTrans && !f.Textured && !f.UseImage && f.WorldSurface == null &&
            !a.HasGteZ && !b.HasGteZ && !c.HasGteZ &&
            _env.ClipX0 <= pauseRt.X && _env.ClipX1 >= pauseRt.X + pauseRt.W - 1)
        {
            bool Corner(HleVertex v) => (v.X == pauseRt.X || v.X == pauseRt.X + pauseRt.W) &&
                (v.Y == pauseRt.Y || v.Y == pauseRt.Y + pauseRt.H);
            if (Corner(a) && Corner(b) && Corner(c))
            {
                HleVertex Expand(HleVertex v) { v.X += v.X == pauseRt.X ? -pauseRt.Margin : pauseRt.Margin; return v; }
                wakeA=Expand(a);wakeB=Expand(b);wakeC=Expand(c);
            }
        }
        if (_pendingCutout && f.NativeTexture?.Asset.UiTileSize is > 1 and var tile)
        {
            // Native UI tiles use inclusive PS1 UV endpoints (0..31 across 32 pixels).
            // High-resolution replacements need contiguous tile boundaries, including rotation.
            float loU=Math.Min(a.U,Math.Min(b.U,c.U)),hiU=Math.Max(a.U,Math.Max(b.U,c.U));
            float loV=Math.Min(a.V,Math.Min(b.V,c.V)),hiV=Math.Max(a.V,Math.Max(b.V,c.V));
            bool fixU=loU%tile==0 && hiU-loU==tile-1;
            bool fixV=loV%tile==0 && hiV-loV==tile-1;
            HleVertex TileUv(HleVertex v) {
                if(fixU && v.U==hiU)v.U+=1;
                if(fixV && v.V==hiV)v.V+=1;
                return v;
            }
            wakeA=TileUv(a);wakeB=TileUv(b);wakeC=TileUv(c);
        }
        if (_pendingWaterWake)
        {
            if (_diagnoseSurfaces)
            {
                _wakeDraws++;
                if(Math.Max(a.X,Math.Max(b.X,c.X))>=_env.ClipX0 && Math.Min(a.X,Math.Min(b.X,c.X))<=_env.ClipX1
                    && Math.Max(a.Y,Math.Max(b.Y,c.Y))>=_env.ClipY0 && Math.Min(a.Y,Math.Min(b.Y,c.Y))<=_env.ClipY1)
                    _wakeVisibleDraws++;
                int light=f.RawTexture ? 128 : Math.Max(a.R,Math.Max(a.G,a.B));
                _wakeMinLight=Math.Min(_wakeMinLight,light);_wakeMaxLight=Math.Max(_wakeMaxLight,light);
            }
            float duB=b.U-a.U,dvB=b.V-a.V,duC=c.U-a.U,dvC=c.V-a.V;
            float determinant=duB*dvC-duC*dvB;
            if(MathF.Abs(determinant)>.0001f)
            {
                float axisX=((c.X-a.X)*duB-(b.X-a.X)*duC)/determinant;
                float axisY=((c.Y-a.Y)*duB-(b.Y-a.Y)*duC)/determinant;
                float center=(Math.Min(a.V,Math.Min(b.V,c.V))+Math.Max(a.V,Math.Max(b.V,c.V)))*.5f;
                // Overlap soft foam footprints along the trail without moving
                // the water surface or changing the airborne sprite geometry.
                HleVertex Extend(HleVertex v) {
                    float offset=(v.V-center)*.5f;
                    v.X+=axisX*offset;v.Y+=axisY*offset;return v;
                }
                wakeA=Extend(a);wakeB=Extend(b);wakeC=Extend(c);
            }
        }
        _verts[_count++] = V(wakeA, f);
        _verts[_count++] = V(wakeB, f);
        _verts[_count++] = V(wakeC, f);
        if (_pendingRepCoverage)
        {
            long drawVblank = Interrupts.VBlankCount;
            if (_effectDrawVblank != drawVblank) { _effectDrawVblank = drawVblank; _effectFrameTriangles = 0; }
            _effectFrameTriangles++;
        }
        if (_diagnoseSurfaces)
        {
        float diagnosticWidth=Math.Max(a.X,Math.Max(b.X,c.X))-Math.Min(a.X,Math.Min(b.X,c.X));
        float diagnosticHeight=Math.Max(a.Y,Math.Max(b.Y,c.Y))-Math.Min(a.Y,Math.Min(b.Y,c.Y));
        bool diagnosticVisible=Math.Max(a.X,Math.Max(b.X,c.X))>=_env.ClipX0
            && Math.Min(a.X,Math.Min(b.X,c.X))<=_env.ClipX1
            && Math.Max(a.Y,Math.Max(b.Y,c.Y))>=_env.ClipY0
            && Math.Min(a.Y,Math.Min(b.Y,c.Y))<=_env.ClipY1;
        if (_diagnoseSurfaces && Interrupts.VBlankCount>=4800 && Interrupts.VBlankCount<=5100
            && diagnosticVisible && diagnosticHeight>80 && diagnosticWidth>0
            && _streakDiagnosticMaterials.Add($"{f.NativeTexture?.Asset.Key}/{f.WorldSurface?.Kind}") && _streakDiagnosticCount++<32)
            Console.WriteLine($"[JetMoto:streak-draw] vblank={Interrupts.VBlankCount} asset={f.NativeTexture?.Asset.Key} kind={f.WorldSurface?.Kind} " +
                $"a=({a.X},{a.Y};uv={a.U},{a.V};z={a.Z};valid={a.HasGteZ}) b=({b.X},{b.Y};uv={b.U},{b.V};z={b.Z};valid={b.HasGteZ}) c=({c.X},{c.Y};uv={c.U},{c.V};z={c.Z};valid={c.HasGteZ})");
        }
        if (_diagnoseSurfaces && _pendingRepCoverage
            && Interrupts.VBlankCount >= _effectDiagnosticStart && Interrupts.VBlankCount <= _effectDiagnosticEnd
            && _effectDiagnosticCount++ < 96)
        {
            Console.WriteLine($"[JetMoto:effect-draw] vblank={Interrupts.VBlankCount} asset={f.NativeTexture?.Asset.Key} rect={_pendingRepX},{_pendingRepY},{_pendingRepW},{_pendingRepH} blend={_kBlend} semi={_kTransparent} rgb={a.R},{a.G},{a.B} layer={(_kAirborne ? "airborne" : _kWaterWake ? "foam" : "other")} " +
                $"a=({a.X},{a.Y};uv={a.U},{a.V};z={a.Z};valid={a.HasGteZ}) b=({b.X},{b.Y};uv={b.U},{b.V};z={b.Z};valid={b.HasGteZ}) c=({c.X},{c.Y};uv={c.U},{c.V};z={c.Z};valid={c.HasGteZ})");
        }
        // A triangle needs a coherent depth for every corner. Never mix W=1
        // fallback vertices with camera-space depths from unrelated corners.
        if (!a.HasGteZ || !b.HasGteZ || !c.HasGteZ ||
            !float.IsFinite(a.Z) || !float.IsFinite(b.Z) || !float.IsFinite(c.Z) ||
            a.Z <= 0f || b.Z <= 0f || c.Z <= 0f)
            _verts[start].W = _verts[start + 1].W = _verts[start + 2].W = 1f;
        if(_verts[start].SurfaceKind==0||_verts[start+1].SurfaceKind==0||_verts[start+2].SurfaceKind==0)
            _verts[start].SurfaceKind=_verts[start+1].SurfaceKind=_verts[start+2].SurfaceKind=0;
        else if(_verts[start].SurfaceKind>3.5f&&_verts[start].SurfaceKind<4.5f)Assets.Native.WorldSurfaceBindings.WaterTriangles++;
        else if(_verts[start].SurfaceKind>2.5f)Assets.Native.WorldSurfaceBindings.RiderTriangles++;
        else if(_verts[start].SurfaceKind>1.5f)Assets.Native.WorldSurfaceBindings.WaterTriangles++;
        else Assets.Native.WorldSurfaceBindings.LitTriangles++;
        if (!_emittingAirborne && !_emittingWorldSpray && _pendingRepCoverage && f.NativeTexture?.Asset.WaterSpray == true)
        {
            float vMin = Math.Min(a.V, Math.Min(b.V,c.V)), vMax = Math.Max(a.V, Math.Max(b.V,c.V));
            float uSpan = Math.Max(a.U,Math.Max(b.U,c.U))-Math.Min(a.U,Math.Min(b.U,c.U));
            float duB=b.U-a.U, dvB=b.V-a.V, duC=c.U-a.U, dvC=c.V-a.V;
            float determinant=duB*dvC-duC*dvB;
            // Recover the sprite's horizontal texture axis, not the bounding
            // width of one triangle: skewed quads must share the same lift.
            float axisX=((b.X-a.X)*dvC-(c.X-a.X)*dvB)/determinant;
            float axisY=((b.Y-a.Y)*dvC-(c.Y-a.Y)*dvB)/determinant;
            float width=MathF.Sqrt(axisX*axisX+axisY*axisY)*uSpan;
            if (MathF.Abs(determinant)>0.0001f && vMax > vMin && width > 1f && float.IsFinite(width))
            {
                float lift = Math.Min(width * .38f, 64f);
                HleVertex Lift(HleVertex v) { v.Y -= lift * (vMax-v.V)/(vMax-vMin); return v; }
                _emittingAirborne = true;
                try { DrawTri(Lift(a),Lift(b),Lift(c),f); }
                finally { _emittingAirborne = false; }
            }
        }
    }

    public void DrawRect(in HleRect r, in PrimFlags f)
    {
        AuditPrimitive(f,r.X,r.Y,r.X+r.W,r.Y+r.H);
        ResolveReplacement(f, r.U, r.V, r.U + Math.Max(0, r.W - 1), r.V + Math.Max(0, r.H - 1));
        Begin(f, 6);
        float x0 = r.X, x1 = r.X + r.W;
        if (IsSplitDivider(x0, r.Y, x1, r.Y + r.H, f))
        { x0 -= _kViewShiftX; x1 -= _kViewShiftX; }
        // Solid clears/fades which cover the active gameplay viewport also cover
        // its new wings. Blackwater Falls uses this rectangle as its sky. Support
        // full-width, top/bottom and side-by-side viewports, while excluding partial
        // HUD rectangles, textured images and smaller inset/rear-view windows.
        if (GpuHle.WideAspect > 0 && _kTarget is { Margin: > 0 } rt && !f.Textured && !f.UseImage)
        {
            bool semanticWaterUnderlay = f.WorldSurface is { Kind: 4, ScreenFill: true };
            bool fullWidth = _env.ClipX0 <= rt.X && _env.ClipX1 >= rt.X + rt.W - 1;
            bool halfWidth = IsHalfView(rt, _env.ClipX0, _env.ClipX1);
            if ((fullWidth || halfWidth) && x0 <= _env.ClipX0 && x1 >= _env.ClipX1 + 1 &&
                ((r.Y <= Math.Max(rt.Y, _env.ClipY0) && r.Y + r.H >= Math.Min(rt.Y + rt.H, _env.ClipY1 + 1)) ||
                 (semanticWaterUnderlay && r.Y < Math.Min(rt.Y + rt.H, _env.ClipY1 + 1) && r.Y + r.H > Math.Max(rt.Y, _env.ClipY0))))
            {
                int left = _env.ClipX0 <= rt.X ? rt.X - rt.Margin : _env.ClipX0;
                int right = _env.ClipX1 >= rt.X + rt.W - 1 ? rt.X + rt.W + rt.Margin : _env.ClipX1 + 1;
                x0 = Math.Min(x0, left - _kViewShiftX);
                x1 = Math.Max(x1, right - _kViewShiftX);
            }
        }
        var a = new HleVertex { X = x0, Y = r.Y, R = r.R, G = r.G, B = r.B, U = r.U, V = r.V };
        var b = new HleVertex { X = x1, Y = r.Y, R = r.R, G = r.G, B = r.B, U = (short)(r.U + r.W), V = r.V };
        var c = new HleVertex { X = x0, Y = r.Y + r.H, R = r.R, G = r.G, B = r.B, U = r.U, V = (short)(r.V + r.H) };
        var d = new HleVertex
            { X = x1, Y = r.Y + r.H, R = r.R, G = r.G, B = r.B, U = (short)(r.U + r.W), V = (short)(r.V + r.H) };
        TraceTriangle(a,b,c,f,"rectangle");TraceTriangle(b,d,c,f,"rectangle");
        _verts[_count++] = V(a, f);
        _verts[_count++] = V(b, f);
        _verts[_count++] = V(c, f);
        _verts[_count++] = V(b, f);
        _verts[_count++] = V(d, f);
        _verts[_count++] = V(c, f);
    }

    public void DrawLine(in HleVertex a, in HleVertex b, in PrimFlags f)
    {
        _pendingRepTex = 0;
        _pendingRepClut = 0;
        _pendingRepCoverage = false;
        _pendingAirborne = false;
        _pendingWorldSpray = false;
        Begin(f, 6);
        float x1 = a.X, y1 = a.Y;
        float x2 = b.X, y2 = b.Y;
        if (x1 == x2 && IsSplitDivider(x1, Math.Min(y1, y2), x2 + 1, Math.Max(y1, y2) + 1, f))
        { x1 -= _kViewShiftX; x2 -= _kViewShiftX; }
        float dx = x2 - x1, dy = y2 - y1;

        if (dx == 0 && dy == 0)
        {
            LineVert(x1, y1, a, f);
            LineVert(x1 + 1, y1, a, f);
            LineVert(x1 + 1, y1 + 1, a, f);
            LineVert(x1 + 1, y1 + 1, a, f);
            LineVert(x1, y1 + 1, a, f);
            LineVert(x1, y1, a, f);
            return;
        }

        float xo, yo;
        if (Math.Abs(dx) > Math.Abs(dy))
        {
            xo = 0;
            yo = 1;
            if (dx > 0) x2++;
            else x1++;
        }
        else
        {
            xo = 1;
            yo = 0;
            if (dy > 0) y2++;
            else y1++;
        }

        LineVert(x1, y1, a, f);
        LineVert(x2, y2, b, f);
        LineVert(x2 + xo, y2 + yo, b, f);
        LineVert(x2 + xo, y2 + yo, b, f);
        LineVert(x1 + xo, y1 + yo, a, f);
        LineVert(x1, y1, a, f);
    }

    private void LineVert(float x, float y, in HleVertex src, in PrimFlags f)
    {
        var v = src;
        v.X = x;
        v.Y = y;
        _verts[_count++] = V(v, f);
    }

    public void FillRect(int x, int y, int w, int h, ushort color15)
    {
        Flush();
        _vram.Fill(x, y, w, h, color15);
        foreach (var rt in _rts)
        {
            if (rt == null || !rt.Intersects(x, y, w, h)) continue;
            if (rt.Covers(x, y, x + w - 1, y + h - 1))
            {
                FillRtFull(rt, color15);
                if(_traceGeometry)_geometryTargets[rt]=new GeometryTrace(_frame);
                rt.Dirty = false;
                rt.LastDrawFrame = _frame;
            }
            else
            {
                SyncRtFromVram(rt, x, y, w, h);
            }
        }
    }

    private void ClearMargin(GlDisplayRt rt)
    {
        if (GpuHle.RetainDisplayMargins || rt.Margin <= 0 || rt.LastMarginFrame == _frame) return;
        rt.LastMarginFrame = _frame;

        var s = GlVram.Scale;
        var left = rt.Margin * s;
        var right = (rt.Margin + rt.W) * s;

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, rt.Fbo);
        _gl.ClearColor(0f, 0f, 0f, 0f);
        _gl.Enable(EnableCap.ScissorTest);
        _gl.Scissor(0, 0, (uint)left, (uint)rt.TexH);
        _gl.Clear(ClearBufferMask.ColorBufferBit);
        _gl.Scissor(right, 0, (uint)(rt.TexW - right), (uint)rt.TexH);
        _gl.Clear(ClearBufferMask.ColorBufferBit);
        _gl.Disable(EnableCap.ScissorTest);
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    private void FillRtFull(GlDisplayRt rt, ushort color15)
    {
        float r = (color15 & 0x1F) / 31f, g = ((color15 >> 5) & 0x1F) / 31f, b = ((color15 >> 10) & 0x1F) / 31f;
        var a = (color15 & 0x8000) != 0 ? 1f : 0f;
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, rt.Fbo);
        _gl.Disable(EnableCap.ScissorTest);
        _gl.ClearColor(r, g, b, a);
        _gl.Clear(ClearBufferMask.ColorBufferBit);
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    public void CopyVram(int sx, int sy, int dx, int dy, int w, int h)
    {
        Flush();
        GlDisplayRt? retainedSource=null,retainedDestination=null;
        if (GpuHle.RetainDisplayMargins)
            foreach(var rt in _rts)
            {
                if(rt is not {Margin:>0} || rt.W!=w || rt.H!=h)continue;
                if(rt.X==sx && rt.Y==sy)retainedSource=rt;
                if(rt.X==dx && rt.Y==dy)retainedDestination=rt;
            }
        WritebackDirtyIntersecting(sx, sy, w, h);
        _vram.CopyRect(sx, sy, dx, dy, w, h);
        SyncRtsFromVram(dx, dy, w, h);
        // Guest VRAM contains only the original canvas. Pause restores its saved
        // full framebuffer between buffers; carry the native side areas with it.
        if(retainedSource is {} source && retainedDestination is {} destination &&
            source!=destination && source.Margin==destination.Margin)
        {
            _gl.Disable(EnableCap.ScissorTest);
            _gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer,source.Fbo);
            _gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer,destination.Fbo);
            _gl.BlitFramebuffer(0,0,source.TexW,source.TexH,0,0,destination.TexW,destination.TexH,
                ClearBufferMask.ColorBufferBit,BlitFramebufferFilter.Nearest);
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer,0);
            destination.Dirty=true;
            destination.LastDrawFrame=_frame;
        }
    }

    public void WriteVram(int x, int y, int w, int h, ReadOnlySpan<ushort> px)
    {
        Flush();
        _vram.WriteRect(x, y, w, h, px);
        SyncRtsFromVram(x, y, w, h);
    }

    public void ReadVram(int x, int y, int w, int h, Span<ushort> px)
    {
        Flush();
        WritebackDirtyIntersecting(x, y, w, h);
        _vram.ReadRect(x, y, w, h, px);
    }

    public int RegisterImage(ReadOnlySpan<byte> rgba, int width, int height)
    {
        _gl.ActiveTexture(TextureUnit.Texture7);
        var t = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, t);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)width, (uint)height, 0,
            PixelFormat.Rgba, PixelType.UnsignedByte, rgba);
        _gl.ActiveTexture(TextureUnit.Texture0);

        _images.Add(t);
        return _images.Count - 1;
    }

    public void Flush()
    {
        // Reassert even for an empty batch: transfers and presentation also call Flush.
        // This keeps native framebuffer dithering off if another GL user changed state.
        _gl.Disable(EnableCap.Dither);
        if (_count == 0) return;

        var rt = _kTarget;
        uint destTex;
        if (rt == null)
        {
            _vram.BindDraw();
            destTex = _vram.Texture;
        }
        else
        {
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, rt.Fbo);
            _gl.Viewport(0, 0, (uint)rt.TexW, (uint)rt.TexH);
            destTex = rt.Tex;
        }

        var destW = rt == null ? GlVram.Width : rt.TexW;
        var destH = rt == null ? GlVram.Height : rt.TexH;

        GpuGlAccess.Gl = _gl;
        GpuGlAccess.TargetFbo = rt == null ? _vram.Fbo : rt.Fbo;
        GpuGlAccess.TargetWidth = destW;
        GpuGlAccess.TargetHeight = destH;
        GpuGlAccess.TargetOriginX = rt == null ? 0 : rt.X;
        GpuGlAccess.TargetOriginY = rt == null ? 0 : rt.Y;
        GpuGlAccess.TargetMargin = rt == null ? 0 : rt.Margin;

        _gl.Disable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.CullFace);
        _gl.Enable(EnableCap.ScissorTest);
        var s = GlVram.Scale;

        int clipX0, clipY0, clipX1, clipY1;
        if (rt == null)
        {
            clipX0 = _kClipX0;
            clipY0 = _kClipY0;
            clipX1 = _kClipX1;
            clipY1 = _kClipY1;
        }
        else
        {
            clipX0 = _kClipX0 - rt.X + rt.Margin;
            clipY0 = _kClipY0 - rt.Y;
            clipX1 = _kClipX1 - rt.X + rt.Margin;
            clipY1 = _kClipY1 - rt.Y;
            if (rt.Margin > 0 && ((_kClipX0 <= rt.X && _kClipX1 >= rt.X + rt.W - 1) ||
                IsHalfView(rt, _kClipX0, _kClipX1)))
            {
                // Extend only external edges; never cross a split-screen divider.
                if (_kClipX0 <= rt.X) clipX0 = 0;
                if (_kClipX1 >= rt.X + rt.W - 1) clipX1 = rt.Wide1x - 1;
            }
        }

        var bx0 = (int)Math.Floor(_drawMinX) + (rt == null ? 0 : rt.Margin - rt.X);
        var by0 = (int)Math.Floor(_drawMinY) - (rt == null ? 0 : rt.Y);
        var bx1 = (int)Math.Ceiling(_drawMaxX) + (rt == null ? 0 : rt.Margin - rt.X);
        var by1 = (int)Math.Ceiling(_drawMaxY) - (rt == null ? 0 : rt.Y);

        int rx0 = Math.Max(clipX0, bx0), ry0 = Math.Max(clipY0, by0);
        int rx1 = Math.Min(clipX1, bx1), ry1 = Math.Min(clipY1, by1);

        _gl.Scissor(clipX0 * s, clipY0 * s,
            (uint)Math.Max(0, (clipX1 - clipX0 + 1) * s), (uint)Math.Max(0, (clipY1 - clipY0 + 1) * s));

        var readX = Math.Max(0, rx0 * s);
        var readY = Math.Max(0, ry0 * s);
        var readW = Math.Max(0, (rx1 - rx0 + 1) * s);
        var readH = Math.Max(0, (ry1 - ry0 + 1) * s);
        var needDest = _legacy || _kCheckMask != 0 || _kCutout;
        if (needDest)
        {
            destTex = _vram.BeginDestRead(destTex, destW, destH, readX, readY, readW, readH);
            RebindTarget(rt);
            _vramDirty = false;
        }
        else if (rt == null && _kSamplesVram && SampledRegionIsDirty())
        {
            _vram.SampleBarrier();
            _vramDirty = false;
        }

        _gl.UseProgram(_progPrim);
        SetWorldUniforms();
        if(_uEffectTime>=0)_gl.Uniform1(_uEffectTime,_frame/60f);
        _gl.Uniform1(_uRepCoverage, _kCutout ? -1f : _kRepCoverage ? (_kWorldSpray ? 4f : _kAirborne ? 2f : _kWaterWake ? 3f : 1f) : 0f);
        _gl.Uniform2(_uCutoutBlend, !_kTransparent ? 1f : _kBlend switch {0=>.5f,2=>-1f,3=>.25f,_=>1f},
            !_kTransparent ? 0f : _kBlend==0 ? .5f : 1f);
        _gl.BindVertexArray(_vao);
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, _vram.Texture);
        _gl.ActiveTexture(TextureUnit.Texture1);
        _gl.BindTexture(TextureTarget.Texture2D, destTex);
        if (_kImage >= 0 && _kImage < _images.Count)
        {
            // Normal preparation happens at the loader boundary. A cache miss
            // during replay is safe: this batch has no replacement handle yet.
            uint imageHandle = _images[_kImage];
            if (_nativeImages.TryGetValue(_kImage, out var asset))
            {
                int pending = _count; _count = 0;
                try { imageHandle = EnsureNativeTexture(asset)?.Handle ?? 0; }
                finally { _count = pending; }
            }
            _gl.ActiveTexture(TextureUnit.Texture2);
            _gl.BindTexture(TextureTarget.Texture2D, imageHandle);
        }

        if (_kRepTex != 0)
        {
            _gl.ActiveTexture(TextureUnit.Texture3);
            _gl.BindTexture(TextureTarget.Texture2D, _kRepTex);
            _gl.Uniform4(_uRepRect, _kRepX, _kRepY, _kRepW, _kRepH);
        }

        if (_kRepClut != 0)
        {
            _gl.ActiveTexture(TextureUnit.Texture4);
            _gl.BindTexture(TextureTarget.Texture2D, _kRepClut);
            _gl.Uniform1(_uRepClutCount, (float)_kRepClutCount);
        }

        _gl.ActiveTexture(TextureUnit.Texture0);
        if (rt != null)
        {
            _gl.Uniform2(_uPosBias, (float)(rt.Margin - rt.X), (float)-rt.Y);
            _gl.Uniform2(_uFbInv, 2f / rt.Wide1x, 2f / rt.H);
        }
        else
        {
            _gl.Uniform2(_uPosBias, 0f, 0f);
            _gl.Uniform2(_uFbInv, 2f / VramShadow.Width, 2f / VramShadow.Height);
        }

        if (_legacy)
        {
            _gl.Uniform4(_uTexWindow, (float)_kTwAndX, _kTwAndY, _kTwOrX, _kTwOrY);
            _gl.Uniform1(_uSetMask, _kSetMask == 1 ? 1f : 0f);
            _gl.Uniform1(_uCheckMask, _kCheckMask == 1 ? 1f : 0f);
            if (_uDestSize >= 0) _gl.Uniform2(_uDestSize, (float)destW, destH);
            if (_uSemiTrans >= 0) _gl.Uniform1(_uSemiTrans, _kTransparent ? 1f : 0f);
            if (_uBlendMode >= 0) _gl.Uniform1(_uBlendMode, (float)_kBlend);
        }
        else
        {
            _gl.Uniform4(_uTexWindow, _kTwAndX, _kTwAndY, _kTwOrX, _kTwOrY);
            _gl.Uniform1(_uSetMask, _kSetMask == 1 ? 1f : 0f);
            _gl.Uniform1(_uCheckMask, _kCheckMask);
            _gl.Uniform4(_uBlendOpaque, 1f, 1f, 1f, 0f);
        }

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        _gl.BufferSubData<GlVertex>(BufferTargetARB.ArrayBuffer, 0, _verts.AsSpan(0, _count));

        if (_legacy || _kCutout)
        {
            _gl.Disable(EnableCap.Blend);
            _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)_count);
        }
        else if (_kRepCoverage)
        {
            // Coverage blends even when the original polygon opcode is opaque.
            // The shader scales the original src/dst factors by coverage. A
            // subtractive effect needs one pass, because there are no STP/opaque
            // categories mixed inside this explicitly authored material.
            _gl.Enable(EnableCap.Blend);
            _gl.BlendFuncSeparate(BlendingFactor.Src1Color, BlendingFactor.Src1Alpha,
                BlendingFactor.One, BlendingFactor.Zero);
            _gl.BlendEquationSeparate(_kTransparent && _kBlend == 2
                ? BlendEquationModeEXT.FuncReverseSubtract : BlendEquationModeEXT.FuncAdd,
                BlendEquationModeEXT.FuncAdd);
            SetBlend(!_kTransparent ? 1f : _kBlend switch {0=>0.5f,3=>0.25f,_=>1f},
                !_kTransparent ? 0f : _kBlend == 0 ? 0.5f : 1f);
            _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)_count);
        }
        else if (!_kTransparent)
        {
            _gl.Disable(EnableCap.Blend);
            _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)_count);
        }
        else
        {
            _gl.Enable(EnableCap.Blend);
            _gl.BlendFuncSeparate(BlendingFactor.Src1Color, BlendingFactor.Src1Alpha, BlendingFactor.One,
                BlendingFactor.Zero);
            if (_kBlend == 2)
            {
                _gl.BlendEquation(BlendEquationModeEXT.FuncAdd);
                SetBlend(0f, 1f);
                _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)_count);

                if (needDest)
                {
                    _vram.BeginDestRead(destTex, destW, destH, readX, readY, readW, readH);
                    RebindTarget(rt);
                }

                _gl.BlendEquationSeparate(BlendEquationModeEXT.FuncReverseSubtract, BlendEquationModeEXT.FuncAdd);
                SetBlend(1f, 1f);
                _gl.Uniform4(_uBlendOpaque, 0f, 0f, 0f, 1f);
                _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)_count);
            }
            else
            {
                _gl.BlendEquation(BlendEquationModeEXT.FuncAdd);
                SetBlend(_kBlend switch { 0 => 0.5f, 3 => 0.25f, _ => 1f }, _kBlend == 0 ? 0.5f : 1f);
                _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)_count);
            }
        }

        _gl.Disable(EnableCap.ScissorTest);
        if (rt != null)
        {
            rt.Dirty = true;
            rt.LastDrawFrame = _frame;
        }
        else
        {
            var x0 = Math.Max(_kClipX0, (int)Math.Floor(_drawMinX));
            var y0 = Math.Max(_kClipY0, (int)Math.Floor(_drawMinY));
            var x1 = Math.Min(_kClipX1, (int)Math.Ceiling(_drawMaxX));
            var y1 = Math.Min(_kClipY1, (int)Math.Ceiling(_drawMaxY));
            if (x1 >= x0 && y1 >= y0)
            {
                Assets.Textures.VramTracker.MarkGpuWrite(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
                if (!_vramDirty)
                {
                    _vramDirty = true;
                    _vdX0 = x0;
                    _vdY0 = y0;
                    _vdX1 = x1 + 1;
                    _vdY1 = y1 + 1;
                }
                else
                {
                    if (x0 < _vdX0) _vdX0 = x0;
                    if (y0 < _vdY0) _vdY0 = y0;
                    if (x1 + 1 > _vdX1) _vdX1 = x1 + 1;
                    if (y1 + 1 > _vdY1) _vdY1 = y1 + 1;
                }
            }
        }

        TraceFlush(rt);
        _count = 0;
        _kSamplesVram = false;
    }

    private void SetBlend(float src, float dst)
    {
        _gl.Uniform4(_uBlend, src, src, src, dst);
    }

    private void SetScaleUniform(uint prog)
    {
        var loc = _gl.GetUniformLocation(prog, "uScale");
        if (loc < 0) return;
        if (_legacy) _gl.Uniform1(loc, (float)GlVram.Scale);
        else _gl.Uniform1(loc, GlVram.Scale);
    }

    private void RebindTarget(GlDisplayRt? rt)
    {
        if (rt == null)
        {
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _vram.Fbo);
            _gl.Viewport(0, 0, (uint)GlVram.Width, (uint)GlVram.Height);
        }
        else
        {
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, rt.Fbo);
            _gl.Viewport(0, 0, (uint)rt.TexW, (uint)rt.TexH);
        }
    }

    public void Present(in HleDispEnv disp)
    {
        PresentDisplay(disp.X, disp.Y, disp.W, disp.H, disp.Rgb24);
    }

    public unsafe (uint tex, int w, int h, float aspect) PresentDisplay(int dispX, int dispY, int w, int h,
        bool rgb24 = false, int outW = 0, int outH = 0)
    {
        if (!Ready || w <= 0 || h <= 0) return (0, 0, 0, GpuHle.OutputAspect);

        Flush();

        for (var i = 0; i < _rts.Length; i++)
        {
            if (_rts[i] is not { } rt) continue;
            if (rt.Dirty) Writeback(rt);
            // A displayed framebuffer remains valid even when a slow frame or
            // static pause screen produces no geometry. VRAM writes synchronize
            // these targets; elapsed host frames are not an invalidation signal.
            bool displayed = dispX >= rt.X && dispY >= rt.Y &&
                dispX + w <= rt.X + rt.W && dispY + h <= rt.Y + rt.H;
            if (!displayed && _frame - rt.LastDrawFrame > 300)
            {
                rt.Destroy(_gl);
                _rts[i] = null;
                InvalidateClassify();
            }
        }

        GlDisplayRt? src = null;
        if (!rgb24)
            foreach (var rt in _rts)
            {
                if (rt == null) continue;
                if (dispX < rt.X || dispY < rt.Y || dispX + w > rt.X + rt.W || dispY + h > rt.Y + rt.H) continue;
                if (src == null || rt.LastDrawFrame > src.LastDrawFrame) src = rt;
            }

        // Pause/front-end screens may reuse the last race framebuffer without redrawing it.
        // Crop its centre rather than squeezing old widescreen side pixels into 4:3.
        var presentMargin = GpuHle.WideAspect > 0f && src != null ? src.Margin : 0;
        var w1x = w + presentMargin * 2;
        // The allocation is integer-rounded, but the sampled field of view is exact.
        // This avoids the ~0.31% squeeze of presenting all 428 columns as 16:9.
        var sampleWidth = presentMargin > 0 ? w * GpuHle.WideAspect / GpuHle.SourceAspect : w;
        var sampleInset = (w1x - sampleWidth) * 0.5f;
        var h1x = h;
        var aspect = presentMargin > 0 ? GpuHle.WideAspect :
            src != null ? GpuHle.SourceAspect : GpuHle.OutputAspect;


        GpuHle.LastDisplayW = w;
        GpuHle.LastDisplayH = h;

        var presentScale = GlVram.Scale;
        var fbW = w1x * presentScale;
        var fbH = h1x * presentScale;
        EnsurePresentSize(fbW, fbH, GlVram.Scale == 1);

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _presentFbo);
        _gl.Viewport(0, 0, (uint)fbW, (uint)fbH);
        _gl.Disable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.Blend);
        _gl.Disable(EnableCap.ScissorTest);
        _gl.Disable(EnableCap.CullFace);

        _gl.UseProgram(rgb24 ? _progPresent24 : _progPresent);
        _gl.BindVertexArray(_presentVao);
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, src?.Tex ?? _vram.Texture);
        if (rgb24)
        {
            _gl.Uniform2(_uPresent24Origin, (float)dispX, dispY);
            _gl.Uniform2(_uPresent24Size, (float)w, h);
        }
        else if (src != null)
        {
            _gl.Uniform2(_uPresentOrigin, (float)(dispX - src.X + src.Margin - presentMargin + sampleInset), dispY - src.Y);
            _gl.Uniform2(_uPresentSize, sampleWidth, (float)h1x);
            _gl.Uniform2(_uPresentTexSize, (float)src.Wide1x, src.H);
        }
        else
        {
            _gl.Uniform2(_uPresentOrigin, (float)dispX, dispY);
            _gl.Uniform2(_uPresentSize, (float)w, h);
            _gl.Uniform2(_uPresentTexSize, (float)VramShadow.Width, VramShadow.Height);
        }

        _gl.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);

        var outTex = ApplyPostFx(_presentTex, fbW, fbH);
        CaptureFrame(fbW, fbH, aspect,src);

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        return (outTex, fbW, fbH, aspect);
    }

    private unsafe void CaptureFrame(int width, int height, float aspect,GlDisplayRt? source)
    {
        if (string.IsNullOrWhiteSpace(_captureDirectory)) return;
        long frame = Interrupts.VBlankCount;
        long raceFrame=frame;
        if(_captureRaceRelative)
        {
            if(!long.TryParse(Environment.GetEnvironmentVariable("JETMOTO_REPLAY_RACE_START_VBLANK"),out long raceStart)||frame<raceStart)return;
            if(_captureRaceStart!=raceStart){_captureRaceStart=raceStart;_lastCapture=-1;_effectCaptures=0;_effectCaptureStarted=false;}
            raceFrame=frame-raceStart;
        }
        if (long.TryParse(Environment.GetEnvironmentVariable("JETMOTO_CAPTURE_START"), out long start) && raceFrame < start) return;
        if (long.TryParse(Environment.GetEnvironmentVariable("JETMOTO_CAPTURE_END"), out long end) && raceFrame > end) return;
        bool recentEffectDraw = _effectDrawVblank >= 0 && frame - _effectDrawVblank <= 10;
        if (_effectCaptureFrames > 0)
        {
            if (_effectCaptures >= _effectCaptureFrames || _lastEffectCaptureFrame == _frame) return;
            if (!_effectCaptureStarted && !recentEffectDraw) return;
            _effectCaptureStarted = true;
            _lastEffectCaptureFrame = _frame;
            _effectCaptures++;
        }
        int interval = int.TryParse(Environment.GetEnvironmentVariable("JETMOTO_CAPTURE_EVERY"), out int n)
            ? Math.Clamp(n, 1, 3600) : 120;
        if (raceFrame <= 0 || (_effectCaptureFrames == 0 && raceFrame / interval <= _lastCapture / interval)) return;
        _lastCapture = raceFrame;
        byte[] pixels = new byte[checked(width * height * 4)];
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _presentFbo);
        fixed (byte* p = pixels)
            _gl.ReadPixels(0, 0, (uint)width, (uint)height, PixelFormat.Rgba, PixelType.UnsignedByte, p);
        string name=_captureRaceRelative ? $"race-{raceFrame:000000}" : $"frame-{frame:000000}";
        string path = Path.Combine(_captureDirectory, name+(_captureRaw ? ".rgba" : ".png"));
        // Raw RGBA recording avoids compression work on the rendering thread.
        if(_captureRaw) File.WriteAllBytes(path,pixels);
        else Assets.PngWriter.WriteRgba(path, pixels, width, height);
        if(_traceGeometry)
        {
            long sourceFrame=source?.LastDrawFrame??_frame;
            GeometryTrace? geometry=null;
            if(source!=null&&_geometryTargets.TryGetValue(source,out var recorded)&&recorded.Frame==sourceFrame)
                geometry=recorded;
            File.WriteAllText(Path.ChangeExtension(path,".geometry.json"),System.Text.Json.JsonSerializer.Serialize(new {
                sourceFrame,captureFrame=_frame,available=geometry?.Triangles.Count>0,
                truncated=geometry?.Truncated??false,width,height,aspect,
                framebuffer=source is null ? null : new[]{source.X,source.Y,source.W,source.H,source.Margin},
                vertexFields=new[]{"x","y","z","hasGteZ","u","v"},
                triangles=geometry?.Triangles,
                note="Drawn batches for the captured target since its last buffer switch/full clear, not final pixel ownership; texture masks and shader discards are not evaluated."
            },new System.Text.Json.JsonSerializerOptions{NumberHandling=System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals}));
        }
        File.WriteAllText(Path.ChangeExtension(path, ".json"), System.Text.Json.JsonSerializer.Serialize(new
        { vblank = frame, raceFrame=_captureRaceRelative ? raceFrame : (long?)null, renderFrame = _frame, lastEffectDrawVblank = _effectDrawVblank,
          lastEffectDrawTriangles = _effectFrameTriangles, width, height, aspect, lighting = Assets.Native.WorldSurfaceBindings.Summary,
          emission = _worldWakeEnabled && _motionCamera is {} motion ? new {
              scene=motion.Scene.Name,time=motion.Time,waterLevel=motion.Scene.FlatWaterLevel,
              wakeRasterSegments=_wakeRasterSegments,sprayProjection=DescribeSprayProbe(),
              riders=motion.Riders.OrderBy(r=>r.Key).Select(r=> {
                  var birth=(r.Value.Min+r.Value.Max)*.5f;
                  return new { id=r.Key,position=new[]{birth.X,birth.Y,birth.Z},
                      groundVetoPassed=motion.Scene.AllowsWaterEmission(birth),
                      segments=_motionSegments.Count(s=>s.RiderId==r.Key),
                      particles=_sprayParticles.Count(p=>p.RiderId==r.Key),
                      injectionVisited=_sprayActors.Contains(r.Key) };
              }),note="Emission state only; neither positive water contact nor visible-pixel proof."
          } : null }));
    }

    //support for post-fx shaders to be loaded, so you can have cool shaders (this was too anonying to implement)
    private unsafe uint ApplyPostFx(uint srcTex, int w, int h)
    {
        if (!PostFx.Active) return srcTex;
        if (!EnsurePostProgram()) return srcTex;

        if (_postTex == 0)
        {
            _postTex = _gl.GenTexture();
            _gl.BindTexture(TextureTarget.Texture2D, _postTex);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
            _postFbo = _gl.GenFramebuffer();
        }

        if (w != _postW || h != _postH)
        {
            _gl.BindTexture(TextureTarget.Texture2D, _postTex);
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)w, (uint)h, 0,
                PixelFormat.Rgba, PixelType.UnsignedByte, null);
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _postFbo);
            _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                TextureTarget.Texture2D, _postTex, 0);
            _postW = w;
            _postH = h;
        }

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _postFbo);
        _gl.Viewport(0, 0, (uint)w, (uint)h);
        _gl.Disable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.Blend);
        _gl.Disable(EnableCap.ScissorTest);

        _gl.UseProgram(_postProg);
        _gl.BindVertexArray(_presentVao);
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, srcTex);
        if (_uPostTexSize >= 0) _gl.Uniform2(_uPostTexSize, (float)w, h);
        if (_uPostOutputSize >= 0) _gl.Uniform2(_uPostOutputSize, (float)w, h);
        if (_uPostTime >= 0) _gl.Uniform1(_uPostTime, (float)_postClock.Elapsed.TotalSeconds);
        if (_uPostFrame >= 0) _gl.Uniform1(_uPostFrame, _postFrame++);
        ApplyPostParams();
        _gl.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);

        return _postTex;
    }

    private void ApplyPostParams()
    {
        var version = PostFx.ParamVersion;
        if (version != _postParamVersion)
        {
            _postParamVersion = version;
            _postParams = PostFx.SnapshotParams();
            _postParamLoc = new int[_postParams.Length];
            for (var i = 0; i < _postParams.Length; i++)
                _postParamLoc[i] = _gl.GetUniformLocation(_postProg, _postParams[i].Name);
        }

        for (var i = 0; i < _postParams.Length; i++)
            if (_postParamLoc[i] >= 0)
                _gl.Uniform1(_postParamLoc[i], _postParams[i].Value);
    }

    private bool EnsurePostProgram()
    {
        var version = PostFx.Version;
        if (version == _postVersion) return _postProg != 0;
        _postVersion = version;

        if (_postProg != 0)
        {
            _gl.DeleteProgram(_postProg);
            _postProg = 0;
        }

        var src = PostFx.Source;
        if (src == null) return false;

        _postProg = GlShaders.Build(_gl, GlShaders.FullscreenVs, src, "postfx", out var error);
        if (_postProg == 0)
        {
            PostFx.Error = error ?? "shader fails to build";
            Console.WriteLine($"[gpu-pfx] {PostFx.Error}");
            return false;
        }

        PostFx.Error = null;
        _gl.UseProgram(_postProg);
        _gl.Uniform1(_gl.GetUniformLocation(_postProg, "uTex"), 0);
        _uPostTexSize = _gl.GetUniformLocation(_postProg, "uTexSize");
        _uPostOutputSize = _gl.GetUniformLocation(_postProg, "uOutputSize");
        _uPostTime = _gl.GetUniformLocation(_postProg, "uTime");
        _uPostFrame = _gl.GetUniformLocation(_postProg, "uFrame");
        _postParamVersion = -1;
        return true;
    }

    private unsafe void EnsurePresentSize(int w, int h, bool nearest)
    {
        if (w == _presentW && h == _presentH && nearest == _presentNearest) return;
        _gl.BindTexture(TextureTarget.Texture2D, _presentTex);
        _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)w, (uint)h, 0, PixelFormat.Rgba,
            PixelType.UnsignedByte, null);
        var filter = nearest ? GLEnum.Nearest : GLEnum.Linear;
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)filter);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)filter);
        _presentW = w;
        _presentH = h;
        _presentNearest = nearest;
    }

    public void Dispose()
    {
        _nativeTextures.Clear(); _nativeImages.Clear();
        foreach (uint texture in _images) if (texture != 0) _gl.DeleteTexture(texture);
        _images.Clear();
        foreach (uint texture in _repTextures.Values) _gl.DeleteTexture(texture);
        _repTextures.Clear();
        foreach (uint texture in _repCluts.Values) _gl.DeleteTexture(texture);
        _repCluts.Clear();
        if (_waterWake != 0) { _gl.DeleteTexture(_waterWake); _waterWake = 0; }
        foreach(uint texture in _worldMaps.Values)_gl.DeleteTexture(texture);_worldMaps.Clear();
        if (_waterDetail != 0) { _gl.DeleteTexture(_waterDetail); _waterDetail = 0; }
        if (_riderShadow != 0) { _gl.DeleteTexture(_riderShadow); _riderShadow = 0; _shadowCamera = null; }
        foreach (var rt in _rts) rt?.Destroy(_gl);
        _vram.Dispose();
        if (_vbo != 0) _gl.DeleteBuffer(_vbo);
        if (_presentVbo != 0) _gl.DeleteBuffer(_presentVbo);
        if (_vao != 0) _gl.DeleteVertexArray(_vao);
        if (_presentVao != 0) _gl.DeleteVertexArray(_presentVao);
        if (_progPrim != 0) _gl.DeleteProgram(_progPrim);
        if (_progPresent != 0) _gl.DeleteProgram(_progPresent);
        if (_progPresent24 != 0) _gl.DeleteProgram(_progPresent24);
        if (_presentTex != 0) _gl.DeleteTexture(_presentTex);
        if (_presentFbo != 0) _gl.DeleteFramebuffer(_presentFbo);
        if (_postProg != 0) _gl.DeleteProgram(_postProg);
        if (_postTex != 0) _gl.DeleteTexture(_postTex);
        if (_postFbo != 0) _gl.DeleteFramebuffer(_postFbo);
    }
}
