using RecompOne.Runtime;
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
        looseDir: Path.Combine(repo, "JetMoto3_loose"),
        image: Environment.GetEnvironmentVariable("JETMOTO3_CUE")
               ?? Path.Combine(repo, "Jet Moto 3 (USA).cue"));

// Jet Moto 3 needs runtime behaviour the other two must not get: a vblank
// clock for loops that call nothing, 24-bit output from the software VRAM
// shadow, and an unpaced stream feeder. Everything here is off by default, so
// Jet Moto 1 and 2 run exactly the code they ran before any of it existed.
GameQuirks.Apply("jm3");

var mem = new PSMemory();
Entry.Run(mem, disc != null && (File.Exists(disc) || LooseDisc.Is(disc)) ? disc : null, "Jet Moto 3");
return 0;
