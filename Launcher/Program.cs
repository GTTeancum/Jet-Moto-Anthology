using System.Reflection;
using JetMotoLauncher;
using RecompOne.Runtime.Memory;

// Jet Moto Anthology launcher.
//
// This executable contains no game code. The recompiler runs here, on the
// player's machine, against the disc they own: it translates that disc's
// executable to C#, compiles it in memory, and runs it. The result is cached so
// only the first launch pays for it.
//
// The same binary ships twice, as JetMoto and JetMoto2. Which game it is comes
// from its own file name.

try
{
    var opt = Options.Parse(args);
    if (opt is null) return 0;                 // --help printed

    var want = opt.Game is not null ? GameProfile.ForKey(opt.Game) : GameProfile.Pinned();

    string? disc = DiscPicker.Resolve(opt.Disc, want);
    if (disc is null)
    {
        string name = want?.Name ?? "Jet Moto";
        string exe = want?.Exe ?? "JetMoto";
        Console.Error.WriteLine(
            $"No {name} disc found.\n\n" +
            "This program ships no game data - you supply a rip of a disc you\n" +
            "own. Put the .cue (or an extracted folder) beside this program, or\n" +
            "pass it:\n\n" +
            $"    {exe} --disc \"D:\\rips\\{name} (USA).cue\"\n");
        return 2;
    }

    var title = GameProfile.Detect(disc, opt.Game) ?? want;
    if (title is null)
    {
        Console.Error.WriteLine($"Not a Jet Moto disc: {disc}");
        return 2;
    }
    if (want is not null && title.Key != want.Key)
    {
        Console.Error.WriteLine(
            $"That is a {title.Name} disc; this is the {want.Name} build.\n" +
            $"Run {title.Exe} instead, or pass --game {title.Key} to override.");
        return 2;
    }

    Console.WriteLine($"[Launcher] {title.Name}  <-  {disc}");

    if (opt.Extract is not null)
        return DiscExtractor.Run(disc, opt.Extract, title) ? 0 : 1;

    var asm = Recompile.LoadOrBuild(disc, title, opt.Rebuild);

    var entry = asm.GetType("Recompiled.Entry")
                ?? throw new InvalidOperationException("recompiled assembly has no Recompiled.Entry");
    var run = entry.GetMethod("Run", BindingFlags.Public | BindingFlags.Static)
              ?? throw new InvalidOperationException("Recompiled.Entry has no Run method");

    // Per-game runtime behaviour, off for anything that does not ask. Must run
    // before PSMemory is constructed: the memory path reads these flags.
    RecompOne.Runtime.GameQuirks.Apply(title.Key);

    var mem = new PSMemory();
    run.Invoke(null, [mem, disc, title.Name]);
    return 0;
}
catch (TargetInvocationException tie) when (tie.InnerException is not null)
{
    Console.Error.WriteLine(tie.InnerException);
    return 1;
}
catch (Exception e)
{
    Console.Error.WriteLine(e.Message);
    return 1;
}
