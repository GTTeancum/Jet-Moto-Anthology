using System.Text.Json;
using RecompOne.Runtime;
using RecompOne.Runtime.Hardware;

namespace JetMoto;

internal static class ValidationReplay
{
    private sealed record Step(long Start, long End, ushort Buttons,string? Clock);
    private static long _raceStart=-1;

    // Called from the source-anchored race scope. It changes only this
    // process's validation clock; production launches never configure a replay.
    public static void MarkRaceStart()
    {
        if(Interlocked.CompareExchange(ref _raceStart,Interrupts.VBlankCount,-1)!=-1)return;
        string value=Volatile.Read(ref _raceStart).ToString(System.Globalization.CultureInfo.InvariantCulture);
        Environment.SetEnvironmentVariable("JETMOTO_REPLAY_RACE_START_VBLANK",value);
        Console.WriteLine($"[JetMoto:replay] Race-relative clock began at VBlank {value}.");
    }

    public static void Configure()
    {
        Widescreen.RaceEntered=null;
        Controller.ReplayButtons=null;
        Interlocked.Exchange(ref _raceStart,-1);
        Environment.SetEnvironmentVariable("JETMOTO_REPLAY_RACE_START_VBLANK",null);
        string? path = Environment.GetEnvironmentVariable("JETMOTO_REPLAY");
        if (string.IsNullOrWhiteSpace(path)) return;
        var steps = JsonSerializer.Deserialize<Step[]>(File.ReadAllText(path))
            ?? throw new InvalidDataException("Missing replay steps");
        if (steps.Length > 10000 || steps.Any(s => s.Start < 0 || s.End <= s.Start ||
            (s.Clock is not null && !string.Equals(s.Clock,"race",StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(s.Clock,"global",StringComparison.OrdinalIgnoreCase))))
            throw new InvalidDataException("Invalid replay interval");
        Widescreen.RaceEntered=MarkRaceStart;
        Controller.ReplayButtons = () =>
        {
            long frame = Interrupts.VBlankCount;
            long raceStart=Volatile.Read(ref _raceStart);
            long raceFrame=frame-raceStart;
            ushort pressed = 0;
            foreach (var step in steps)
            {
                bool race=string.Equals(step.Clock,"race",StringComparison.OrdinalIgnoreCase);
                long clock=race ? raceFrame : frame;
                if((!race||raceStart>=0)&&clock>=step.Start&&clock<step.End)pressed|=step.Buttons;
            }
            return (ushort)~pressed;
        };
        Console.WriteLine($"[JetMoto:replay] Loaded {steps.Length} global/race-relative intervals from {path}; process-local input only.");
    }
}
