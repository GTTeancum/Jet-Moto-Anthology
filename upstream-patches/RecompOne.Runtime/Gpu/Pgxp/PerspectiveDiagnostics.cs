using System.Threading;

namespace RecompOne.Runtime.Pgxp;

/// <summary>Counts submitted polygons, not frames or a guarantee of visual correctness.</summary>
public static class PerspectiveDiagnostics
{
    private static long _textured, _corrected, _flat, _partial, _vertices, _tracked;
    public static string Summary => $"perspective={Interlocked.Read(ref _corrected)}/{Interlocked.Read(ref _textured)} flat2DOrUntracked={Interlocked.Read(ref _flat)} partialDepth={Interlocked.Read(ref _partial)} trackedVertices={Interlocked.Read(ref _tracked)}/{Interlocked.Read(ref _vertices)}";
    public static void Record(bool textured, int count, int tracked, bool corrected)
    {
        if (!textured) return;
        Interlocked.Increment(ref _textured);
        Interlocked.Add(ref _vertices, count);
        Interlocked.Add(ref _tracked, tracked);
        if (corrected) Interlocked.Increment(ref _corrected);
        else if (tracked == 0) Interlocked.Increment(ref _flat);
        else Interlocked.Increment(ref _partial);
    }
}
