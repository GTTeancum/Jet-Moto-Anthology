namespace JetMotoLauncher;

sealed class Options
{
    public string? Disc;
    public string? Game;          // "jm1" / "jm2" / "jm3", else detected from the disc
    public string? Extract;       // output folder for --extract
    public bool Rebuild;          // ignore the cache and recompile

    const string Help = """
        Jet Moto Anthology

          JetMoto [--disc <path>] [--game jm1|jm2|jm3] [--extract [folder]] [--rebuild]

        --disc <path>      A .cue of a disc you own, or a folder previously
                           produced by --extract. Without this the launcher looks
                           beside itself and in the current directory.
        --game jm1|jm2|jm3 Force which port to build. Detected from the disc
                           otherwise.
        --extract [folder] Extract the disc to loose files with an ogg
                           soundtrack, then exit. Defaults to <disc name>_loose.
                           The port prefers a loose folder when it finds one.
        --rebuild          Discard the cached recompilation and redo it.

        This program contains no game code or data. The first launch translates
        your disc's executable and caches the result next to this file; later
        launches start immediately.
        """;

    public static Options? Parse(string[] args)
    {
        var o = new Options();
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-h" or "--help" or "/?":
                    string exe = Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? "JetMoto");
                    Console.WriteLine(Help.Replace("JetMoto [", $"{exe} ["));
                    return null;
                case "--disc" when i + 1 < args.Length:
                    o.Disc = args[++i]; break;
                case "--game" when i + 1 < args.Length:
                    o.Game = args[++i].ToLowerInvariant(); break;
                case "--rebuild":
                    o.Rebuild = true; break;
                case "--extract":
                    // optional argument; a following token that is not a flag
                    o.Extract = (i + 1 < args.Length && !args[i + 1].StartsWith('-'))
                        ? args[++i] : "";
                    break;
                default:
                    // a bare path is the disc, so drag-and-drop onto the exe works
                    if (!args[i].StartsWith('-') && o.Disc is null) o.Disc = args[i];
                    else throw new ArgumentException($"unknown option: {args[i]}");
                    break;
            }
        }
        return o;
    }
}
