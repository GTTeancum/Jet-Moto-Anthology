namespace JetMoto;

/// <summary>Session-only override of the original track availability query.</summary>
public static class TrackAccess
{
    public static bool Enabled { get; private set; }
    private static readonly HashSet<uint> Reported = [];

    public static void Configure(bool enabled)
    {
        Enabled = enabled;
        Reported.Clear();
        if (enabled)
            Console.WriteLine("[JetMoto:unlockall] All 10 tracks available for this session; saved unlock progress is unchanged.");
    }

    // Original func_8014046C indexes the difficulty x ten-track availability
    // table at 0x8018A9BC. A1 is the dial slot, V0 is its original result.
    // Override only this read's result: never modify the table, difficulty,
    // championship records, save buffers or memory-card files.
    public static uint Availability(uint dialSlot, uint original)
    {
        if (!Enabled || dialSlot >= 10) return original;
        if (Reported.Add(dialSlot))
            Console.WriteLine($"[JetMoto:unlockall] Track dial slot {dialSlot}: available (original={original}).");
        return 1;
    }

    // This value is consumed only by the track selector's TRACKS%d.BS/DMD
    // requests at 8013EF64, not by race difficulty or progression state.
    public static uint MenuArtworkVariant(uint original) => Enabled && original < 4 ? 3u : original;
}
