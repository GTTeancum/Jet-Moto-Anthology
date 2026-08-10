using System.Diagnostics;
using System.Reflection;
using JetMotoLauncher;
using RecompOne.Runtime.Cdrom;
using RecompOne.Runtime.Memory;

// Jet Moto Anthology launcher.
//
// This executable contains no game code. The recompiler runs here, on the
// player's machine, against the disc they own: it translates that disc's
// executable to C#, compiles it in memory, and runs it. The result is cached so
// only the first launch pays for it.

try
{
    var opt = Options.Parse(args);
    if (opt is null) return 0;                 // --help printed

    string? disc = DiscPicker.Resolve(opt.Disc);
    if (disc is null)
    {
        Console.Error.WriteLine(
            "No disc found.\n\n" +
            "Jet Moto Anthology ships no game data -- you supply a rip of a disc\n" +
            "you own. Put the .cue (or an extracted folder) beside this program,\n" +
            "or pass it:\n\n" +
            "    JetMoto --disc \"D:\\rips\\Jet Moto (USA).cue\"\n");
        return 2;
    }

    var title = GameProfile.Detect(disc, opt.Game);
    if (title is null)
    {
        Console.Error.WriteLine($"Not a Jet Moto disc: {disc}");
        return 2;
    }
    Console.WriteLine($"[Launcher] {title.Name}  <-  {disc}");

    if (opt.Extract is not null)
    {
        return DiscExtractor.Run(disc, opt.Extract, title) ? 0 : 1;
    }

    var asm = Recompile.LoadOrBuild(disc, title, opt.Rebuild);

    var entry = asm.GetType("Recompiled.Entry")
                ?? throw new InvalidOperationException("recompiled assembly has no Recompiled.Entry");
    var run = entry.GetMethod("Run", BindingFlags.Public | BindingFlags.Static)
              ?? throw new InvalidOperationException("Recompiled.Entry has no Run method");

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
