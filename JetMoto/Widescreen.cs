using System.Threading;
using RecompOne.Runtime.Hle;
using RecompOne.Runtime.Memory;

namespace JetMoto;

/// <summary>
/// SCUS-94309-specific Hor+ policy. Never changes the GTE projection, VRAM layout,
/// physics, HUD coordinates, near/far clipping, backface tests or texture UVs.
/// Hooks are anchored to the verified generated instruction addresses, not timers,
/// display resolution guesses or "any 3D means gameplay" heuristics.
/// </summary>
public static class Widescreen
{
    internal static Action? RaceEntered;
    public const float GameplayAspect = 16f / 9f;
    public const float MenuAspect = 4f / 3f;
    public const uint ProfileTable = 0x8016E834;
    public const uint CameraPointers = 0x801CBB00;
    public const uint CameraProfiles = 0x801CBB10;
    public const int ProfileCount = 10;
    public const int SceneCapacity = 1024;
    private static int _raceDepth, _menuDepth, _maxVisible, _saturatedLists;
    private static long _cameraUpdates;
    public static bool Active => _raceDepth > 0 && _menuDepth == 0;
    // Pausing freezes gameplay while preserving the retained framebuffer aspect.
    private static bool RacePresentation => _raceDepth > 0;
    public static string Diagnostics => $"view={(RacePresentation ? "16:9" : "4:3")} cameraUpdates={Interlocked.Read(ref _cameraUpdates)} visibleMax={Volatile.Read(ref _maxVisible)} visibleLimitHits={Volatile.Read(ref _saturatedLists)}";

    public static IDisposable EnterRace()
    {
        RaceEntered?.Invoke();
        _raceDepth++;
        SetView();
        return new RaceScope();
    }

    private sealed class RaceScope : IDisposable
    {
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _raceDepth--;
            SetView();
        }
    }

    // The pause-check function is called every race frame. Only its actual
    // pause branch activates this scope; ordinary frames do not switch aspect.
    public sealed class DeferredMenuScope : IDisposable
    {
        private bool _active;
        public void Activate()
        {
            if (_active) return;
            _active = true;
            _menuDepth++;
            SetView();
        }
        public void Dispose()
        {
            if (!_active) return;
            _active = false;
            _menuDepth--;
            SetView();
        }
    }

    private static void SetView()
    {
        GpuHle.RetainDisplayMargins = RacePresentation && !Active;
        GpuHle.SourceAspect = MenuAspect;
        GpuHle.OutputAspect = MenuAspect;
        GpuHle.TargetAspect = RacePresentation ? GameplayAspect : MenuAspect;
        float aspect = RacePresentation ? GameplayAspect : 0;
        if (GpuHle.WideAspect == aspect) return;
        GpuHle.WideAspect = aspect;
        Console.WriteLine("[JetMoto:widescreen] " + Diagnostics);
    }

    /// <summary>
    /// Before 800F96CC transforms the camera-local planes into object space,
    /// derive both side planes afresh from the selected immutable profile.
    /// Restoring from the profile also prevents cumulative widening and restores
    /// menu previews after a race. Plane length is preserved for sphere tests.
    /// </summary>
    public static void PrepareCamera(PSMemory mem, uint camera)
    {
        if (camera > 1) throw new InvalidOperationException($"Unknown camera slot {camera}.");
        uint context = mem.ReadU32(CameraPointers + 4 * camera);
        uint profile = mem.ReadU32(CameraProfiles + 4 * camera);
        if (context == 0) return;
        if (profile >= ProfileCount || (context & 0x1FFFFFFF) >= 0x200000)
            throw new InvalidOperationException($"Unexpected camera context 0x{context:X8}, profile {profile}.");
        uint source = ProfileTable + 0x60 * profile;
        int width = (int)mem.ReadU32(source + 0x10);
        if (width is not (320 or 640))
            throw new InvalidOperationException($"Unexpected projection width {width} in profile {profile}.");
        double factor = Active ? (width + 2.0 * (GpuHle.WideMargin(width) + 4)) / width : 1;
        for (uint side = 0; side < 2; side++)
        {
            uint plane = source + 0x30 + side * 8;
            var n = ExpandPlane((short)mem.ReadU16(plane), (short)mem.ReadU16(plane + 2),
                (short)mem.ReadU16(plane + 4), factor);
            // Game stores three frustum plane normals as the columns of a MATRIX.
            mem.WriteU16(context + 0x40 + side * 2, unchecked((ushort)n.x));
            mem.WriteU16(context + 0x46 + side * 2, unchecked((ushort)n.y));
            mem.WriteU16(context + 0x4C + side * 2, unchecked((ushort)n.z));
        }
        Interlocked.Increment(ref _cameraUpdates);
    }

    public static (short x, short y, short z) ExpandPlane(short x, short y, short z, double factor)
    {
        if (!(factor >= 1) || !double.IsFinite(factor)) throw new ArgumentOutOfRangeException(nameof(factor));
        if (factor == 1) return (x, y, z);
        double length = Math.Sqrt((double)x * x + (double)y * y + (double)z * z);
        double nx = x / factor;
        double norm = Math.Sqrt(nx * nx + (double)y * y + (double)z * z);
        if (norm == 0) return (x, y, z);
        double scale = length / norm;
        return (checked((short)Math.Round(nx * scale)), checked((short)Math.Round(y * scale)),
            checked((short)Math.Round(z * scale)));
    }

    public static void ObserveVisibleList(uint count)
    {
        if (!Active) return;
        if (count > _maxVisible) _maxVisible = (int)count;
        // Build08 relocates the CPU scene list into a separate bounded host arena.
        // The original 280-entry allocation is NOT enlarged in place.
        if (count >= SceneCapacity)
        {
            int n = Interlocked.Increment(ref _saturatedLists);
            if (n == 1) Console.WriteLine("[JetMoto:widescreen] WARNING: scene-list capacity reached; retain this log.");
        }
    }
}
