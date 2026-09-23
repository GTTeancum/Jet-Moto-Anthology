using System.Security.Cryptography;
using System.Text.Json;
using RecompOne.Runtime.Assets.Native;
using StbImageSharp;
using JetMoto;

if (args.Length is < 1 or > 2) {
    Console.Error.WriteLine("Usage: NeuralPackTests <pack-directory> [original-1x-PNG-directory]");
    return 2;
}
try {
    string root=Path.GetFullPath(args[0]);
    using var manifest=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root,"pack-manifest.json")));
    var doc=manifest.RootElement;
    if(doc.GetProperty("build").GetString()!="09" || doc.GetProperty("scale").GetInt32()!=4 ||
       doc.GetProperty("texture_count").GetInt32()!=1406 || doc.GetProperty("banks").GetArrayLength()!=34)
        throw new InvalidDataException("Not the complete cumulative Build09 pack.");
    var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);int count=0, effects=0;long checkedAlpha=0, checkedCoverage=0;
    foreach(var bank in doc.GetProperty("banks").EnumerateArray()) {
        string original=bank.GetProperty("source").GetString()!;
        foreach(var item in bank.GetProperty("textures").EnumerateArray()) {
            string relative=item.GetProperty("png").GetString()!;
            int ordinal=item.GetProperty("ordinal").GetInt32();string id=item.GetProperty("texture_id").GetString()!;
            string expected=original[..^4]+$"/{ordinal:D4}-{id}.png";
            if(relative!=expected || relative.StartsWith('/') || relative.Contains('\\') || relative.Contains(':') ||
                relative.Split('/').Any(s=>s is "" or "." or "..") || !names.Add(relative))
                throw new InvalidDataException("Invalid/duplicate identity: "+relative);
            string file=Path.Combine(root,relative);
            string hash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
            if(!hash.Equals(item.GetProperty("png_sha256").GetString(),StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("PNG checksum: "+relative);
            int w=item.GetProperty("width").GetInt32(),h=item.GetProperty("height").GetInt32();
            bool effect=NativeTextures.IsEffectTexture(original,ordinal,Convert.ToUInt32(id,16));
            var asset=new NativeTextureAsset(original+"#"+ordinal+":"+id,file,w,h,effect);
            var texture=asset.GetTexture() ?? throw new InvalidDataException("Real runtime loader rejected "+relative);
            if(texture.Width!=w*4 || texture.Height!=h*4 || texture.ScaleX!=4 || texture.ScaleY!=4)
                throw new InvalidDataException("Wrong runtime dimensions: "+relative);
            if(!ReferenceEquals(texture,asset.GetTexture()))throw new InvalidDataException("Runtime cache changed object.");
            if(texture.Coverage!=effect || texture.Nearest==effect)throw new InvalidDataException("Wrong alpha/filter semantics: "+relative);
            if(effect) {
                string side=file+".material.json";
                string sideHash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(side)));
                if(!sideHash.Equals(item.GetProperty("enhancement").GetProperty("material_sha256").GetString(),StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Material checksum: "+relative);
                var levels=new HashSet<byte>();
                for(int y=0;y<texture.Height;y++)for(int x=0;x<texture.Width;x++) {
                    byte a=texture.Rgba[(y*texture.Width+x)*4+3];levels.Add(a);checkedCoverage++;
                    if((x<6||y<6||x>=texture.Width-6||y>=texture.Height-6)&&a!=0)throw new InvalidDataException("Missing transparent effect margin.");
                }
                if(levels.Count<32)throw new InvalidDataException("Effect coverage lacks smooth falloff.");
                effects++;
            }
            if(args.Length==2) {
                var image=ImageResult.FromMemory(File.ReadAllBytes(Path.Combine(args[1],relative)),ColorComponents.RedGreenBlueAlpha);
                if(image.Width!=w || image.Height!=h)throw new InvalidDataException("Wrong original size: "+relative);
                if(!effect) for(int y=0;y<h*4;y++)for(int x=0;x<w*4;x++) {
                    if(texture.Rgba[(y*w*4+x)*4+3]!=image.Data[((y/4)*w+x/4)*4+3])
                        throw new InvalidDataException("Alpha category/footprint changed: "+relative);
                    checkedAlpha++;
                }
            }
            count++;Console.WriteLine($"PASS {count:D4}: original ID, SHA256, runtime 4x PNG decode/cache"+(effect?", authored coverage":args.Length==2?", exact original alpha":"")+" "+relative);
        }
    }
    if(count!=1406 || effects!=15 || Directory.EnumerateFiles(root,"*.png",SearchOption.AllDirectories).Count()!=1406)
        throw new InvalidDataException("Wrong complete PNG count.");
    Console.WriteLine($"PASS: all {count} PNGs accepted by the actual NativeTextureAsset loader; {checkedAlpha} unchanged categorical alpha pixels and {checkedCoverage} authored coverage pixels checked; {effects} effect materials.");
    return 0;
} catch(Exception e) { Console.Error.WriteLine("FAIL: "+e);return 1; }
