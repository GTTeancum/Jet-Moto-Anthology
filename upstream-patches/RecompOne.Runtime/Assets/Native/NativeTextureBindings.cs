using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using RecompOne.Runtime.Memory;
using StbImageSharp;

namespace RecompOne.Runtime.Assets.Native;

/// <summary>A replacement identified by an original source asset, never by VRAM contents.</summary>
public sealed class NativeTextureAsset(string key, string file, int sourceWidth, int sourceHeight, bool allowEffectCoverage = false, bool waterSpray = false)
{
    public string Key { get; } = key;
    public string FilePath { get; } = file;
    public int SourceWidth { get; } = sourceWidth;
    public int SourceHeight { get; } = sourceHeight;
    public bool AllowsEffectCoverage { get; } = allowEffectCoverage;
    public bool WaterSpray { get; } = allowEffectCoverage && waterSpray;
    public bool CoverageLoaded => _texture?.Coverage == true;
    public long BoundCommands, ResolvedCommands;
    private bool _attempted;
    private ReplacementTexture? _texture;
    public ReplacementTexture? GetTexture()
    {
        if (_attempted) return _texture;
        _attempted = true;
        if (!File.Exists(FilePath)) return null;
        try
        {
            if (new FileInfo(FilePath).Length > 64 * 1024 * 1024) throw new InvalidDataException("PNG exceeds the 64 MiB file limit.");
            var data = File.ReadAllBytes(FilePath);
            // Bound allocation before handing a file to the image decoder. Only PNG is accepted.
            if (data.Length < 33 || data.Length > 64 * 1024 * 1024 ||
                !data.AsSpan(0, 8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10}) ||
                !data.AsSpan(12,4).SequenceEqual("IHDR"u8)) throw new InvalidDataException("Expected a PNG image.");
            uint w = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(16,4));
            uint h = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(20,4));
            if (SourceWidth < 1 || SourceHeight < 1 || w != SourceWidth * 4 || h != SourceHeight * 4 ||
                w > 4096 || h > 4096 || (long)w*h > 16*1024*1024)
                throw new InvalidDataException($"Expected exactly {SourceWidth*4} x {SourceHeight*4} (4x). Got {w} x {h}.");
            var image = ImageResult.FromMemory(data, ColorComponents.RedGreenBlueAlpha);
            // Smooth alpha is opt-in for a verified original particle identity AND
            // an adjacent versioned material file. Old packs/overrides keep their
            // categorical PS1 alpha; unrelated texture IDs cannot enable this mode.
            bool coverage = false;
            string metadataPath = FilePath + ".material.json";
            if (File.Exists(metadataPath))
            {
                if (!AllowsEffectCoverage) throw new InvalidDataException("Effect coverage is not allowed for this source asset.");
                if (new FileInfo(metadataPath).Length > 4096) throw new InvalidDataException("Effect material metadata is too large.");
                using var metadata = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(metadataPath));
                var m = metadata.RootElement;
                if (m.GetProperty("format").GetString() != "jetmoto-effect-material-1" ||
                    m.GetProperty("sourceKey").GetString() != Key ||
                    m.GetProperty("sourceWidth").GetInt32() != SourceWidth ||
                    m.GetProperty("sourceHeight").GetInt32() != SourceHeight ||
                    m.GetProperty("alphaMode").GetString() != "coverage")
                    throw new InvalidDataException("Effect material identity, dimensions or format mismatch.");
                coverage = true;
            }
            if (!coverage)
                for (int i = 3; i < image.Data.Length; i += 4)
                    if (image.Data[i] is not (0 or 128 or 255))
                        throw new InvalidDataException("Alpha must be 0 (transparent), 128 (STP), or 255 (opaque).");
            _texture = new ReplacementTexture { Width=image.Width, Height=image.Height,
                ScaleX=4, ScaleY=4, Rgba=image.Data, Nearest=!coverage, Coverage=coverage };
            System.Threading.Interlocked.Increment(ref NativeTextureBindings.ImagesLoaded);
            if (coverage) {
                System.Threading.Interlocked.Increment(ref NativeTextureBindings.EffectImagesLoaded);
                Console.WriteLine($"[JetMoto:effects] Original asset {Key}: 4x replacement art, smooth coverage, original animation.");
            }
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        { Console.WriteLine($"[JetMoto:native-textures] Original fallback for {Key}: {e.Message}"); }
        return _texture;
    }
}

/// <summary>Immutable native material. It can safely outlive the emulated command buffer.</summary>
public sealed record NativeTextureMaterial(NativeTextureAsset Asset, int U0, int V0, int Width, int Height,
    ushort TPage, ushort Clut)
{
    public bool Accepts(ushort page, ushort clut, int uMin, int vMin, int uMax, int vMax,
        int andX, int andY, int orX, int orY)
    {
        // ABR and dither changes do not change the source image. Palette/depth/page changes do.
        if ((page & 0x19f) != (TPage & 0x19f) || (((page >> 7)&3) < 2 && clut != Clut)) return false;
        if (uMin < 0 || vMin < 0 || uMax > 255 || vMax > 255 || uMin > uMax || vMin > vMax) return false;
        static bool Inside(int lo,int hi,int mask,int bits,int origin,int size)
        {
            if(mask==255 && bits==0) return lo>=origin && hi<origin+size;
            if(origin<=0 && origin+size>=256) return true;
            for(int v=lo;v<=hi;v++) if(((v&mask)|bits)<origin || ((v&mask)|bits)>=origin+size)return false;
            return true;
        }
        return Inside(uMin,uMax,andX,orX,U0,Width) && Inside(vMin,vMax,andY,orY,V0,Height);
    }
}

/// <summary>
/// Provenance only. No pixel reads, hashes, VRAM rectangles or framebuffer inspection.
/// A game-specific native renderer binds an original material to the command it emitted.
/// Every payload byte write invalidates that command; ordering-table headers are not tagged.
/// </summary>
public static class NativeTextureBindings
{
    private static bool _originalAssetsOnly;
    public static bool OriginalAssetsOnly
    {
        get => _originalAssetsOnly;
        set { if (_originalAssetsOnly != value) Array.Clear(_words); _originalAssetsOnly=value; }
    }
    public static long ImagesLoaded, CommandsBound, CommandsResolved, RejectedDraws, EffectImagesLoaded, EffectCommandsResolved;
    private sealed class Token(uint start, NativeTextureMaterial material)
    { public readonly uint Start=start; public readonly NativeTextureMaterial Material=material; public bool Valid=true; }
    private static Token?[] _words=[];
    private static uint _mask, _hostSize;
    public static void Init(uint size)
    {
        if (size < 0x200000 || size > 0x800000 || (size & (size-1)) != 0) throw new ArgumentOutOfRangeException(nameof(size));
        _mask=size-1; _hostSize=0; _words=new Token?[size/4];
    }
    public static void ConfigureHostScratch(uint size)
    {
        if(size==0 || size>MemoryMap.HostScratchMaxSize || (size&3)!=0)throw new ArgumentOutOfRangeException(nameof(size));
        if(_words.Length==0)throw new InvalidOperationException("Native provenance is not initialized.");
        if(_hostSize==size)return;
        if(_hostSize!=0)throw new InvalidOperationException("Native workspace cannot be silently resized.");
        Array.Resize(ref _words,checked((int)((_mask+1+size)/4)));_hostSize=size;
    }
    private static bool Address(uint address, out uint offset, out uint remaining)
    {
        uint p=address&0x1fffffffu; offset=remaining=0;
        if(_words.Length==0)return false;
        if(p<MemoryMap.RamWindow){offset=p&_mask;remaining=_mask+1-offset;return true;}
        uint h=p-MemoryMap.HostScratchBase;
        if(h>=_hostSize)return false;
        offset=_mask+1+h;remaining=_hostSize-h;return true;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Invalidate(uint address, int bytes)
    {
        if (!OriginalAssetsOnly || bytes<=0) return;
        for(int i=0;i<bytes;)
        {
            if(!Address(address+(uint)i,out uint off,out _))return;
            int slot=(int)(off>>2);
            var t=_words[slot];if(t!=null){t.Valid=false;_words[slot]=null;}
            i+=Math.Min(bytes-i,4-(int)(off&3));
        }
    }
    public static bool Bind(uint command, int wordCount, NativeTextureMaterial material)
    {
        if (!OriginalAssetsOnly || (command&3)!=0 || wordCount < 1 || wordCount>16 ||
            !Address(command,out uint off,out uint remaining) || (uint)wordCount*4 > remaining) return false;
        var token=new Token(off,material);
        for (int i=0;i<wordCount;i++)
        {
            int slot=(int)(off>>2)+i;
            if (_words[slot] is {} old) old.Valid=false;
            _words[slot]=token;
        }
        System.Threading.Interlocked.Increment(ref CommandsBound);
        System.Threading.Interlocked.Increment(ref material.Asset.BoundCommands);
        return true;
    }
    public static NativeTextureMaterial? Resolve(uint command)
    {
        if (!OriginalAssetsOnly || (command&3)!=0 || !Address(command,out uint off,out _)) return null;
        var token=_words[off>>2];
        if (token is not {Valid:true} || token.Start!=off) return null;
        System.Threading.Interlocked.Increment(ref CommandsResolved);
        System.Threading.Interlocked.Increment(ref token.Material.Asset.ResolvedCommands);
        if (token.Material.Asset.CoverageLoaded) System.Threading.Interlocked.Increment(ref EffectCommandsResolved);
        return token.Material;
    }
    public static string Summary => $"native[bound={CommandsBound},resolved={CommandsResolved},png={ImagesLoaded},draw-fallback={RejectedDraws},effects={EffectImagesLoaded},effect-resolved={EffectCommandsResolved}]";
}
