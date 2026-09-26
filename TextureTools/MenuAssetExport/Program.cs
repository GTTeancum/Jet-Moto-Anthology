using System.Security.Cryptography;
using System.Text.Json;
using RecompOne.Runtime.Assets;
using RecompOne.Runtime.Cdrom;
using RecompOne.Runtime.Context;
using RecompOne.Runtime.Memory;
using Recompiled;
using R = RecompOne.Runtime.Runtime;

// This authoring process never initializes the game window, sound, or input.
if (args.Length != 5)
    throw new ArgumentException("Usage: MenuAssetExport CUE SOURCE.BS OUTPUT.png WIDTH HEIGHT");
int width = int.Parse(args[3]), height = int.Parse(args[4]);
if (width < 16 || height < 16 || width > 640 || height > 512 || width % 16 != 0 || height % 16 != 0)
    throw new ArgumentException("Dimensions must match the original macroblock-aligned background.");
string output = Path.GetFullPath(args[2]);
if (File.Exists(output) || File.Exists(output + ".json"))
    throw new IOException("Refusing to overwrite an existing extraction.");
using var fs = DiscFs.Open(args[0]);
byte[] boot = fs.ReadFile("SCUS_943.09"), source = fs.ReadFile(args[1]);
if (Convert.ToHexString(SHA256.HashData(boot)).ToLowerInvariant() !=
    "f1ad5aa4a092c9fc2a7f2d6795951a60a4419d02a0a3d0ea2a200fabc7a0ce48")
    throw new InvalidDataException("Unsupported original executable revision.");
if (source.Length < 8 || source.Length > 0x40000)
    throw new InvalidDataException("Invalid BS source length.");
var memory = new PSMemory();
memory.LoadBytes(0x800DD2D0, boot.AsSpan(0x800, 978944).ToArray());
memory.LoadBytes(0x80010000, source);
var cpu = new CpuContext { SP = 0x801FFFF0, A0 = 0 };
// Original DecDCTReset uploads these two tables. Feed them directly to MDEC
// without initializing the game's interrupt callbacks, video or audio devices.
var mdec = R.Mdec!;
foreach (uint table in new uint[] { 0x80159568, 0x801595EC })
{
    uint command = memory.ReadU32(table);
    if (command != (table == 0x80159568 ? 0x40000001u : 0x60000000u))
        throw new InvalidDataException("Unexpected original MDEC table.");
    for (uint i = 0; i <= 32; i++) mdec.Write0(memory.ReadU32(table + i * 4));
}
cpu.A0 = 0x80010000;
cpu.A1 = 0x80060000;
Jet_Moto_main.func_800E2694(cpu, memory);
uint header = memory.ReadU32(0x80060000);
int words = (int)(header & 0xFFFF);
if (words == 0 || words * 4 > 0x70000)
    throw new InvalidDataException("Invalid expanded MDEC stream.");
// Decode at original 15-bit precision so the extracted source agrees with game.
mdec.Write0((header | 0x08000000u) & 0xFDFFFFFFu);
for (int i = 0; i < words; i++) mdec.Write0(memory.ReadU32(0x80060004u + (uint)i * 4));
var pixels = new byte[width * height * 4];
for (int bx = 0; bx < width; bx += 16)
for (int by = 0; by < height; by += 16)
for (int y = 0; y < 16; y++)
for (int x = 0; x < 16; x += 2)
{
    if (mdec.OutEmpty) throw new InvalidDataException("Decoded image is smaller than supplied dimensions.");
    uint pair = mdec.ReadData();
    for (int p = 0; p < 2; p++)
    {
        uint color = (pair >> (16 * p)) & 0xFFFF;
        int at = ((by + y) * width + bx + x + p) * 4;
        pixels[at] = (byte)((color & 31) * 8);
        pixels[at + 1] = (byte)(((color >> 5) & 31) * 8);
        pixels[at + 2] = (byte)(((color >> 10) & 31) * 8);
        pixels[at + 3] = 255;
    }
}
if (!mdec.OutEmpty) throw new InvalidDataException("Decoded image is larger than supplied dimensions.");
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
PngWriter.WriteRgba(output, pixels, width, height);
File.WriteAllText(output + ".json", JsonSerializer.Serialize(new {
    source = args[1], sourceSha256 = Convert.ToHexString(SHA256.HashData(source)),
    width, height, originalPrecision = "RGB555 expanded by 8", decoder = "original VLC + runtime MDEC",
    pngSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(output))),
    status = "original source extraction; not an upscale or visual acceptance"
}, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Extracted {args[1]}: {width}x{height}, {words} MDEC words -> {output}");
