using NativeFileDialogSharp;
using RecompOne.Runtime.Cdrom;

namespace JetMotoLauncher;

static class DiscPicker
{
    /// <summary>
    /// Find the disc for <paramref name="want"/>: what was asked for, else
    /// RECOMPONE_DISC, else a search beside the executable.
    ///
    /// The search checks what each candidate actually is rather than taking the
    /// first one it meets. Sorting by filename meant "Jet Moto (USA).cue" always
    /// won, so with both discs present Jet Moto 2 was simply unreachable.
    /// </summary>
    public static string? Resolve(string? requested, GameProfile? want)
    {
        if (!string.IsNullOrWhiteSpace(requested))
        {
            if (LooseDisc.Is(requested) || File.Exists(requested)) return requested;
            Console.Error.WriteLine($"[Launcher] not found: {requested}");
            return null;
        }

        var env = Environment.GetEnvironmentVariable("RECOMPONE_DISC");
        if (!string.IsNullOrWhiteSpace(env) && (LooseDisc.Is(env) || File.Exists(env))) return env;

        var candidates = Candidates().ToList();
        if (want is not null)
        {
            foreach (var c in candidates)
                if (GameProfile.Detect(c, null)?.Key == want.Key) return c;
        }
        else if (candidates.Count > 0)
        {
            return candidates[0];
        }

        return Ask(want);
    }

    static IEnumerable<string> Candidates()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dir in Roots())
        {
            if (!Directory.Exists(dir)) continue;

            // The folder itself, before anything inside it. Dropping the
            // executable straight into an extracted tree is the normal way to
            // deploy one of these -- the disc root and the program end up in the
            // same directory -- and checking only subdirectories meant that
            // arrangement was invisible, so a fully extracted game still stopped
            // to ask for a disc it already had.
            if (LooseDisc.Is(dir) && seen.Add(Path.GetFullPath(dir)))
                yield return dir;

            // an extracted tree is a deliberate choice, so it goes first
            foreach (var sub in Directory.EnumerateDirectories(dir))
                if (LooseDisc.Is(sub) && seen.Add(Path.GetFullPath(sub)))
                    yield return sub;

            foreach (var cue in Directory.EnumerateFiles(dir, "*.cue").OrderBy(f => f))
                if (seen.Add(Path.GetFullPath(cue)))
                    yield return cue;
        }
    }

    /// <summary>
    /// The folder the executable actually sits in.
    ///
    /// Not AppContext.BaseDirectory: this ships as a single file, and for one of
    /// those BaseDirectory is the temporary self-extraction directory. Anything
    /// looked for -- or written -- there is invisible to the player and swept
    /// away later. Recompile.cs makes the same distinction for its cache.
    /// </summary>
    public static string AppFolder =>
        Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;

    static IEnumerable<string> Roots()
    {
        yield return AppFolder;
        yield return AppContext.BaseDirectory;
        yield return Directory.GetCurrentDirectory();
        // one level up covers the common "game/ next to discs/" arrangement
        yield return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".."));
    }

    /// <summary>
    /// Nothing was found, so ask. Someone who double-clicked the executable has
    /// no command line to put --disc on, and telling them to open a terminal is
    /// not a release.
    /// </summary>
    static string? Ask(GameProfile? want)
    {
        try
        {
            Console.WriteLine($"[Launcher] choose your {want?.Name ?? "Jet Moto"} .cue file");
            var r = Dialog.FileOpen("cue", Directory.GetCurrentDirectory());
            if (r.IsOk && File.Exists(r.Path)) return r.Path;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"[Launcher] could not open a file picker: {e.Message}");
        }
        return null;
    }
}
