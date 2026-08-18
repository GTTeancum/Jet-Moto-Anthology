using RecompOne.Runtime.Context;
using RecompOne.Runtime.Host;
using RecompOne.Runtime.Memory;
using RecompOne.Runtime.Sdk;

namespace Recompiled;

/// <summary>Jet Moto 3 developer fast-start hooks selected by --track.</summary>
public static class Jm3FastBoot
{
    static readonly string? Track = Environment.GetEnvironmentVariable("RECOMPONE_TRACK");

    public static void PlayMovie(CpuContext c, IMemory m)
    {
        uint movie = c.A0;
        uint descriptor = 0x800F60E0u + movie * 32u;

        if (!string.IsNullOrWhiteSpace(Track))
        {
            string path = ReadCString(m, m.ReadU32(descriptor));
            Console.Error.WriteLine($"[FastTrack] skip movie {movie}: {path}");
            ScriptedInput.NotifyJm3Movie(path);
            LibCdStream.StopMoviePlayback("fast-track movie skip");

            // This cleanup is also the final call made by the original wrapper.
            // Keeping it lets the shell advance its own movie/menu state safely.
            JetMoto3.func_800150D4(c, m);
            return;
        }

        // Original func_800DCA44, kept here so normal launches are unchanged.
        if (m.ReadU8(0x80099758u) != 0)
        {
            c.A0 = 0x800F3720u;
            c.A1 = m.ReadU32(descriptor);
            JetMoto3.func_8003FB10(c, m);

            c.A0 = 0x7Fu;
            c.A1 = 0;
            JetMoto3.func_8006F904(c, m);

            c.A0 = descriptor;
            JetMoto3.func_800EC844(c, m);
            c.A0 = descriptor;
            JetMoto3.func_800EC9B0(c, m);
        }

        JetMoto3.func_800150D4(c, m);
    }

    static string ReadCString(IMemory m, uint address)
    {
        if (address == 0) return "<null>";
        var chars = new List<char>();
        for (int i = 0; i < 260; i++)
        {
            byte b = m.ReadU8(address + (uint)i);
            if (b == 0) break;
            chars.Add((char)b);
        }
        return new string(chars.ToArray());
    }
}
