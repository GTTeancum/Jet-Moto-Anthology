using RecompOne.Runtime.Cdrom;

namespace JetMotoLauncher;

/// <summary>
/// Which of the two ports a disc is, and the configuration that drives it.
/// Detection reads the boot executable's name off the disc rather than trusting
/// a filename, so a rip called anything at all still works.
/// </summary>
sealed class GameProfile
{
    public required string Key { get; init; }        // jm1 / jm2
    public required string Name { get; init; }
    public required string Resource { get; init; }   // embedded config
    public required string BootExe { get; init; }    // SCUS_943.09 etc.
    public required string Exe { get; init; }        // the shipped executable name

    static readonly GameProfile[] All =
    [
        new() { Key = "jm1", Name = "Jet Moto",   Resource = "jetmoto.json",  BootExe = "SCUS_943.09", Exe = "JetMoto" },
        new() { Key = "jm2", Name = "Jet Moto 2", Resource = "jetmoto2.json", BootExe = "SCUS_941.67", Exe = "JetMoto2" },
    ];

    public static GameProfile? ForKey(string key) => All.FirstOrDefault(g => g.Key == key);

    /// <summary>
    /// Which game an executable is. Both games ship as their own exe -- one
    /// binary pinned two ways -- so the file name is the pin. Without it a
    /// single launcher would have to guess from whichever disc it found first,
    /// which is how Jet Moto 2 became unreachable whenever both discs were
    /// present.
    /// </summary>
    public static GameProfile? Pinned()
    {
        var exe = Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? "");
        return All.FirstOrDefault(g => string.Equals(g.Exe, exe, StringComparison.OrdinalIgnoreCase));
    }

    public static GameProfile? Detect(string disc, string? forced)
    {
        if (forced is not null)
            return ForKey(forced)
                   ?? throw new ArgumentException($"unknown --game: {forced}");

        // SYSTEM.CNF names the boot executable; that is the disc's own identity.
        try
        {
            using var fs = CueFs.Open(disc);
            string cnf;
            try { cnf = System.Text.Encoding.ASCII.GetString(fs.ReadFile("\\SYSTEM.CNF;1")); }
            catch { cnf = ""; }

            foreach (var g in All)
                if (cnf.Contains(g.BootExe, StringComparison.OrdinalIgnoreCase) ||
                    fs.Exists("\\" + g.BootExe + ";1"))
                    return g;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"[Launcher] could not read the disc: {e.Message}");
        }
        return null;
    }

    public string ReadConfig()
    {
        using var s = typeof(GameProfile).Assembly.GetManifestResourceStream(Resource)
            ?? throw new InvalidOperationException($"missing embedded config {Resource}");
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }
}
