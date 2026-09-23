using System.Numerics;

namespace RecompOne.Runtime.Assets.Native;

/// <summary>Light-space depth from posed native rider triangles, never screen pixels.</summary>
public static class RiderShadowMap
{
    public const int Size = 1024;
    public const float Extent = 128;
    public const float DepthRange = 512;

    public static byte[] Rasterize(WorldCamera camera)
    {
        ushort[] depths = new ushort[Size * Size];
        byte[] pixels = new byte[Size * Size * 4];
        Vector3 light = camera.Scene.Sunlight;
        Vector3 right = Vector3.Normalize(Vector3.Cross(Vector3.UnitZ, light));
        Vector3 up = Vector3.Cross(light, right);
        Vector3 Project(Vector3 p)
        {
            p -= camera.Eye;
            return new((Vector3.Dot(p, right) / Extent + .5f) * Size,
                (Vector3.Dot(p, up) / Extent + .5f) * Size,
                Vector3.Dot(p, light) / DepthRange + .5f);
        }
        foreach (var triangle in camera.Casters)
        {
            Vector3 a = Project(triangle.A), b = Project(triangle.B), c = Project(triangle.C);
            float det = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
            if (!float.IsFinite(det) || MathF.Abs(det) < .00001f) continue;
            int x0 = Math.Max(0, (int)MathF.Floor(Math.Min(a.X, Math.Min(b.X, c.X))));
            int x1 = Math.Min(Size - 1, (int)MathF.Ceiling(Math.Max(a.X, Math.Max(b.X, c.X))));
            int y0 = Math.Max(0, (int)MathF.Floor(Math.Min(a.Y, Math.Min(b.Y, c.Y))));
            int y1 = Math.Min(Size - 1, (int)MathF.Ceiling(Math.Max(a.Y, Math.Max(b.Y, c.Y))));
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float dx = x + .5f - a.X, dy = y + .5f - a.Y;
                float u = (dx * (c.Y - a.Y) - dy * (c.X - a.X)) / det;
                float v = ((b.X - a.X) * dy - (b.Y - a.Y) * dx) / det;
                if (u < 0 || v < 0 || u + v > 1) continue;
                float depth = a.Z + u * (b.Z - a.Z) + v * (c.Z - a.Z);
                if (!(depth > 0 && depth < 1)) continue;
                ushort encoded = (ushort)(depth * 65535);
                int index = y * Size + x;
                if (encoded <= depths[index]) continue;
                depths[index] = encoded;
                pixels[index * 4] = (byte)(encoded >> 8);
                pixels[index * 4 + 1] = (byte)encoded;
                pixels[index * 4 + 2] = 255;
                pixels[index * 4 + 3] = 255;
            }
        }
        return pixels;
    }
}
