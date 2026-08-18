using RecompOne.Runtime;
using RecompOne.Runtime.Cdrom;
using RecompOne.Runtime.Memory;
using Recompiled;

string[] trackNames =
[
    "canyon", "ice", "volcano"
];

string? track = null;
string? discOverride = null;
for (int i = 0; i < args.Length; i++)
{
    string arg = args[i];
    if (arg is "--help" or "-h")
    {
        Console.WriteLine("JetMoto3 [disc-or-loose-directory] [--track NAME]");
        Console.WriteLine("JetMoto3 --canyon   (track names are also switches)");
        Console.WriteLine($"Tracks: {string.Join(", ", trackNames)}");
        return 0;
    }

    if (arg.Equals("--track", StringComparison.OrdinalIgnoreCase))
    {
        if (++i >= args.Length)
        {
            Console.Error.WriteLine("--track requires a track name");
            return 2;
        }
        track = args[i].ToLowerInvariant();
        continue;
    }

    if (arg.StartsWith("--track=", StringComparison.OrdinalIgnoreCase))
    {
        track = arg[8..].ToLowerInvariant();
        continue;
    }

    if (arg.StartsWith("--", StringComparison.Ordinal))
    {
        string alias = arg[2..].ToLowerInvariant();
        if (trackNames.Contains(alias))
        {
            track = alias;
            continue;
        }
        Console.Error.WriteLine($"unknown option: {arg}");
        return 2;
    }

    if (discOverride != null)
    {
        Console.Error.WriteLine($"unexpected argument: {arg}");
        return 2;
    }
    discOverride = arg;
}

if (track != null && !trackNames.Contains(track))
{
    Console.Error.WriteLine($"unknown track '{track}'. Tracks: {string.Join(", ", trackNames)}");
    return 2;
}

if (track != null)
{
    Environment.SetEnvironmentVariable("RECOMPONE_TRACK", track);
    Console.Error.WriteLine($"[FastTrack] requested {track}");
}

// Disc: prefer an extracted loose-file tree (tools/extract-disc.py), fall back
// to the bin/cue image. A positional argument overrides either, and may be a loose
// directory or a cue. RECOMPONE_DISC / RECOMPONE_DISC_PREFER also apply.
string repo = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..");
string localLoose = LooseDisc.Is(AppContext.BaseDirectory)
    ? AppContext.BaseDirectory
    : Path.Combine(repo, "JetMoto3_loose");
string? disc = discOverride != null
    ? discOverride
    : DiscSource.Resolve(
        looseDir: localLoose,
        image: Environment.GetEnvironmentVariable("JETMOTO3_CUE")
               ?? Path.Combine(repo, "Jet Moto 3 (USA).cue"));

// Jet Moto 3 needs runtime behaviour the other two must not get: a vblank
// clock for loops that call nothing, 24-bit output from the software VRAM
// shadow, and an unpaced stream feeder. Everything here is off by default, so
// Jet Moto 1 and 2 run exactly the code they ran before any of it existed.
GameQuirks.Apply("jm3");

if (disc != null && (File.Exists(disc) || LooseDisc.Is(disc)))
{
    RecompOne.Runtime.Config.ConfigManager.Game.CdPath = Path.GetFullPath(disc);
    RecompOne.Runtime.Config.ConfigManager.SaveGame();
}

var mem = new PSMemory();
Entry.Run(mem, disc != null && (File.Exists(disc) || LooseDisc.Is(disc)) ? disc : null, "Jet Moto 3");
return 0;
