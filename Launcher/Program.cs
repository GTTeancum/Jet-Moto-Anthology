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
        Gui.Error(
            $"No {name} disc found.\n\n" +
            "This program ships no game data - you supply a rip of a disc you own.\n\n" +
            $"Put the .cue next to {exe}.exe and run it again, or pick it when asked.");
        return 2;
    }

    var title = GameProfile.Detect(disc, opt.Game) ?? want;
    if (title is null)
    {
        Gui.Error($"That is not a Jet Moto disc:\n\n{disc}");
        return 2;
    }
    if (want is not null && title.Key != want.Key)
    {
        Gui.Error($"That is a {title.Name} disc, and this is the {want.Name} build.\n\n" +
                  $"Run {title.Exe}.exe instead.");
        return 2;
    }

    Console.WriteLine($"[Launcher] {title.Name}  <-  {disc}");

    if (opt.Extract is not null)
        return DiscExtractor.Run(disc, opt.Extract, title) ? 0 : 1;

    // What we settled on is still a disc image, which means no extracted tree
    // was found. Extract it into this executable's own folder and run from
    // there, so the next launch finds the tree beside itself and starts
    // straight away. This is the whole first-run experience: point at the
    // bin/cue once, never be asked again.
    //
    // A failure here is not fatal -- the port can boot from the image directly,
    // it just loses the ogg soundtrack and the faster reads.
    string image = disc;
    System.Reflection.Assembly? loaded = null;

    // Extraction and recompilation both take a while on a first run and both
    // report progress by writing to the console. SetupWindow puts that on
    // screen instead -- and shows nothing at all when the work turns out to be
    // cached and finishes in a fraction of a second.
    SetupWindow.Run($"Preparing {title.Name}", report => StatusWriter.Capture(report, () =>
    {
        if (!Directory.Exists(image))
        {
            string here = DiscPicker.AppFolder.TrimEnd(Path.DirectorySeparatorChar);
            Console.WriteLine($"Extracting {title.Name} to {here}");
            if (DiscExtractor.Run(image, here, title)) disc = here;
        }
        loaded = Recompile.LoadOrBuild(disc, title, opt.Rebuild);
        return true;
    }));

    var asm = loaded ?? throw new InvalidOperationException("nothing was built");

    var entry = asm.GetType("Recompiled.Entry")
                ?? throw new InvalidOperationException("recompiled assembly has no Recompiled.Entry");
    var run = entry.GetMethod("Run", BindingFlags.Public | BindingFlags.Static)
              ?? throw new InvalidOperationException("Recompiled.Entry has no Run method");

    // Per-game runtime behaviour, off for anything that does not ask. Must run
    // before PSMemory is constructed: the memory path reads these flags.
    RecompOne.Runtime.GameQuirks.Apply(title.Key);

    // Tell the runtime which disc we settled on. Entry.Run calls
    // WaitForValidDisc before it opens anything, and that spins until the
    // *runtime's* configured disc path is a real file -- so a first run with an
    // empty settings.json sat in the disc picker even though --disc had already
    // named the disc and the recompile had already used it.
    RecompOne.Runtime.Config.ConfigManager.Game.CdPath = Path.GetFullPath(disc);
    RecompOne.Runtime.Config.ConfigManager.SaveGame();

    var mem = new PSMemory();
    run.Invoke(null, [mem, disc, title.Name]);
    return 0;
}
catch (TargetInvocationException tie) when (tie.InnerException is not null)
{
    Gui.Error(tie.InnerException.Message);
    Console.Error.WriteLine(tie.InnerException);
    return 1;
}
catch (Exception e)
{
    Gui.Error(e.Message);
    Console.Error.WriteLine(e);
    return 1;
}
