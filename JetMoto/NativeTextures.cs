using System.Buffers.Binary;
using RecompOne.Runtime.Assets.Native;
using RecompOne.Runtime.Cdrom;
using RecompOne.Runtime.Context;
using RecompOne.Runtime.Memory;

namespace JetMoto;

/// <summary>
/// Jet Moto's native asset linker. Identity is DMD filename + primitive offset ->
/// its sibling TMS bank + original record ordinal/ID. Placement metadata is read
/// from those ORIGINAL files, never discovered by hashing or searching live VRAM.
/// </summary>
public static class NativeTextures
{
    public sealed record ImageRecord(int Ordinal, uint Id, int Depth, int X, int Y, int Width, int Height,
        int ClutX, int ClutY, int ClutWidth, int ClutHeight, NativeTextureAsset Asset);
    public sealed class Model(string name, byte[] original, ImageRecord[] images)
    {
        public readonly string Name=name;
        public readonly byte[] Original=original;
        public readonly ImageRecord[] Images=images;
        public readonly Dictionary<(int,int,int),ImageRecord[]> Groups=images.GroupBy(i=>(i.Depth,i.Depth<2?i.ClutX:0,i.Depth<2?i.ClutY:0)).ToDictionary(g=>g.Key,g=>g.ToArray());
        public readonly Dictionary<int, NativeTextureMaterial?> Materials=[];
        public readonly Dictionary<int,HashSet<int>> RiderHierarchy=[];
        public uint Destination;
    }
    private static readonly Dictionary<string,Model> Models=new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<Model> Active=[];
    private sealed record HudSource(Model Owner, NativeTextureMaterial Material, byte[] Descriptor);
    private static readonly Dictionary<uint,HudSource> HudSources=[];
    private static readonly Dictionary<NativeTextureAsset,NativeTextureAsset> HudAssets=[];
    private static readonly HashSet<string> HudDiagnostics=[];
    public static void RememberHudSource(PSMemory memory,uint source,uint descriptor)
    {
        uint p=source&0x1fffffff, d=descriptor&0x1fffffff;
        HudSources.Remove(d);
        foreach(var model in Active)
        {
            long offset=(long)p-(model.Destination&0x1fffffff);
            if(offset<0 || offset>=model.Original.Length)continue;
            var material=ResolveSource(model,(int)offset);
            if(material==null)return;
            if(!HudAssets.TryGetValue(material.Asset,out var image))
            {
                var original=material.Asset;
                image=new NativeTextureAsset(original.Key+"/hud",original.FilePath,original.SourceWidth,original.SourceHeight,smoothCutout:true);
                HudAssets.Add(original,image);
            }
            HudSources[d]=new(model,material with {Asset=image},memory.Ram.Slice((int)d,12).ToArray());
            if(Environment.GetEnvironmentVariable("JETMOTO_TRACE_HUD")=="1")
                Console.WriteLine($"[JetMoto:hud-source] descriptor={d:X8} source={model.Name}@{offset:X} asset={material.Asset.Key}");
            return;
        }
    }
    public sealed class HudScope : IDisposable
    {
        private readonly PSMemory _memory;
        private readonly uint _owner,_head,_previous;
        public HudScope(PSMemory memory,uint owner,uint list)
        {
            _memory=memory;_owner=owner&0x1fffffff;
            _head=memory.ReadU32(list);_previous=memory.ReadU32(_head)&0xffffff;
        }
        public void Dispose()
        {
            if(Environment.GetEnvironmentVariable("JETMOTO_HUD_UPSCALE")=="0")return;
            var materials=HudSources.Where(p=>p.Key>=_owner && p.Key<_owner+0x1C94 && Active.Contains(p.Value.Owner)
                    && _memory.Ram.Slice((int)p.Key,12).SequenceEqual(p.Value.Descriptor))
                .Select(p=>p.Value.Material).Distinct().ToArray();
            uint node=_memory.ReadU32(_head)&0xffffff;
            ushort page=0;bool hasPage=false;
            for(int count=0;node!=_previous && node!=0xffffff && count<256;count++)
            {
                if(!_memory.IsWorkMemoryRange(node,4))return;
                uint header=_memory.ReadU32(node);int words=(int)(header>>24);
                uint command=node+4,end=command+(uint)words*4;
                if(!_memory.IsWorkMemoryRange(command,words*4L))return;
                while(command<end)
                {
                    byte op=_memory.ReadU8(command+3);int length=CommandLength(op);
                    if(length<1 || command+length*4>end)return;
                    if(op==0xe1){page=(ushort)_memory.ReadU32(command);hasPage=true;}
                    if((op&0xfc)==0x64 && hasPage)
                    {
                        uint uv=_memory.ReadU32(command+8),size=_memory.ReadU32(command+12);
                        int u=(byte)uv,v=(byte)(uv>>8),w=(ushort)size,h=(ushort)(size>>16);
                        ushort clut=(ushort)(uv>>16);
                        var matches=materials.Where(m=>w>0 && h>0 && m.Accepts(page,clut,u,v,u+w-1,v+h-1,255,255,0,0)).ToArray();
                        if(matches.Length==1)NativeTextureBindings.Bind(command,length,matches[0]);
                        else if(Environment.GetEnvironmentVariable("JETMOTO_TRACE_HUD")=="1")
                        {
                            string diagnostic=$"owner={_owner:X} page={page:X} clut={clut:X} uv={u},{v},{w},{h} sources={materials.Length} matches={matches.Length}";
                            if(HudDiagnostics.Add(diagnostic))Console.WriteLine("[JetMoto:hud-unmapped] "+diagnostic);
                        }
                    }
                    command+=(uint)length*4;
                }
                node=header&0xffffff;
            }
        }
    }
    public static HudScope BeginHud(PSMemory memory,uint owner,uint list)=>new(memory,owner,list);
    private static readonly Dictionary<string,uint> FileSizes=new(StringComparer.OrdinalIgnoreCase);
    private static readonly bool FilterMenuArtwork=Environment.GetEnvironmentVariable("JETMOTO_MENU_FILTER")!="0";
    private static long _primitives,_linked,_unknown,_packets,_badPackets,_deferred,_subdivided;
    public static string Summary => $"{NativeTextureBindings.Summary} material[seen={_primitives},linked={_linked},unmapped={_unknown},packets={_packets},bad={_badPackets},deferred={_deferred},subdivided={_subdivided}]";
    private static uint U32(byte[] b,int o) => BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(o,4));
    private static ushort U16(byte[] b,int o) => BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(o,2));
    public static string Normalize(string s)
    {
        s=s.Replace('\\','/').Trim(); int colon=s.IndexOf(':'); if (colon>=0) s=s[(colon+1)..];
        return s.TrimStart('/').Split(';')[0].ToUpperInvariant();
    }
    public static void Configure(string cue, string root)
    {
        NativeTextureBindings.OriginalAssetsOnly=true;
        RecompOne.Runtime.Assets.Textures.TextureResolver.Enabled=false;
        using var fingerprintStream=typeof(NativeTextures).Assembly.GetManifestResourceStream("JetMoto.NativeBanks.json");
        if(fingerprintStream==null) throw new InvalidDataException("Missing original-bank fingerprint catalog.");
        var fingerprints=System.Text.Json.JsonSerializer.Deserialize<Dictionary<string,string>>(fingerprintStream)!;
        // The old VRAM matcher/dumper remains unavailable even if a saved generic setting enables it.
        Models.Clear(); Active.Clear(); FileSizes.Clear(); HudSources.Clear(); HudAssets.Clear(); HudDiagnostics.Clear();
        MenuSourceTrace.Reset();
        using var fs=DiscFs.Open(cue);
        MenuBackgrounds.Configure(fs,root);
        foreach(var entry in fs.Enumerate().Where(e=>!e.IsDir)) FileSizes[Normalize(entry.Path)]=entry.Size;
        var paths=FileSizes.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        int count=0;
        foreach (var name in paths.Where(p=>p.EndsWith(".DMD")).OrderBy(p=>p))
        {
            string bank=Path.ChangeExtension(name,".TMS").Replace('\\','/');
            if (!paths.Contains(bank)) continue;
            try
            {
                byte[] model=fs.ReadFile(name), data=fs.ReadFile(bank);
                if (model.Length<32 || U32(model,0)!=0x50535844 || U32(model,4)!=0x43 || U32(model,8)!=U32(data,8))
                    throw new InvalidDataException("DMD/TMS format or bank timestamp mismatch.");
                // This SHA validates the ORIGINAL file revision, not texture identity or VRAM pixels.
                string digest=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(data));
                if(!fingerprints.TryGetValue(bank,out var expected) || !digest.Equals(expected,StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Original TMS differs from the bundled test-pack revision.");
                var images=ParseBank(bank,data,root);
                Models[name]=new Model(name,model,images); WorldLighting.Configure(Models[name],root); count+=images.Length;
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            { Console.WriteLine($"[JetMoto:native-textures] Original fallback for {name}: {e.Message}"); }
        }
        Console.WriteLine($"[JetMoto:native-textures] Linked catalog: {Models.Count} original DMD/TMS pairs, {count} texture records; 4x PNGs, no VRAM matcher.");
        Console.WriteLine("[JetMoto:native-textures] Packs: Textures/Overrides (optional) then Textures/Native4x-Test. Unsupported or missing materials retain the original image.");
    }
    // Verified original track-bank entries; shadows, rider art and scenery cannot
    // accidentally acquire coverage-alpha semantics from a stray sidecar file.
    public static bool IsEffectTexture(string bank, int ordinal, uint id) => (Normalize(bank), ordinal, id) switch
    {
        ("ISLAND1/ISLAND1.TMS",62,0xEF77) or ("ISLAND2/ISLAND2.TMS",79,0xEF77) or
        ("ISLAND3/ISLAND3.TMS",53,0xEF77) or ("SWAMP1/SWAMP1.TMS",73,0xEF77) or
        ("SWAMP2/SWAMP2.TMS",65,0xEF77) or ("SWAMP3/SWAMP3.TMS",67,0xEF77) or
        ("ISLAND1/ISLAND1.TMS",30,0xC6A1) or ("ISLAND3/ISLAND3.TMS",27,0xC6A1) or
        ("ISLAND2/ISLAND2.TMS",78,0xC65D) or
        ("SWAMP1/SWAMP1.TMS",72,0x8F87) or ("SWAMP2/SWAMP2.TMS",64,0x8F87) or
        ("SWAMP3/SWAMP3.TMS",66,0x8F87) or
        ("ALPINE1/ALPINE1.TMS",49,0x3D31) or ("ALPINE2/ALPINE2.TMS",39,0x3D31) or
        ("ALPINE3/ALPINE3.TMS",42,0x3D31) => true,
        _ => false
    };

    // Dedicated menu banks only. PICKRIDE and PRIZE also contain model artwork
    // and need per-material review before enabling menu-specific sampling.
    public static bool IsMenuArtworkBank(string bank) => Normalize(bank) is
        "STARTUP/TITLE.TMS" or "NAVIGATE/RACETYPE.TMS" or "NAVIGATE/SCORING.TMS" or
        "MISC/OPTIONS.TMS" or "MISC/SOUND.TMS" or "MISC/LOADGAME.TMS" or "MISC/SAVEGAME.TMS" or
        "PICKTRAC/TRACKS0.TMS" or "PICKTRAC/TRACKS1.TMS" or "PICKTRAC/TRACKS2.TMS" or "PICKTRAC/TRACKS3.TMS" or
        "STANDING/OVERALL.TMS" or "STANDING/ALPINE1/STANDING.TMS" or "STANDING/ALPINE2/STANDING.TMS" or
        "STANDING/ALPINE3/STANDING.TMS" or "STANDING/DARK/STANDING.TMS" or
        "STANDING/ISLAND1/STANDING.TMS" or "STANDING/ISLAND2/STANDING.TMS" or "STANDING/ISLAND3/STANDING.TMS" or
        "STANDING/SWAMP1/STANDING.TMS" or "STANDING/SWAMP2/STANDING.TMS" or "STANDING/SWAMP3/STANDING.TMS";

    public static ImageRecord[] ParseBank(string bank, byte[] data, string root)
    {
        bank=Normalize(bank);
        if (bank.Length==0 || bank.Split('/').Any(p=>p is "" or "." or ".." || p.Any(c=>c<32 || ":<>|?*".Contains(c))))
            throw new InvalidDataException("Unsafe native bank filename.");
        if (data.Length<16 || U32(data,0)!=0x50535854 || U32(data,4)!=0x43) throw new InvalidDataException("Not Jet Moto TMS v0x43.");
        int count=checked((int)U32(data,12));
        if (count<0 || count>4096 || 16L+4L*count>data.Length) throw new InvalidDataException("Invalid TMS count.");
        int offset=16+count*4; var images=new List<ImageRecord>();
        for (int i=0;i<count;i++)
        {
            if (offset>data.Length-12) throw new InvalidDataException("Truncated TIM record.");
            int size=checked((int)U32(data,offset)), start=offset+4, end=checked(start+size);
            if (size<20 || end>data.Length || U32(data,start)!=16) throw new InvalidDataException("Invalid TIM length/magic.");
            uint id=U32(data,16+i*4), flags=U32(data,start+4); int depth=(int)(flags&3), o=start+8;
            int cx=0,cy=0,cw=0,ch=0;
            if ((flags&8)!=0)
            {
                if (o>end-12) throw new InvalidDataException("Truncated CLUT.");
                int len=checked((int)U32(data,o)); cx=U16(data,o+4); cy=U16(data,o+6); cw=U16(data,o+8); ch=U16(data,o+10);
                if (len!=12L+cw*(long)ch*2 || o+(long)len>end) throw new InvalidDataException("Invalid CLUT size.");
                o+=len;
            }
            if (o>end-12) throw new InvalidDataException("Truncated TIM pixels.");
            int pixels=checked((int)U32(data,o)), x=U16(data,o+4),y=U16(data,o+6),words=U16(data,o+8),h=U16(data,o+10);
            if (pixels!=12L+words*(long)h*2 || o+(long)pixels!=end) throw new InvalidDataException("Invalid TIM pixel size.");
            int w=depth switch {0=>words*4,1=>words*2,2=>words,_=>0};
            // This disc has one complete CLUT per indexed TIM. Other layouts stay original.
            bool supported=(depth is 0 or 1) ? ch==1 && cw==(depth==0?16:256) : depth==2 && (flags&8)==0;
            if (supported && w>0 && h>0 && w<=1024 && h<=1024)
            {
                string rel=Path.ChangeExtension(Normalize(bank),null)+$"/{i:D4}-{id:X8}.png";
                string user=Path.Combine(root,"Textures","Overrides",rel);
                string shipped=Path.Combine(root,"Textures","Native4x-Test",rel);
                // Filename/ID is the lookup. Original pixel hashes are never used as identifiers.
                bool effect = IsEffectTexture(bank,i,id);
                bool buoy=(bank,i,id) is ("ISLAND1/ISLAND1.TMS",45,0xC935) or ("ISLAND1/ISLAND1.TMS",46,0xC94F)
                    or ("ISLAND2/ISLAND2.TMS",52,0xC935) or ("ISLAND2/ISLAND2.TMS",53,0xC94F)
                    or ("ISLAND3/ISLAND3.TMS",37,0xC935) or ("ISLAND3/ISLAND3.TMS",38,0xC94F)
                    or ("SWAMP1/SWAMP1.TMS",36,0xAD7A) or ("SWAMP1/SWAMP1.TMS",37,0xAD7B)
                    or ("SWAMP2/SWAMP2.TMS",40,0xAD7B) or ("SWAMP2/SWAMP2.TMS",41,0xAD7A)
                    or ("SWAMP3/SWAMP3.TMS",50,0xAD7A) or ("SWAMP3/SWAMP3.TMS",51,0xAD7B);
                bool dial=bank=="PICKTRAC/TRACKS0.TMS" && (i,id) is
                    (2,0x1DA8) or (3,0x1D9F) or (4,0x1D8F) or (5,0x1D8E) or
                    (6,0x1D89) or (7,0x1D86) or (8,0x1D83) or (9,0x1D7A) or
                    (10,0x1D72) or (11,0x1D7D);
                var asset=new NativeTextureAsset(Normalize(bank)+$"#{i}:{id:X8}",File.Exists(user)?user:shipped,w,h,effect,effect && id==0xEF77,smoothCutout:buoy || dial || (FilterMenuArtwork && IsMenuArtworkBank(bank)),uiTileSize:dial ? 32 : 0);
                images.Add(new ImageRecord(i,id,depth,x,y,w,h,cx,cy,cw,ch,asset));
            }
            offset=end;
        }
        if (offset!=data.Length) throw new InvalidDataException("Unexpected TMS trailing data.");
        return images.ToArray();
    }

    public sealed class LoadScope : IDisposable
    {
        private readonly CpuContext _cpu;
        private readonly PSMemory _memory;
        private readonly uint _destination;
        private readonly string _name;
        private readonly uint _caller;
        public LoadScope(CpuContext cpu,PSMemory memory,uint destination,uint nameAddress)
        {
            _cpu=cpu; _memory=memory; _destination=destination;
            _caller=cpu.RA;
            var chars=new List<char>();
            try { for (int i=0;i<255;i++) { byte c=memory.ReadU8(nameAddress+(uint)i); if(c==0)break; chars.Add((char)c); } }
            catch { chars.Clear(); }
            _name=Normalize(new string(chars.ToArray()));
            // A failed/reused file load must not retain a previous bank identity.
            uint p=destination&0x1fffffff;
            uint size=FileSizes.TryGetValue(_name,out uint known)?known:(uint)Math.Max(0,memory.Ram.Length-(long)p);
            MenuSourceTrace.Invalidate(destination,size);
            MenuBackgrounds.Invalidate(destination,size);
            Active.RemoveAll(m=>p<(m.Destination&0x1fffffff)+m.Original.Length && p+(long)size>(m.Destination&0x1fffffff));
        }
        public void Dispose()
        {
            MenuBackgrounds.Loaded(_memory,_name,_destination,_cpu.V0);
            if (FileSizes.TryGetValue(_name,out uint sourceSize))
                MenuSourceTrace.Loaded(_memory,_name,_destination,sourceSize,_cpu.V0,_caller);
            if(Environment.GetEnvironmentVariable("JETMOTO_DIAG_UI")=="1"&&_name.EndsWith("JMFONT.TIM",StringComparison.Ordinal))
                Console.WriteLine($"[JetMoto:ui-font] source={_name} destination={_destination:X8} read={_cpu.V0} caller={_caller:X8}");
            if (!Models.TryGetValue(_name,out var model)) return;
            try
            {
                if (_cpu.V0==0 || _memory.ReadU32(_destination)!=0x50535844 ||
                    _memory.ReadU32(_destination+4)!=0x43 || _memory.ReadU32(_destination+8)!=U32(model.Original,8))
                { Console.WriteLine($"[JetMoto:native-textures] Did not bind incomplete native model {_name}, result={_cpu.V0}."); return; }
                uint p=_destination&0x1fffffff;
                if (p+(long)model.Original.Length>_memory.Ram.Length) return;
                // Match the actual read to the immutable source before the engine patches/animates it.
                if (!_memory.Ram.Slice((int)p,model.Original.Length).SequenceEqual(model.Original))
                { Console.WriteLine($"[JetMoto:native-textures] Native model {_name} differed at the loader boundary; left original."); return; }
                Active.RemoveAll(m=>p<(m.Destination&0x1fffffff)+m.Original.Length && p+model.Original.Length>(m.Destination&0x1fffffff));
                model.Destination=_destination; Active.Add(model);
                Console.WriteLine($"[JetMoto:native-textures] Native model loaded: {_name}, {_destination:X8}, {model.Original.Length} bytes, read={_cpu.V0}.");
            }
            catch(Exception e) when(e is not OutOfMemoryException)
            { Console.WriteLine($"[JetMoto:native-textures] Load binding skipped for {_name}: {e.Message}"); }
        }
    }
    public static LoadScope ObserveLoad(CpuContext cpu,PSMemory memory,uint destination,uint filename)=>new(cpu,memory,destination,filename);
    public static void ResetActive() { Active.Clear(); }
    public static void SaveUsage(string root)
    {
        // Call on the game thread after it stops, not from the asynchronous heartbeat.
        var used=Models.Values.Select(m=>new { source=m.Name, linked=m.Materials
            .Where(p=>p.Value!=null).Select(p=>new { primitiveOffset=$"0x{p.Key:X}",
                texture=p.Value!.Asset.Key, png=p.Value.Asset.FilePath }).ToArray() }).Where(m=>m.linked.Length!=0).ToArray();
        Directory.CreateDirectory(Path.Combine(root,"logs"));
        File.WriteAllText(Path.Combine(root,"logs","native-textures.json"),System.Text.Json.JsonSerializer.Serialize(
            new { schema=2, summary=Summary, worldLighting=WorldLighting.Summary,
                hud=HudAssets.Values.Select(a=>new {key=a.Key,png=a.FilePath,bound=a.BoundCommands,resolved=a.ResolvedCommands}).ToArray(),
                effects=Models.Values.SelectMany(m=>m.Images).Where(i=>i.Asset.AllowsEffectCoverage)
                    .Select(i=>new {key=i.Asset.Key,png=i.Asset.FilePath,coverageLoaded=i.Asset.CoverageLoaded,
                        bound=i.Asset.BoundCommands,resolved=i.Asset.ResolvedCommands}).ToArray(),
                legacyVramMatcher=RecompOne.Runtime.Assets.Textures.TextureResolver.StatsLine(), originalSourceMaterials=used },new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
    }

    private static bool RiderOwns(Model model,int selector,int part)
    {
        // A tag in the same bank is not enough: each rider owns its own pose
        // nodes. Shared mesh leaves are fine; another rider's bones are not.
        if(!model.RiderHierarchy.TryGetValue(selector,out var owned))
        {
            owned=[];var b=model.Original;uint origin=U32(b,12);var pending=new Stack<uint>();
            pending.Push(U32(b,selector+16));
            while(pending.TryPop(out uint address))
            {
                long offset=(long)address-origin;
                if(offset<0 || offset+32>b.Length || owned.Count>=4096)
                    throw new InvalidDataException("Invalid original rider hierarchy.");
                int o=(int)offset;if(!owned.Add(o))continue;
                int type=b[o],count=0,start=0;
                switch(type)
                {
                    case 1:count=b[o+0x14];start=0x18;break;
                    case 2:count=checked((int)U32(b,o+0x10));start=0x14;break;
                    case 3:count=b[o+0xB];start=0x20;break;
                    case 4:count=b[o+0x12];start=0x14;break;
                    case 5:count=b[o+0x26];start=0x28;break;
                    case 9:count=b[o+6];start=0x10;break;
                    case 12:count=U16(b,o+6);start=8;break;
                }
                if(count>64 || (long)o+start+4L*count>b.Length)
                    throw new InvalidDataException("Invalid original rider child table.");
                for(int i=0;i<count;i++)
                {
                    uint child=U32(b,o+start+i*4);
                    if(type==2)
                    {
                        long range=(long)child-origin;
                        if(range<0 || range+12>b.Length)throw new InvalidDataException("Invalid rider LOD range.");
                        child=U32(b,(int)range+8);
                    }
                    pending.Push(child);
                }
            }
            model.RiderHierarchy.Add(selector,owned);
        }
        return owned.Contains(part);
    }

    public static bool IsOriginalRiderTranslation(uint selector,uint address)
    {
        uint sp=selector&0x1fffffffu,p=address&0x1fffffffu;
        foreach(var model in Active)
        {
            uint origin=model.Destination&0x1fffffffu;
            long so=(long)sp-origin,o=(long)p-origin;
            if(so<0 || so+32>model.Original.Length || o<0 || o+24>model.Original.Length) continue;
            return model.Original[(int)so]==9 && U16(model.Original,(int)so+4)==1000 &&
                model.Original[(int)o]==4 && U16(model.Original,(int)o+0x10)==1 && RiderOwns(model,(int)so,(int)o);
        }
        return false;
    }

    public static bool IsOriginalRiderBone(uint selector, uint address, int tag)
    {
        uint sp=selector&0x1fffffffu, p=address&0x1fffffffu;
        foreach (var model in Active)
        {
            uint origin=model.Destination&0x1fffffffu;
            long selectorOffset=(long)sp-origin, offset=(long)p-origin;
            if (selectorOffset<0 || selectorOffset+32>model.Original.Length || offset<0 || offset+44>model.Original.Length) continue;
            int o=(int)offset, so=(int)selectorOffset;
            return model.Original[so]==9 && U16(model.Original,so+4)==1000 &&
                model.Original[o]==5 && U16(model.Original,o+0x24)==tag && tag is >=2 and <=8 && RiderOwns(model,so,o);
        }
        return false;
    }

    internal static readonly bool ExtendedWaterLod=Environment.GetEnvironmentVariable("JETMOTO_EXTEND_WATER_LOD")=="1";
    public static uint WaterLodDistance(uint address,uint squaredDistance)
    {
        if(!ExtendedWaterLod||!Widescreen.Active)return squaredDistance;
        uint p=address&0x1fffffffu;
        foreach(var model in Active){
            long offset=(long)p-(model.Destination&0x1fffffffu);
            if(offset>=0&&offset<model.Original.Length&&WorldLighting.IsWaterLod(model,(int)offset))
                return WaterLodPolicy.ExtendDistance(squaredDistance);
        }
        return squaredDistance;
    }

    public static bool IsOriginalRiderNode(uint address, byte type)
    {
        uint p=address&0x1fffffffu;
        foreach(var model in Active)
        {
            long offset=(long)p-(model.Destination&0x1fffffffu);
            if(offset<0 || offset+36>model.Original.Length) continue;
            int o=(int)offset;var b=model.Original;
            if(b[o]!=type) return false;
            if(type==9) return U16(b,o+2) is >=200 and <220 && U16(b,o+4)==1000 && b[o+6]==4;
            if(type!=2 || model.Name!="NAVIGATE/PICKRIDE.DMD" || U32(b,o+16)!=4) return false;
            // Verify the original near-to-far ranges, including the near child.
            uint origin=U32(b,12);uint[] ends=[324,625,729,1225];uint previous=0;
            for(int i=0;i<4;i++)
            {
                long r=(long)U32(b,o+20+i*4)-origin;
                if(r<0 || r+12>b.Length || U32(b,(int)r)!=ends[i] || U32(b,(int)r+4)!=previous) return false;
                previous=ends[i];
            }
            return true;
        }
        return false;
    }

    public static NativeTextureMaterial? ResolveSource(Model model,int offset)
    {
        if (model.Materials.TryGetValue(offset,out var cached)) return cached;
        NativeTextureMaterial? result=null;
        var b=model.Original;
        if (offset>=0 && offset<=b.Length-20)
        {
            int words=b[offset+2], colors=b[offset+1], length=words*4;
            if (colors is >=1 and <=4 && length>=20 && offset+(long)length<=b.Length)
            {
                int op=b[offset+19], n=(op&8)!=0?4:3, uv=offset+16+colors*4;
                if (op>=0x20 && op<=0x3f && (op&4)!=0 && uv+n*4<=offset+length)
                {
                    ushort clut=U16(b,uv+2),page=U16(b,uv+6);
                    int depth=(page>>7)&3, scale=depth==0?4:depth==1?2:1;
                    int pageX=(page&15)*64,pageY=((page>>4)&1)*256;
                    int cx=(clut&63)*16,cy=clut>>6;
                    uint tw=0;
                    if ((b[offset]&0x80)!=0 && uv+n*4+4<=offset+length && (U32(b,uv+n*4)>>24)==0xe2) tw=U32(b,uv+n*4);
                    int ax=~((int)(tw&31)*8)&255, ay=~((int)((tw>>5)&31)*8)&255;
                    int ox=(int)(((tw>>10)&31)&(tw&31))*8,oy=(int)(((tw>>15)&31)&((tw>>5)&31))*8;
                    int minU=255,minV=255,maxU=0,maxV=0;
                    for(int v=0;v<n;v++)
                    { int u=b[uv+v*4], y=b[uv+v*4+1];minU=Math.Min(minU,u);maxU=Math.Max(maxU,u);minV=Math.Min(minV,y);maxV=Math.Max(maxV,y); }
                    var key=(depth,depth<2?cx:0,depth<2?cy:0);
                    ImageRecord[] candidates=model.Groups.TryGetValue(key,out var group)?group:[];
                    foreach(var image in candidates)
                    {
                        if(image.Depth!=depth || (depth<2 && (cx!=image.ClutX || cy!=image.ClutY))) continue;
                        int u0=(image.X-pageX)*scale,v0=image.Y-pageY;
                        var material=new NativeTextureMaterial(image.Asset,u0,v0,image.Width,image.Height,page,clut);
                        if (!material.Accepts(page,clut,minU,minV,maxU,maxV,ax,ay,ox,oy)) continue;
                        if (model.Name=="MISC/OPTIONS.DMD" && image.Depth==0 &&
                            ((image.Ordinal==1 && image.Id==0x867C) || (image.Ordinal==2 && image.Id==0x8679)) &&
                            ax==255 && ay==255 && ox==0 && oy==0)
                        {
                            int width=maxU-minU+1,height=maxV-minV+1;
                            string region=Path.Combine(Path.GetDirectoryName(image.Asset.FilePath)!,"Regions",
                                Path.GetFileNameWithoutExtension(image.Asset.FilePath),$"{minU:D3}-{minV:D3}-{width:D3}-{height:D3}.png");
                            if (File.Exists(region))
                            {
                                var isolated=new NativeTextureAsset($"{image.Asset.Key}@{minU},{minV},{width},{height}",
                                    region,width,height,smoothCutout:true);
                                material=new NativeTextureMaterial(isolated,minU,minV,width,height,page,clut);
                            }
                        }
                        if(result!=null) { result=null; break; } // Ambiguous original material: do not guess.
                        result=material;
                    }
                }
            }
        }
        model.Materials[offset]=result;
        return result;
    }

    // The native close-range path reserves an unfinished packet, then splits it into
    // four pieces in 8010F75C. 8010FF50 finalizes their headers. Bind only AFTER that
    // finalizer, using the original DMD pointer still in S0, not stale header bytes.
    public static void FinishSubdivision(PSMemory memory,uint source,uint original,uint a,uint b,uint c)
    {
        WorldLighting.FinalizeSubdivision(original,a,b,c,memory);
        uint p=source&0x1fffffff;
        for(int i=Active.Count-1;i>=0;i--)
        {
            var m=Active[i];long offset=(long)p-(m.Destination&0x1fffffff);
            if(offset<0 || offset>=m.Original.Length)continue;
            var material=ResolveSource(m,(int)offset);
            if(material!=null)
            {
                if(BindSubdivision(memory,material,original,a,b,c)) { _subdivided++; _packets+=4; }
                else _badPackets++;
            }
            return;
        }
    }
    public static bool BindSubdivision(PSMemory memory,NativeTextureMaterial material,uint original,uint a,uint b,uint c)
    {
        Span<uint> nodes=stackalloc uint[]{original,a,b,c};
        Span<int> lengths=stackalloc int[4];
        // Check ALL four before binding. Payload provenance then has the same normal
        // byte-write invalidation as ordinary packets. Header links are not tagged.
        for(int i=0;i<4;i++)
        {
            uint address=nodes[i],physical=address&0x1fffffff;
            if(physical<4 || !memory.IsWorkMemoryRange(physical,8) || (physical&3)!=0)return false;
            for(int j=0;j<i;j++)if((nodes[j]&0x1fffffff)==physical)return false;
            int length=(int)(memory.ReadU32(address)>>24);
            byte op=(byte)(memory.ReadU32(address+4)>>24);
            if(op<0x20 || op>0x3f || (op&4)==0 || length!=CommandLength(op) || !memory.IsWorkMemoryRange(physical,4L+length*4L))return false;
            lengths[i]=length;
        }
        for(int i=0;i<4;i++)if(!NativeTextureBindings.Bind(nodes[i]+4,lengths[i],material))return false;
        return true;
    }

    public sealed class EmissionScope : IDisposable
    {
        private NativeTextureMaterial? _material;
        private WorldSurface? _world;
        private uint _start;
        private string _sourceName="unknown";
        private int _sourceOffset;
        private bool _isDeferred;
        private static readonly HashSet<string> UnlitDiagnostic = [];
        private static readonly bool AuditUnlit = Environment.GetEnvironmentVariable("JETMOTO_AUDIT_UNLIT")=="1";
        public EmissionScope(PSMemory memory,uint list=0,uint count=0,uint rotations=0)
        {
            // State loads or other bulk buffer reuse must not resurrect a previous bank.
            Active.RemoveAll(m => memory.ReadU32(m.Destination)!=0x50535844 ||
                memory.ReadU32(m.Destination+4)!=0x43 || memory.ReadU32(m.Destination+8)!=U32(m.Original,8));
            WorldLighting.PrepareEmission(memory,list,count,rotations,Active);
        }
        public void BeginPrimitive(PSMemory memory,uint source,uint output,uint mesh=0)
        {
            _material=null; _world=null; _start=output; _isDeferred=false;
            _primitives++;
            uint p=source&0x1fffffff;
            for(int i=Active.Count-1;i>=0;i--)
            {
                var m=Active[i]; long offset=(long)p-(m.Destination&0x1fffffff);
                if(offset<0 || offset>=m.Original.Length)continue;
                _sourceName=m.Name; _sourceOffset=(int)offset;
                _material=ResolveSource(m,(int)offset);
                _world=_material?.Asset.AllowsEffectCoverage==true ? null : WorldLighting.Resolve(m,(int)offset,mesh);
                if(_material!=null)_linked++;else _unknown++;
                return;
            }
            _unknown++;
        }
        public void Defer() { _isDeferred=true; if(_material!=null)_deferred++; }
        public void EndPrimitive(PSMemory memory,uint output)
        {
            var material=_material; var world=_world; _material=null; _world=null;
            if(AuditUnlit && world==null &&
                RecompOne.Runtime.Interrupts.VBlankCount is >=4500 and <=4800 && output>_start && output-_start<4096 && memory.IsWorkMemoryRange(_start,output-_start))
            {
                byte op=(byte)(memory.ReadU32(_start+4)>>24);
                if(op is >=0x20 and <=0x3f){
                    int n=(op&8)!=0?4:3;bool tex=(op&4)!=0,gouraud=(op&16)!=0;
                    uint pos=_start+8;int minX=32767,minY=32767,maxX=-32768,maxY=-32768;
                    for(int i=0;i<n;i++){
                        if(pos+4>output)break;
                        uint xy=memory.ReadU32(pos);int x=(short)xy,y=(short)(xy>>16);
                        minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);
                        pos+=(uint)(4+(tex?4:0)+(gouraud?4:0));
                    }
                    string key=$"{_sourceName}@{_sourceOffset:X}";
                    if((maxX-minX)*(long)(maxY-minY)>12000 && UnlitDiagnostic.Add(key))
                        Console.WriteLine($"[JetMoto:unlit] {key} op={op:X2} bounds={minX},{minY}..{maxX},{maxY}");
                }
            }
            if(_isDeferred){WorldLighting.Defer(_start,world);return;}
            if((material==null && world==null) || output==_start)return;
            uint start=_start&0x1fffffff,end=output&0x1fffffff;
            void Bad(string reason)
            {
                _badPackets++;
                if(_badPackets<=6)Console.WriteLine($"[JetMoto:native-textures] Original packet fallback {_sourceName}@{_sourceOffset:X}: {reason}; {start:X8}..{end:X8}");
            }
            if(end<start || end-start>4096 || !memory.IsWorkMemoryRange(start,(long)end-start) || (start&3)!=0 || (end&3)!=0) { Bad("invalid emission span");return; }
            // Validate the complete emitted span before granting any material bindings.
            Span<(uint Address,int Words)> bindings=stackalloc (uint,int)[32];int count=0;
            uint p=start;
            while(p<end)
            {
                int words=(int)(memory.ReadU32(p)>>24);
                uint stop=p+4+(uint)words*4;
                if(stop>end) { Bad($"header {memory.ReadU32(p):X8} crosses span at {p:X8}");return; }
                uint command=p+4;
                while(command<stop)
                {
                    byte op=(byte)(memory.ReadU32(command)>>24);int length=CommandLength(op);
                    if(length<1 || command+length*4>stop) { Bad($"unsupported command {op:X2} at {command:X8}");return; }
                    if(op is >=0x20 and <=0x3f && ((op&4)!=0 || world!=null))
                    {
                        if(count==bindings.Length) { Bad("too many native sub-primitives");return; }
                        bindings[count++]=(command,length);
                    }
                    command+=(uint)length*4;
                }
                p=stop; // Header-only (zero-word) packets are legal and carry no texture.
            }
            foreach(var binding in bindings[..count])
            {
                if(material!=null && (memory.ReadU32(binding.Address)&0x04000000)!=0){NativeTextureBindings.Bind(binding.Address,binding.Words,material);_packets++;}
                if(world!=null)WorldSurfaceBindings.Bind(binding.Address,binding.Words,world);
            }
        }
        public void Dispose() { _material=null; }
    }
    public static EmissionScope BeginEmission(PSMemory memory,uint list=0,uint count=0,uint rotations=0)=>new(memory,list,count,rotations);
    public static int CommandLength(byte op)
    {
        if(op is >=0x20 and <=0x3f) { int n=(op&8)!=0?4:3;return 1+n+((op&16)!=0?n-1:0)+((op&4)!=0?n:0); }
        if(op is >=0xe0 and <=0xff || op is 0 or 1) return 1;
        if(op==2) return 3;
        if(op is >=0x40 and <=0x5f)return (op&8)!=0?-1:3+((op&16)!=0?1:0);
        if(op is >=0x60 and <=0x7f)return 2+((op&4)!=0?1:0)+((op&0x18)==0?1:0);
        if(op is >=0x80 and <=0x9f)return 4;
        return -1;
    }
}
