using System.Runtime.InteropServices;
using RecompOne.Runtime.Hle;

namespace RecompOne.Runtime.Interp;

public sealed class InterpBackend : IGpuBackend
{
    private readonly IGpuBackend _inner;
    private readonly InterpClock _clock = new();
    private readonly System.Diagnostics.Stopwatch _watch = System.Diagnostics.Stopwatch.StartNew();
    
    private double _frameStartMs;
    private double _budgetMs;
    private double _renderedMs;
    private int _rendered;
    private int _source;
    private double _lastBeginMs;
    private double _sourceMs;
    private bool _resync;
    private readonly TransformInterp _transforms = new();
    
    private readonly Lock _gate = new();
    private readonly Stack<FrameGraph> _free = new();
    
    private FrameGraph _recording = new();
    private readonly Queue<FrameGraph> _ready = new();
    private FrameGraph _current = new();
    private FrameGraph _previous = new();
    
    private readonly Dictionary<int, int> _groups = new();
    
    private bool _active;
    private bool _interpolating;
    private int _frames = 1;
    
    public InterpBackend(IGpuBackend inner)
    {
        _inner = inner;
        _active = inner.Ready;
        Interp.Backend = this;
    }
    
    public bool Ready => _inner.Ready;
    
    public int SourceRate => _source;
    
    public void SetDrawEnv(in HleDrawEnv env)
    {
        if (!_active)
        {
            _inner.SetDrawEnv(env);
            return;
        }
        _recording.Envs.Add(env);
        _recording.Add(GraphOp.DrawEnv, _recording.Envs.Count - 1);
    }
    
    public void DrawTri(in HleVertex a, in HleVertex b, in HleVertex c, in PrimFlags f)
    {
        if (!_active)
        {
            _inner.DrawTri(in a, in b, in c, in f);
            return;
        }
        
        var offsetX = (float)(Runtime.Gpu?.DrawOffsetX ?? 0);
        var offsetY = (float)(Runtime.Gpu?.DrawOffsetY ?? 0);
        
        var slot = Group(in a, in b, in c);
        Mix(slot, in f, in a, in b, in c);
        
        _recording.Tris.Add(new TriRecord
        {
            Transform = slot,
            A = Detach(in a, offsetX, offsetY),
            B = Detach(in b, offsetX, offsetY),
            C = Detach(in c, offsetX, offsetY),
            Flags = f,
            OffsetX = offsetX,
            OffsetY = offsetY
        });
        
        _recording.Add(GraphOp.Tri, _recording.Tris.Count - 1);
    }
    
    private void Mix(int slot, in PrimFlags f, in HleVertex a, in HleVertex b, in HleVertex c)
    {
        if (slot <= 0) return;
        
        var held = _recording.Transforms[slot - 1];
        var key = held.Key == 0u ? 2166136261u : held.Key;
        key = (key ^ f.TPage) * 16777619u;
        key = (key ^ f.Clut) * 16777619u;
        key = (key ^ (f.Textured ? 1u : 0u)) * 16777619u;
        held.Key = key;
        held.Pages |= 1u << (int)(f.TPage & 0x1Fu);
        
        if (f.Textured)
        {
            held.U0 = MathF.Min(held.U0, MathF.Min(a.U, MathF.Min(b.U, c.U)));
            held.U1 = MathF.Max(held.U1, MathF.Max(a.U, MathF.Max(b.U, c.U)));
            held.V0 = MathF.Min(held.V0, MathF.Min(a.V, MathF.Min(b.V, c.V)));
            held.V1 = MathF.Max(held.V1, MathF.Max(a.V, MathF.Max(b.V, c.V)));
        }
        
        _recording.Transforms[slot - 1] = held;
    }
    
    private int Group(in HleVertex a, in HleVertex b, in HleVertex c)
    {
        var serial = a.Transform > 0 ? a.Transform : b.Transform > 0 ? b.Transform : c.Transform;
        if (serial <= 0) return 0;
        
        if (_groups.TryGetValue(serial, out var index))
        {
            var held = _recording.Transforms[index];
            held.Tris++;
            _recording.Transforms[index] = held;
            return index + 1;
        }
        
        Span<short> rotation = stackalloc short[9];
        Span<int> translation = stackalloc int[3];
        Span<int> view = stackalloc int[3];
        if (!Gte.Snapshot(serial, rotation, translation, view)) return 0;
        
        index = _recording.Transforms.Count;
        _recording.Transforms.Add(new TransformRecord
        {
            Serial = serial,
            R0 = rotation[0], R1 = rotation[1], R2 = rotation[2],
            R3 = rotation[3], R4 = rotation[4], R5 = rotation[5],
            R6 = rotation[6], R7 = rotation[7], R8 = rotation[8],
            TX = translation[0], TY = translation[1], TZ = translation[2],
            H = view[0], OFX = view[1], OFY = view[2],
            Tris = 1,
            Match = -1,
            U0 = float.MaxValue, U1 = float.MinValue,
            V0 = float.MaxValue, V1 = float.MinValue
        });
        
        _groups[serial] = index;
        return index + 1;
    }
    
    public void DrawRect(in HleRect r, in PrimFlags f)
    {
        if (!_active)
        {
            _inner.DrawRect(in r, in f);
            return;
        }
        _recording.Rects.Add(new RectRecord { Rect = r, Flags = f });
        _recording.Add(GraphOp.Rect, _recording.Rects.Count - 1);
    }
    
    public void DrawLine(in HleVertex a, in HleVertex b, in PrimFlags f)
    {
        if (!_active)
        {
            _inner.DrawLine(in a, in b, in f);
            return;
        }
        
        _recording.Lines.Add(new LineRecord { A = a, B = b, Flags = f });
        _recording.Add(GraphOp.Line, _recording.Lines.Count - 1);
    }
    
    public void FillRect(int x, int y, int w, int h, ushort color15)
    {
        if (!_active)
        {
            _inner.FillRect(x, y, w, h, color15);
            return;
        }
        _recording.Fills.Add(new FillRecord { X = x, Y = y, W = w, H = h, Color = color15 });
        _recording.Add(GraphOp.Fill, _recording.Fills.Count - 1);
    }
    
    public void CopyVram(int sx, int sy, int dx, int dy, int w, int h)
    {
        if (!_active)
        {
            _inner.CopyVram(sx, sy, dx, dy, w, h);
            return;
        }
        _recording.Copies.Add(new CopyRecord { Sx = sx, Sy = sy, Dx = dx, Dy = dy, W = w, H = h });
        _recording.Add(GraphOp.CopyVram, _recording.Copies.Count - 1);
    }
    
    public void WriteVram(int x, int y, int w, int h, ReadOnlySpan<ushort> px)
    {
        if (!_active)
        {
            _inner.WriteVram(x, y, w, h, px);
            return;
        }
        var offset = _recording.AddPixels(px);
        _recording.Writes.Add(new WriteRecord { X = x, Y = y, W = w, H = h, Offset = offset, Length = px.Length });
        _recording.Add(GraphOp.WriteVram, _recording.Writes.Count - 1);
    }
    
    public void ReadVram(int x, int y, int w, int h, Span<ushort> px)
    {
        Settle();
        
        _inner.ReadVram(x, y, w, h, px);
    }
    
    public int RegisterImage(ReadOnlySpan<byte> rgba, int width, int height)
    {
        return _inner.RegisterImage(rgba, width, height);
    }

    public bool PrepareNativeTexture(Assets.Native.NativeTextureAsset asset) => _inner.PrepareNativeTexture(asset);
    public int RegisterNativeImage(Assets.Native.NativeTextureAsset asset) => _inner.RegisterNativeImage(asset);
    
    public void Flush()
    {
        Settle();
        _inner.Flush();
    }
    
    public void Present(in HleDispEnv disp)
    {
        _inner.Present(in disp);
    }
    
    private const double StallMs = 100.0;
    
    public double PaceMs { get; private set; }
    
    public void Publish()
    {
        if (!_active) return;
        
        lock (_gate)
        {
            _ready.Enqueue(_recording);
            _recording = _free.Count > 0 ? _free.Pop() : new FrameGraph();
            _recording.Clear();
            _groups.Clear();
        }
    }
    
    public bool Acquire()
    {
        if (!_active) return false;
        
        lock (_gate)
        {
            if (_ready.Count == 0) return false;
            
            while (_ready.Count > 1)
            {
                _resync = true;
                
                var skipped = _ready.Dequeue();
                Replay(skipped, null, 1f);
                
                Recycle(_previous);
                _previous = _current;
                _current = skipped;
            }
            
            Recycle(_previous);
            _previous = _current;
            _current = _ready.Dequeue();
        }
        
        return true;
    }
    
    private void Recycle(FrameGraph graph)
    {
        graph.Clear();
        _free.Push(graph);
    }
    
    public int BeginPresent()
    {
        if (!_active) return 1;
        
        _source = VideoRate.Rate;
        
        var now = _watch.Elapsed.TotalMilliseconds;
        var gap = _lastBeginMs > 0.0 ? now - _lastBeginMs : 0.0;
        _lastBeginMs = now;
        
        if (gap > StallMs) _resync = true;
        else if (gap > 0.0) _sourceMs = _sourceMs > 0.0 ? _sourceMs * 0.75 + gap * 0.25 : gap;
        
        if (_resync)
        {
            _resync = false;
            _clock.Reset();
            _interpolating = false;
            _budgetMs = 0.0;
            PaceMs = 0.0;
            _frameStartMs = now;
            _rendered = 0;
            _renderedMs = 0.0;
            return 1;
        }
        
        var frames = _clock.Advance(_source, Interp.EffectiveTarget);
        
        _budgetMs = _sourceMs > 0.0 ? _sourceMs : _source > 0 ? 1000.0 / _source : 0.0;
        
        var target = Interp.EffectiveTarget;
        PaceMs = frames > 1 && target > 0 ? 1000.0 / target : 0.0;
        _frameStartMs = _watch.Elapsed.TotalMilliseconds;
        _rendered = 0;
        _renderedMs = 0.0;
        
        lock (_gate)
        {
            _interpolating = frames > 1 && _current.Interpolatable && _previous.Interpolatable &&
                             !_current.IsEmpty && _previous.Tris.Count > 0;
            
            if (!_interpolating) return 1;
            
            _frames = frames;
            Hle.GpuHle.Hold();
            Prepare();
            
        }
        
        return frames;
    }
    
    private void Prepare()
    {
        _transforms.Match(_current, _previous);
    }
    
    public void Compose(int index)
    {
        if (!_active) return;
        
        var weight = _interpolating && index < _frames ? _clock.Weights(_frames)[index] : 1f;
        var start = _watch.Elapsed.TotalMilliseconds;
        
        lock (_gate)
        {
            if (_interpolating && weight < 1f) _transforms.Build(_current, _previous, weight);
            
            Replay(_current, _interpolating ? _previous : null, weight);
        }
        
        _renderedMs += _watch.Elapsed.TotalMilliseconds - start;
        _rendered++;
    }
    
    public bool Affordable(int index)
    {
        if (!_active || !_interpolating || index == 0 || _budgetMs <= 0.0) return true;
        if (_rendered == 0) return true;
        
        var elapsed = _watch.Elapsed.TotalMilliseconds - _frameStartMs;
        return elapsed + _renderedMs / _rendered <= _budgetMs;
    }
    
    public void EndPresent()
    {
        Hle.GpuHle.Release();
        Begin();
    }
    
    private void Begin()
    {
        var wanted = _inner.Ready;
        if (wanted == _active) return;
        
        _active = wanted;
        _clock.Reset();
        while (_ready.Count > 0) Recycle(_ready.Dequeue());
        _recording.Clear();
        _current.Clear();
        _previous.Clear();
    }
    
    private void Settle()
    {
        if (!_active) return;
        
        lock (_gate)
        {
            if (_current.IsEmpty) return;
            
            Replay(_current, null, 1f);
            _current.Clear();
            _current.Interpolatable = false;
        }
    }
    
    private void Replay(FrameGraph graph, FrameGraph? previous, float weight)
    {
        var pixels = CollectionsMarshal.AsSpan(graph.Pixels);
        var ops = CollectionsMarshal.AsSpan(graph.Ops);
        var slots = CollectionsMarshal.AsSpan(graph.Slots);
        var envs = CollectionsMarshal.AsSpan(graph.Envs);
        var rects = CollectionsMarshal.AsSpan(graph.Rects);
        var fills = CollectionsMarshal.AsSpan(graph.Fills);
        var copies = CollectionsMarshal.AsSpan(graph.Copies);
        var writes = CollectionsMarshal.AsSpan(graph.Writes);
        
        for (var i = 0; i < ops.Length; i++)
        {
            var slot = slots[i];
            
            switch (ops[i])
            {
                case GraphOp.DrawEnv:
                    _inner.SetDrawEnv(envs[slot]);
                    break;
                
                case GraphOp.Tri:
                    ReplayTri(graph, previous, slot, weight);
                    break;
                case GraphOp.Rect:
                {
                    ref var rect = ref rects[slot];
                    _inner.DrawRect(in rect.Rect, in rect.Flags);
                    break;
                }
                case GraphOp.Line:
                {
                    var line = graph.Lines[slot];
                    _inner.DrawLine(in line.A, in line.B, in line.Flags);
                    break;
                }
                case GraphOp.Fill:
                {
                    ref var fill = ref fills[slot];
                    _inner.FillRect(fill.X, fill.Y, fill.W, fill.H, fill.Color);
                    break;
                }
                case GraphOp.CopyVram:
                {
                    ref var copy = ref copies[slot];
                    _inner.CopyVram(copy.Sx, copy.Sy, copy.Dx, copy.Dy, copy.W, copy.H);
                    break;
                }
                case GraphOp.WriteVram:
                {
                    ref var write = ref writes[slot];
                    _inner.WriteVram(write.X, write.Y, write.W, write.H, pixels.Slice(write.Offset, write.Length));
                    break;
                }
            }
        }
    }
    
    private void ReplayTri(FrameGraph graph, FrameGraph? previous, int slot, float weight)
    {
        var tri = graph.Tris[slot];
        
        if (previous == null || weight >= 1f)
        {
            Emit(in tri, in tri.A, in tri.B, in tri.C);
            return;
        }
        
        if (tri.Transform > 0 &&  _transforms.Warp(tri.Transform, in tri.A, in tri.B, in tri.C, out var wa, out var wb, out var wc))
        {
            Emit(in tri, in wa, in wb, in wc);
            return;
        }
        
        Emit(in tri, in tri.A, in tri.B, in tri.C);
    }
    
    private void Emit(in TriRecord tri, in HleVertex a, in HleVertex b, in HleVertex c)
    {
        var va = Attach(in a, tri.OffsetX, tri.OffsetY);
        var vb = Attach(in b, tri.OffsetX, tri.OffsetY);
        var vc = Attach(in c, tri.OffsetX, tri.OffsetY);
        
        _inner.DrawTri(in va, in vb, in vc, in tri.Flags); //send interpolated tri to backend
    }
    
    private static HleVertex Detach(in HleVertex v, float offsetX, float offsetY)
    {
        var d = v;
        d.X -= offsetX;
        d.Y -= offsetY;
        return d;
    }
    
    private static HleVertex Attach(in HleVertex v, float offsetX, float offsetY)
    {
        var a = v;
        a.X += offsetX;
        a.Y += offsetY;
        return a;
    }
    
    private static bool HasDepth(in TriRecord from, in TriRecord to)
    {
        return from.A.HasGteZ && from.B.HasGteZ && from.C.HasGteZ && to.A.HasGteZ && to.B.HasGteZ && to.C.HasGteZ;
    }
}
