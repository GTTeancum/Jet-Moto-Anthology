using System.Security.Cryptography;
using RecompOne.Runtime.Context;
using RecompOne.Runtime.Memory;

namespace JetMoto;

// Diagnostic source provenance only. This does not select replacement textures.
public static class MenuSourceTrace
{
    private sealed record Source(string Name, uint Start, uint Length);
    private static readonly List<Source> Sources = [];
    private static readonly HashSet<string> Seen = [];
    private static readonly bool Enabled = Environment.GetEnvironmentVariable("JETMOTO_TRACE_MENU_SOURCES") == "1";

    public static void Reset() { Sources.Clear(); Seen.Clear(); }

    public static void Invalidate(uint address, uint length)
    {
        if (!Enabled) return;
        uint start = address & 0x1FFFFFFF;
        Sources.RemoveAll(s => start < (long)s.Start + s.Length && (long)start + length > s.Start);
    }

    public static void Loaded(PSMemory memory, string name, uint address, uint length, uint result, uint caller)
    {
        if (!Enabled || result == 0 || !(name.EndsWith(".BS") || name.EndsWith(".TIM"))) return;
        uint start = address & 0x1FFFFFFF;
        if (length == 0 || (long)start + length > memory.Ram.Length) return;
        Sources.Add(new(name, start, length));
        string hash = Convert.ToHexString(SHA256.HashData(memory.Ram.Slice((int)start, (int)length)));
        Log($"load source={name} address={address:X8} length={length} result={result} caller={caller:X8} loadedSha256={hash}");
    }

    public static void Decode(CpuContext cpu, PSMemory memory)
    {
        if (!Enabled) return;
        uint start = cpu.A0 & 0x1FFFFFFF;
        var source = Sources.LastOrDefault(s => start >= s.Start && start < (long)s.Start + s.Length);
        Log($"vlc source={source?.Name ?? "untracked"} address={cpu.A0:X8} offset={(source == null ? -1L : start - source.Start)} output={cpu.A1:X8} caller={cpu.RA:X8}");
    }

    public static void Background(CpuContext cpu, PSMemory memory)
    {
        if (!Enabled) return;
        uint start = cpu.A0 & 0x1FFFFFFF;
        var source = Sources.LastOrDefault(s => start >= s.Start && start < (long)s.Start + s.Length);
        Log($"background source={source?.Name ?? "untracked"} address={cpu.A0:X8} size={cpu.A1}x{cpu.A2} destination={cpu.A3},{memory.ReadU32(cpu.SP + 16)} caller={cpu.RA:X8}");
    }

    public static void Upload(CpuContext cpu, PSMemory memory)
    {
        if (!Enabled) return;
        uint start = cpu.A1 & 0x1FFFFFFF;
        var source = Sources.LastOrDefault(s => start >= s.Start && start < (long)s.Start + s.Length);
        if (source == null) return;
        uint rect = cpu.A0 & 0x1FFFFFFF;
        if ((long)rect + 8 > memory.Ram.Length) return;
        Log($"upload source={source.Name} address={cpu.A1:X8} offset={start - source.Start} " +
            $"rect={(short)memory.ReadU16(rect)},{(short)memory.ReadU16(rect + 2)}," +
            $"{memory.ReadU16(rect + 4)},{memory.ReadU16(rect + 6)} caller={cpu.RA:X8}");
    }

    private static void Log(string message)
    {
        if (Seen.Count < 512 && Seen.Add(message)) Console.WriteLine(
            $"[JetMoto:menu-source] frame={RecompOne.Runtime.Interrupts.VBlankCount} " + message);
    }
}
