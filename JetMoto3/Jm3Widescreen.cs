using RecompOne.Runtime.Hle;

namespace Recompiled;

/// <summary>Jet Moto 3 projection bounds used by its world visibility test.</summary>
public static class Jm3Widescreen
{
    const int OriginalWidth = 640;
    const int OriginalRightExclusive = OriginalWidth + 1;

    static int Margin => GpuHle.WideMargin(OriginalWidth);

    public static int CullLeft => -Margin;
    public static int CullLeftPacked => -Margin << 16;
    public static int CullRightExclusive => OriginalRightExclusive + Margin;
}
