namespace JetMoto;

/// <summary>Disc selection is independent of the process working directory and saved settings.</summary>
internal static class DiscLocator
{
    private static readonly string[] PreferredNames = ["Jet Moto (USA).cue", "Jet Moto.cue", "JetMoto.cue"];

    public static string FindLocalDisc(string executableDirectory, Func<string, string?> validate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executableDirectory);
        ArgumentNullException.ThrowIfNull(validate);
        string directory = Path.GetFullPath(executableDirectory);
        string[] candidates = Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
            .Where(p => Path.GetExtension(p).Equals(".cue", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase).ToArray();
        if (candidates.Length == 0)
            throw new FileNotFoundException("No CUE file was found beside JetMoto.exe. " +
                "Keep the extracted Jet Moto CUE and all 14 BIN tracks in the same folder as JetMoto.exe.");

        var valid = new List<string>();
        var errors = new List<string>();
        foreach (string candidate in candidates)
        {
            string? error;
            try { error = validate(candidate); }
            catch (Exception e) { error = e.Message; }
            if (error == null) valid.Add(candidate);
            else errors.Add(Path.GetFileName(candidate) + ": " + error);
        }
        // Never pick a merely similarly-named disc: validation must succeed first.
        foreach (string preferred in PreferredNames)
        {
            string? match = valid.FirstOrDefault(p => Path.GetFileName(p).Equals(preferred, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;
        }
        if (valid.Count == 1) return valid[0];
        if (valid.Count > 1)
            throw new InvalidOperationException("More than one matching Jet Moto CUE was found. " +
                "Keep one CUE in this folder, or name the one to use Jet Moto (USA).cue. " +
                "Do not rename its BIN tracks.");
        throw new InvalidDataException("No usable Jet Moto USA disc was found beside JetMoto.exe.\n\n" +
            string.Join("\n", errors.Take(4)) + (errors.Count > 4 ? "\nAdditional invalid CUE files were omitted." : ""));
    }
}
