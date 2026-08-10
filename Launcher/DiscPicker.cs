using RecompOne.Runtime.Cdrom;

namespace JetMotoLauncher;

static class DiscPicker
{
    /// <summary>
    /// Find a disc: what was asked for, else RECOMPONE_DISC, else an extracted
    /// folder or a .cue sitting beside the executable or in the working
    /// directory. Loose folders win, since a player who extracted one meant it.
    /// </summary>
    public static string? Resolve(string? requested)
    {
        if (!string.IsNullOrWhiteSpace(requested))
        {
            if (LooseDisc.Is(requested) || File.Exists(requested)) return requested;
            Console.Error.WriteLine($"[Launcher] not found: {requested}");
            return null;
        }

        var env = Environment.GetEnvironmentVariable("RECOMPONE_DISC");
        if (!string.IsNullOrWhiteSpace(env) && (LooseDisc.Is(env) || File.Exists(env))) return env;

        foreach (var dir in Roots())
        {
            if (!Directory.Exists(dir)) continue;

            foreach (var sub in Directory.EnumerateDirectories(dir))
                if (LooseDisc.Is(sub)) return sub;

            var cue = Directory.EnumerateFiles(dir, "*.cue").OrderBy(f => f).FirstOrDefault();
            if (cue is not null) return cue;
        }
        return null;
    }

    static IEnumerable<string> Roots()
    {
        yield return AppContext.BaseDirectory;
        yield return Directory.GetCurrentDirectory();
        // one level up covers the common "game/ next to discs/" arrangement
        var up = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".."));
        yield return up;
    }
}
