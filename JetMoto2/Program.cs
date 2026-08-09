using RecompOne.Runtime.Cdrom;
using RecompOne.Runtime.Memory;
using Recompiled;

// Disc: prefer an extracted loose-file tree (tools/extract-disc.py), fall back
// to the bin/cue image. argv[0] overrides either, and may itself be a loose
// directory or a cue. RECOMPONE_DISC / RECOMPONE_DISC_PREFER also apply.
string repo = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..");
string? disc = args.Length > 0
    ? args[0]
    : DiscSource.Resolve(
        looseDir: Path.Combine(repo, "JetMoto2", "disc"),
        image: Environment.GetEnvironmentVariable("JETMOTO2_CUE")
               ?? Path.Combine(repo, "JetMoto2_PS1image", "Jet Moto 2 (v1.1).cue"));

var mem = new PSMemory();
Entry.Run(mem, disc != null && (File.Exists(disc) || LooseDisc.Is(disc)) ? disc : null, "Jet Moto 2");
return 0;
