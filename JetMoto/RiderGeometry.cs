using System.Buffers.Binary;
using System.Numerics;
using RecompOne.Runtime;
using RecompOne.Runtime.Assets.Native;

namespace JetMoto;

internal static class RiderGeometry
{
    private sealed record Face(int Shift, int RiderId, Vector3[] Vertices);
    private static readonly Dictionary<string, Dictionary<int, Face>> Models = [];
    public static long Faces;

    public static void Configure(NativeTextures.Model model)
    {
        byte[] b = model.Original;
        uint U(int o) => BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(o, 4));
        int S(int o) => BinaryPrimitives.ReadInt16LittleEndian(b.AsSpan(o, 2));
        int P(int o) => checked((int)(U(o) - U(12)));
        Dictionary<int, Face> faces = [];
        HashSet<(int, int, int)> visited = [];
        void Walk(int o, int shift, int riderId)
        {
            if (o < 0 || o + 24 > b.Length || shift < 0 || shift > 12)
                throw new InvalidDataException("Rider geometry node outside original model");
            if (!visited.Add((o, shift, riderId))) return;
            if (visited.Count > 50000) throw new InvalidDataException("Rider hierarchy too large");
            int type = b[o], count = 0, start = 0;
            if (type == 9 && S(o + 2) is >= 200 and < 220 && S(o + 4) == 1000)
            {
                Walk(P(o + 16), shift, S(o + 2));
                return;
            }
            if (type == 0)
            {
                if (riderId < 200) return;
                int vertices = P(o + 4), polygon = P(o + 12);
                for (uint i = 0; i < U(o + 16); i++)
                {
                    int n = (int)(U(polygon + 12) >> 16) & 7;
                    if (n is 3 or 4)
                    {
                        Vector3[] points = new Vector3[n];
                        for (int j = 0; j < n; j++)
                        {
                            int v = checked(vertices + S(polygon + 4 + j * 2) * 8);
                            points[j] = new(S(v), S(v + 2), S(v + 4));
                        }
                        // Shared geometry cannot establish an actor identity by
                        // address alone. Keep its shadows but exclude it from emitters.
                        int owner = faces.TryGetValue(polygon, out var previous) && previous.RiderId != riderId
                            ? -1 : riderId;
                        faces[polygon] = new(shift, owner, points);
                    }
                    int size = b[polygon + 2] * 4;
                    if (size < 16) throw new InvalidDataException("Rider polygon size");
                    polygon = checked(polygon + size);
                }
                return;
            }
            switch (type)
            {
                case 1: count = b[o + 20]; start = 24; break;
                case 2: count = checked((int)U(o + 16)); start = 20; break;
                case 3: count = b[o + 11]; start = 32; break;
                case 4: count = b[o + 18]; start = 20; break;
                case 5: count = b[o + 38]; start = 40; break;
                case 9: count = b[o + 6]; start = 16; break;
                case 11: count = 2; start = 8; break;
                case 12: count = S(o + 6); start = 8; shift = S(o + 4); break;
            }
            if (count < 0 || count > 4096) throw new InvalidDataException("Rider child count");
            for (int i = 0; i < count; i++)
            {
                int child = P(o + start + i * 4);
                if (type == 2) child = P(child + 8);
                Walk(child, shift, riderId);
            }
        }
        try
        {
            for (uint i = 0; i < U(24); i++) Walk(P(28 + (int)i * 4), (int)U(16), -1);
            Models[model.Name] = faces;
            if (faces.Count > 0) Console.WriteLine($"[JetMoto:rider-geometry] {model.Name}: {faces.Count} original highest-detail faces.");
            if (faces.Count > 0) Console.WriteLine($"[JetMoto:rider-ownership] {model.Name}: " +
                string.Join(",", faces.Values.GroupBy(f => f.RiderId).OrderBy(g => g.Key).Select(g => $"{g.Key}:{g.Count()}")));
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Console.WriteLine($"[JetMoto:rider-geometry] Original fallback for {model.Name}: {e.Message}");
        }
    }

    public static WorldSurface? Resolve(NativeTextures.Model model, int offset, WorldCamera? camera, WorldBasis rotation)
    {
        if (camera == null || !Models.TryGetValue(model.Name, out var faces) || !faces.TryGetValue(offset, out var face)) return null;
        Vector3 translation = new((int)Gte.ReadControl(5), (int)Gte.ReadControl(6), (int)Gte.ReadControl(7));
        float scale = MathF.Pow(2, face.Shift);
        var inverse = camera.Rotation.Inverse;
        Span<Vector3> points = stackalloc Vector3[4];
        for (int i = 0; i < face.Vertices.Length; i++)
            points[i] = inverse.Apply((rotation.Apply(face.Vertices[i]) + translation) / scale - camera.Translation);
        var normal = -Vector3.Cross(points[1] - points[0], points[2] - points[0]);
        if (!float.IsFinite(normal.LengthSquared()) || normal.LengthSquared() < 1e-10f) return null;
        camera.Casters.Add(new(points[0], points[1], points[2]));
        if (face.Vertices.Length == 4) camera.Casters.Add(new(points[1], points[3], points[2]));
        if (face.RiderId is >= 200 and < 220)
        {
            if (!camera.Riders.TryGetValue(face.RiderId, out var rider))
                camera.Riders[face.RiderId] = rider = new RiderBounds();
            for (int i = 0; i < face.Vertices.Length; i++) rider.Include(points[i]);
        }
        Faces++;
        return new(camera, points[0], normal, 3, (int)Gte.ReadControl(24) / 65536f,
            (int)Gte.ReadControl(25) / 65536f, (ushort)Gte.ReadControl(26),riderId:face.RiderId);
    }
}
