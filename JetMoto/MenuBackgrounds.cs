using System.Security.Cryptography;
using System.Buffers.Binary;
using System.Text.Json;
using RecompOne.Runtime.Assets.Native;
using RecompOne.Runtime.Cdrom;
using RecompOne.Runtime.Context;
using RecompOne.Runtime.Hle;
using RecompOne.Runtime.Host;
using RecompOne.Runtime.Memory;

namespace JetMoto;

// Explicit original file ownership. No VRAM search, screen recognition or global overlay.
public static class MenuBackgrounds
{
    private sealed class Asset(string name, byte[] source, NativeTextureAsset image)
    {
        public readonly string Name = name;
        public readonly byte[] Source = source;
        public readonly NativeTextureAsset Image = image;
        public uint Address;
        public bool Loaded;
        public int Registered = -1;
    }
    private static readonly List<Asset> Assets = [];
    private static string? RequestedRider;
    private static readonly bool Enabled = Environment.GetEnvironmentVariable("JETMOTO_MENU_BACKGROUNDS") == "1";

    public static void Configure(DiscFs disc, string root)
    {
        Assets.Clear();
        RequestedRider = null;
        if (!Enabled) return;
        foreach (var entry in disc.Enumerate().Where(e => !e.IsDir &&
            (e.Path.EndsWith(".BS", StringComparison.OrdinalIgnoreCase) ||
             e.Path.EndsWith(".TIM", StringComparison.OrdinalIgnoreCase))))
        {
            string name = NativeTextures.Normalize(entry.Path);
            string path = Path.Combine(root, "Textures", "Menu4x", Path.ChangeExtension(name, ".png"));
            if (!File.Exists(path) || !File.Exists(path + ".json")) continue;
            try
            {
                using var manifest = JsonDocument.Parse(File.ReadAllText(path + ".json"));
                var meta = manifest.RootElement;
                var original = meta.GetProperty("source");
                byte[] source = disc.ReadFile(entry.Path);
                if (NativeTextures.Normalize(original.GetProperty("source").GetString()!) != name ||
                    !Convert.ToHexString(SHA256.HashData(source)).Equals(original.GetProperty("sourceSha256").GetString(), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Original source checksum mismatch.");
                int w = original.GetProperty("width").GetInt32(), h = original.GetProperty("height").GetInt32();
                if (name.EndsWith(".BS"))
                {
                    if (w != 640 || h != 480) throw new InvalidDataException("Unverified background dimensions.");
                }
                else if (!(IsTrackOverview(name, source) && w == 320 && h == 240) &&
                    !(IsRiderPanel(name, source) && w == 576 && h == 192))
                    throw new InvalidDataException("Unverified standalone menu image.");
                // Verify authored bytes when preparing this menu, rather than
                // reading every full-size PNG before the title can appear.
                Assets.Add(new(name, source, new NativeTextureAsset(name, path, w, h)
                    { ExpectedSha256 = meta.GetProperty("outputSha256").GetString()! }));
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            { Console.WriteLine($"[JetMoto:menu-background] original fallback {name}: {ex.Message}"); }
        }
        Console.WriteLine($"[JetMoto:menu-background] Test candidate catalog: {Assets.Count} original sources.");
    }

    internal static bool IsTrackOverview(string name, ReadOnlySpan<byte> source)
    {
        string[] tracks = ["SWAMP1", "SWAMP2", "SWAMP3", "ISLAND1", "ISLAND2", "ISLAND3", "ALPINE1", "ALPINE2", "ALPINE3", "DARK"];
        bool known = false;
        for (int i = 0; i < tracks.Length; i++)
            known |= name == $"{tracks[i]}/OVERV{i}.TIM" || name == $"{tracks[i]}/OVERV{i}L.TIM";
        return known && source.Length == 20 + 320 * 240 * 2 &&
            BinaryPrimitives.ReadUInt32LittleEndian(source) == 0x10 &&
            BinaryPrimitives.ReadUInt32LittleEndian(source[4..]) == 2 &&
            BinaryPrimitives.ReadUInt32LittleEndian(source[8..]) == 12 + 320 * 240 * 2 &&
            BinaryPrimitives.ReadUInt16LittleEndian(source[16..]) == 320 &&
            BinaryPrimitives.ReadUInt16LittleEndian(source[18..]) == 240;
    }

    internal static bool IsRiderPanel(string name, ReadOnlySpan<byte> source)
    {
        bool known = false;
        for (int i = 0; i < 20; i++) known |= name == $"NAVIGATE/RIDER{i:D2}.TIM";
        return known && source.Length == 20 + 576 * 192 * 2 &&
            BinaryPrimitives.ReadUInt32LittleEndian(source) == 0x10 &&
            BinaryPrimitives.ReadUInt32LittleEndian(source[4..]) == 2 &&
            BinaryPrimitives.ReadUInt32LittleEndian(source[8..]) == 12 + 576 * 192 * 2 &&
            BinaryPrimitives.ReadUInt16LittleEndian(source[16..]) == 576 &&
            BinaryPrimitives.ReadUInt16LittleEndian(source[18..]) == 192;
    }

    public static void RequestRider(CpuContext cpu)
    {
        if (!Enabled) return;
        RequestedRider = cpu.A1 < 20 ? $"NAVIGATE/RIDER{cpu.A1:D2}.TIM" : null;
        // This original asynchronous loader reuses the BS decode workspace.
        Invalidate(0x800447D8, 20 + 576 * 192 * 2);
        if (Assets.FirstOrDefault(a => a.Name == RequestedRider) is {} asset) Prepare(asset);
        Console.WriteLine($"[JetMoto:menu-background] rider request source={RequestedRider ?? "unsupported"} frame={RecompOne.Runtime.Interrupts.VBlankCount}");
    }

    public static void Invalidate(uint address, uint length)
    {
        uint start = address & 0x1FFFFFFF;
        foreach (var asset in Assets)
            if (asset.Loaded && start < (long)asset.Address + asset.Source.Length && (long)start + length > asset.Address)
                asset.Loaded = false;
    }

    public static void Loaded(PSMemory memory, string name, uint address, uint result)
    {
        if (!Enabled || result == 0) return;
        uint start = address & 0x1FFFFFFF;
        foreach (var asset in Assets.Where(a => a.Name == name))
        {
            asset.Loaded = (long)start + asset.Source.Length <= memory.Ram.Length &&
                memory.Ram.Slice((int)start, asset.Source.Length).SequenceEqual(asset.Source);
            asset.Address = start;
            if (asset.Loaded) Prepare(asset);
        }
    }

    public readonly struct DrawScope : IDisposable
    {
        private readonly Action? _draw;
        internal DrawScope(Action draw) { _draw = draw; }
        public void Dispose() => _draw?.Invoke();
    }

    public static DrawScope Begin(CpuContext cpu, PSMemory memory)
    {
        if (!Enabled || !GpuHle.Active || GpuHle.Backend is not { Ready: true } backend) return default;
        uint start = cpu.A0 & 0x1FFFFFFF;
        var asset = Assets.FirstOrDefault(a => a.Loaded && a.Address == start);
        if (asset == null || cpu.A1 != 640 || cpu.A2 != 480 || cpu.A3 != 0 || memory.ReadU32(cpu.SP + 16) != 0)
            return default;
        // Revalidate ownership at the original decoder boundary, not at presentation.
        if (!memory.Ram.Slice((int)start, asset.Source.Length).SequenceEqual(asset.Source)) return default;
        return Draw(asset, backend, 640, 480);
    }

    public static DrawScope BeginTrackUpload(CpuContext cpu, PSMemory memory)
    {
        if (!Enabled || cpu.RA != 0x8014D368 || !GpuHle.Active || GpuHle.Backend is not { Ready: true } backend)
            return default;
        uint data = cpu.A1 & 0x1FFFFFFF, rect = cpu.A0 & 0x1FFFFFFF;
        var asset = Assets.FirstOrDefault(a => a.Loaded && a.Address + 20 == data && a.Name.EndsWith(".TIM"));
        if (asset == null || (long)rect + 8 > memory.Ram.Length ||
            memory.ReadU16(rect) != 0 || memory.ReadU16(rect + 2) != 0 ||
            memory.ReadU16(rect + 4) != 320 || memory.ReadU16(rect + 6) != 240 ||
            !memory.Ram.Slice((int)asset.Address, asset.Source.Length).SequenceEqual(asset.Source))
            return default;
        return Draw(asset, backend, 320, 240);
    }

    public static DrawScope BeginRiderUpload(CpuContext cpu, PSMemory memory)
    {
        if (!Enabled || RequestedRider == null || cpu.RA != 0x8014D368 ||
            !GpuHle.Active || GpuHle.Backend is not { Ready: true } backend) return default;
        uint data = cpu.A1 & 0x1FFFFFFF, rect = cpu.A0 & 0x1FFFFFFF;
        if (data != 0x447EC || (long)rect + 8 > memory.Ram.Length ||
            memory.ReadU16(rect) != 39 || memory.ReadU16(rect + 2) != 69 ||
            memory.ReadU16(rect + 4) != 576 || memory.ReadU16(rect + 6) != 192) return default;
        var asset = Assets.FirstOrDefault(a => a.Name == RequestedRider);
        RequestedRider = null;
        if (asset == null || !memory.Ram.Slice(0x447D8, asset.Source.Length).SequenceEqual(asset.Source)) return default;
        return Draw(asset, backend, 576, 192, 39, 69, 640, 480);
    }

    private static DrawScope Draw(Asset asset, IGpuBackend backend, int width, int height,
        int x = 0, int y = 0, int clipWidth = 0, int clipHeight = 0)
    {
        if (asset.Registered < 0 && !Prepare(asset)) return default;
        return new DrawScope(() =>
        {
            backend.SetDrawEnv(new HleDrawEnv { ClipX0 = 0, ClipY0 = 0,
                ClipX1 = (clipWidth == 0 ? width : clipWidth) - 1,
                ClipY1 = (clipHeight == 0 ? height : clipHeight) - 1 });
            var flags = new PrimFlags { Textured = true, UseImage = true, Image = asset.Registered };
            var a = new HleVertex { X = x, Y = y, U = 0, V = 0, R = 128, G = 128, B = 128, Z = 1 };
            var b = a; b.X = x + width; b.U = 1;
            var c = a; c.Y = y + height; c.V = 1;
            var d = b; d.Y = y + height; d.V = 1;
            backend.DrawTri(a, b, c, flags);
            backend.DrawTri(b, d, c, flags);
            Console.WriteLine($"[JetMoto:menu-background] submitted original-owned candidate {asset.Name}");
        });
    }

    private static bool Prepare(Asset asset)
    {
        if (GpuHle.Backend is not { Ready: true } backend) return false;
        bool ready = false;
        GpuJobs.Run(() =>
        {
            if (asset.Registered < 0) asset.Registered = backend.RegisterNativeImage(asset.Image);
            ready = asset.Registered >= 0 && backend.PrepareNativeTexture(asset.Image);
        });
        return ready;
    }
}
