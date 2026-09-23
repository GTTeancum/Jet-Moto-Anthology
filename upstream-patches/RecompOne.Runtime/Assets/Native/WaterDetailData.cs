namespace RecompOne.Runtime.Assets.Native;

/// <summary>Periodic ripple normal/variation map; contains no scene or material color.</summary>
public static class WaterDetailData
{
    public const int Size = 256;
    public static byte[] Create()
    {
        float[] height = new float[Size * Size];
        float Noise(float x, float y, int px, int py)
        {
            int ix = (int)MathF.Floor(x), iy = (int)MathF.Floor(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            float Hash(int a, int b)
            {
                uint h = unchecked((uint)((a % px + px) % px) * 1597334677u ^
                    (uint)((b % py + py) % py) * 3812015801u ^ 0x12a78d91u);
                h ^= h >> 16; h *= 2246822519u; h ^= h >> 13;
                return (h & 65535) / 65535f;
            }
            float a = Hash(ix, iy), b = Hash(ix + 1, iy);
            float c = Hash(ix, iy + 1), d = Hash(ix + 1, iy + 1);
            return (a + (b - a) * fx) * (1 - fy) + (c + (d - c) * fx) * fy;
        }
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            float u = x / (float)Size, v = y / (float)Size;
            // Periodic, warped crest lines give normals a coherent ripple direction.
            // Noise varies their spacing and strength rather than forming granular bumps.
            float warp = Noise(u * 8, v * 4, 8, 4) - .5f;
            float envelope = .65f + .35f * Noise(u * 4, v * 4, 4, 4);
            float phase = v * 8 + warp * 1.4f + .12f * MathF.Sin(MathF.Tau * u * 2);
            height[y * Size + x] = .5f
                + envelope * .20f * MathF.Sin(MathF.Tau * phase)
                + .10f * MathF.Sin(MathF.Tau * (v * 16 + u * 4 + warp * 2.1f))
                + .045f * MathF.Sin(MathF.Tau * (v * 32 - u * 8 + warp * 3.3f))
                + .08f * (Noise(u * 8, v * 8, 8, 8) - .5f);
        }
        byte[] pixels = new byte[Size * Size * 4];
        byte Byte(float v) => (byte)Math.Clamp((int)(v * 255 + .5f), 0, 255);
        float H(int x, int y) => height[(y & (Size - 1)) * Size + (x & (Size - 1))];
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            int i = (y * Size + x) * 4;
            pixels[i] = Byte(.5f + (H(x + 1, y) - H(x - 1, y)) * 1.8f);
            pixels[i + 1] = Byte(.5f + (H(x, y + 1) - H(x, y - 1)) * 1.8f);
            pixels[i + 2] = Byte(Noise(x / 32f, y / 32f, 8, 8));
            pixels[i + 3] = Byte(H(x, y));
        }
        return pixels;
    }
}
