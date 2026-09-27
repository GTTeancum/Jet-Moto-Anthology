using System.Diagnostics;
using RecompOne.Runtime.Assets.Native;

namespace RecompOne.Runtime.Diagnostics;

// Opt-in, bounded telemetry. Presentation intervals are not emulated vblanks.
public static class PerformanceDiagnostics
{
    public static readonly bool Enabled = Environment.GetEnvironmentVariable("JETMOTO_PERF") == "1";
    private static readonly object Gate = new();
    private static readonly double[] Intervals = new double[8192];
    private static int _next, _used;
    private static long _lastPresent, _presents, _shadowTicks, _shadowCount, _wakeTicks, _wakeCount;
    private static long _uploads, _drawMisses, _gpuBytes, _gpuPeak, _gpuEvictions;
    private static long _lastReport, _lastAllocated;
    private static readonly int[] Collections = new int[3];
    public static long Start() => Enabled ? Stopwatch.GetTimestamp() : 0;
    public static void Shadow(long start) { if (Enabled) { Interlocked.Add(ref _shadowTicks, Stopwatch.GetTimestamp()-start); Interlocked.Increment(ref _shadowCount); } }
    public static void Wake(long start) { if (Enabled) { Interlocked.Add(ref _wakeTicks, Stopwatch.GetTimestamp()-start); Interlocked.Increment(ref _wakeCount); } }
    public static void Texture(bool prepared, long bytes, long peak, long evictions)
    {
        if (!Enabled) return;
        Interlocked.Increment(ref _uploads); if (!prepared) Interlocked.Increment(ref _drawMisses);
        Interlocked.Exchange(ref _gpuBytes, bytes); Interlocked.Exchange(ref _gpuPeak, peak); Interlocked.Exchange(ref _gpuEvictions, evictions);
    }
    public static void Present()
    {
        if (!Enabled) return;
        long now = Stopwatch.GetTimestamp();
        lock (Gate)
        {
            if (_lastPresent != 0) { Intervals[_next] = (now-_lastPresent)*1000.0/Stopwatch.Frequency; _next=(_next+1)%Intervals.Length; _used=Math.Min(_used+1,Intervals.Length); }
            _lastPresent=now; _presents++;
        }
    }
    public static string Summary()
    {
        if (!Enabled) return "";
        long now=Stopwatch.GetTimestamp(), allocated=GC.GetTotalAllocatedBytes(false);
        double seconds=_lastReport==0 ? 0 : (now-_lastReport)/(double)Stopwatch.Frequency;
        double rate=seconds>0 ? (allocated-_lastAllocated)/1048576.0/seconds : 0;
        _lastReport=now; _lastAllocated=allocated;
        double p50=0,p95=0,p99=0;
        lock (Gate)
        {
            if (_used>0) { var values=Intervals.AsSpan(0,_used).ToArray(); Array.Sort(values);
                p50=values[(int)((values.Length-1)*.5)];p95=values[(int)((values.Length-1)*.95)];p99=values[(int)((values.Length-1)*.99)]; }
            _next=_used=0;
        }
        int g0=GC.CollectionCount(0),g1=GC.CollectionCount(1),g2=GC.CollectionCount(2);
        string gc=$"{g0-Collections[0]}/{g1-Collections[1]}/{g2-Collections[2]}";
        Collections[0]=g0;Collections[1]=g1;Collections[2]=g2;
        var cpu=NativeTextureAsset.CacheStats;
        double Avg(long ticks,long count)=>count==0?0:ticks*1000.0/Stopwatch.Frequency/count;
        return FormattableString.Invariant($"perf[presents={_presents},presentP50Ms={p50:F2},presentP95Ms={p95:F2},presentP99Ms={p99:F2},allocatedMiBps={rate:F2},gc={gc},shadowMeanMs={Avg(_shadowTicks,_shadowCount):F3},wakeMeanMs={Avg(_wakeTicks,_wakeCount):F3},cpuTexturesMiB={cpu.Bytes/1048576.0:F1},gpuTexturesMiB={_gpuBytes/1048576.0:F1},gpuPeakMiB={_gpuPeak/1048576.0:F1},gpuEvictions={_gpuEvictions},uploads={_uploads},drawMisses={_drawMisses}]");
    }
}
