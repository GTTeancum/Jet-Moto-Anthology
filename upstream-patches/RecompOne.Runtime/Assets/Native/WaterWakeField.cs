using System.Numerics;

namespace RecompOne.Runtime.Assets.Native;

/// <summary>World-space ribbon coverage. Receivers remain native water surfaces;
/// proximity to a verified water plane rejects airborne/below-surface paths.</summary>
public static class WaterWakeField
{
    public const int Size = 1024;
    public const float Extent = 512;
    public static Vector2 Origin(WorldCamera camera) =>
        new(MathF.Floor((camera.Eye.X - Extent * .5f) * Size / Extent) * Extent / Size,
            MathF.Floor((camera.Eye.Y - Extent * .5f) * Size / Extent) * Extent / Size);

    private static float Smooth(float a, float b, float x)
    {
        float t = Math.Clamp((x - a) / (b - a), 0, 1);
        return t * t * (3 - 2 * t);
    }

    public static int Rasterize(WorldCamera camera, IReadOnlyList<RiderPathSegment> segments, Span<byte> pixels)
    {
        if (pixels.Length != Size * Size * 4) throw new ArgumentException("Wake field dimensions", nameof(pixels));
        pixels.Clear();
        if (camera.Scene.FlatWaterLevel is not {} level) return 0;
        Vector2 origin = Origin(camera);
        const float scale = Size / Extent;
        int drawn = 0;
        foreach (var segment in segments)
        {
            Vector2 a = new(segment.A.X, segment.A.Y), b = new(segment.B.X, segment.B.Y);
            Vector2 direction = b - a;
            float length = direction.Length(), dt = segment.BornB - segment.BornA;
            if (!float.IsFinite(length + dt) || length < .01f || dt <= 0 || segment.BornB > camera.Time) continue;
            float speed = Smooth(3, 30, length / dt);
            float youngest = camera.Time - segment.BornB;
            if (speed <= 0 || youngest >= RiderMotionHistory.Lifetime) continue;
            if (MathF.Min(segment.A.Z, segment.B.Z) > level + 12 || MathF.Max(segment.A.Z, segment.B.Z) < level) continue;
            Vector2 normal = new(-direction.Y / length, direction.X / length);
            float radius = 2 + 3 * Math.Clamp(camera.Time - segment.BornA, 0, RiderMotionHistory.Lifetime);
            Vector2 lo = (Vector2.Min(a, b) - new Vector2(radius) - origin) * scale;
            Vector2 hi = (Vector2.Max(a, b) + new Vector2(radius) - origin) * scale;
            if (hi.X < 0 || hi.Y < 0 || lo.X >= Size || lo.Y >= Size) continue;
            int x0 = (int)Math.Clamp(MathF.Floor(lo.X), 0, Size - 1), x1 = (int)Math.Clamp(MathF.Ceiling(hi.X), 0, Size - 1);
            int y0 = (int)Math.Clamp(MathF.Floor(lo.Y), 0, Size - 1), y1 = (int)Math.Clamp(MathF.Ceiling(hi.Y), 0, Size - 1);
            bool wrote = false;
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                Vector2 p = origin + new Vector2(x + .5f, y + .5f) / scale;
                float t = Math.Clamp(Vector2.Dot(p - a, direction) / (length * length), 0, 1);
                if (!camera.Scene.AllowsWaterEmission(Vector3.Lerp(segment.A, segment.B, t))) continue;
                Vector2 delta = p - Vector2.Lerp(a, b, t);
                float age = camera.Time - (segment.BornA + dt * t);
                float height = segment.A.Z + (segment.B.Z - segment.A.Z) * t - level;
                float contact = Smooth(0, 1, height) * (1 - Smooth(6, 12, height));
                float fade = 1 - Smooth(.25f, RiderMotionHistory.Lifetime, age);
                float width = 1.6f + MathF.Max(0, age) * 2.7f;
                float distance = segment.DistanceA + (segment.DistanceB - segment.DistanceA) * t;
                // Flow-aligned lobes, with a coherent pair of spreading foam edges.
                float bend = .15f * MathF.Sin(distance * .65f + segment.RiderId);
                float across = MathF.Abs(Vector2.Dot(delta, normal) / width + bend);
                float cap = 1 - Smooth(width * .6f, width, delta.Length());
                float rim = 1 - Smooth(.10f, .30f, MathF.Abs(across - .66f));
                float body = 1 - Smooth(.15f, .8f, across);
                float strength = speed * contact * fade * cap;
                byte r = (byte)Math.Clamp((rim * .70f + body * .12f) * strength * 255, 0, 255);
                byte g = (byte)Math.Clamp(body * strength * 255, 0, 255);
                int i = (y * Size + x) * 4;
                pixels[i] = Math.Max(pixels[i], r);
                pixels[i + 1] = Math.Max(pixels[i + 1], g);
                if (r != 0 || g != 0) { pixels[i + 3] = 255; wrote = true; }
            }
            if (wrote) drawn++;
        }
        return drawn;
    }
}
