using System.Text.Json;
using RecompOne.Runtime;
using RecompOne.Runtime.Hardware;

namespace JetMoto;

internal static class ValidationReplay
{
    private sealed record Step(long Start, long End, ushort Buttons);

    public static void Configure()
    {
        string? path = Environment.GetEnvironmentVariable("JETMOTO_REPLAY");
        if (string.IsNullOrWhiteSpace(path)) return;
        var steps = JsonSerializer.Deserialize<Step[]>(File.ReadAllText(path))
            ?? throw new InvalidDataException("Missing replay steps");
        if (steps.Length > 10000 || steps.Any(s => s.Start < 0 || s.End <= s.Start))
            throw new InvalidDataException("Invalid replay interval");
        Controller.ReplayButtons = () =>
        {
            long frame = Interrupts.VBlankCount;
            ushort pressed = 0;
            foreach (var step in steps)
                if (frame >= step.Start && frame < step.End) pressed |= step.Buttons;
            return (ushort)~pressed;
        };
        Console.WriteLine($"[JetMoto:replay] Loaded {steps.Length} VBlank intervals from {path}; process-local input only.");
    }
}
