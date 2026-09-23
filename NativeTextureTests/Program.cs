using JetMoto;
using System.Buffers.Binary;
using RecompOne.Runtime;
using RecompOne.Runtime.Assets.Native;
using RecompOne.Runtime.Memory;

int passed=0,failed=0;
void Check(bool ok,string s){if(ok){passed++;Console.WriteLine("PASS: "+s);}else{failed++;Console.Error.WriteLine("FAIL: "+s);}}
void Test(string name,Action action){try{action();}catch(Exception e){Check(false,name+": "+e);}}
NativeTextureBindings.OriginalAssetsOnly=true;
var memory=new PSMemory(0x200000);
string files=Path.Combine(AppContext.BaseDirectory,"Fixtures");
var asset=new NativeTextureAsset("fixture/original-id",Path.Combine(files,"red.png"),16,16);
var material=new NativeTextureMaterial(asset,0,0,16,16,0x11a,0);
Test("command provenance",()=>{
    foreach(uint a in new uint[]{0x1000,0x80001000,0xa0001000,0x80201000,0xa0601000}){
        Check(NativeTextureBindings.Bind(0x80001000,9,material),"bind native payload");
        Check(ReferenceEquals(NativeTextureBindings.Resolve(a),material),$"physical/KSEG/mirror {a:X8}");
    }
    Check(NativeTextureBindings.Resolve(0x1004)==null,"interior payload word cannot impersonate the command start");
    Check(NativeTextureBindings.Resolve(0xffc)==null,"ordering header has no material identity");
    foreach(uint a in new uint[]{0x1f800000,0x1f801810,0xbfc00000,0x1001,0x1ffffc})
        Check(!NativeTextureBindings.Bind(a,9,material),$"reject scratch/MMIO/BIOS/unaligned/cross-boundary {a:X8}");
    Check(!NativeTextureBindings.Bind(0x1000,0,material)&&!NativeTextureBindings.Bind(0x1000,17,material),"bounded packet word count");
    NativeTextureBindings.Bind(0x1000,9,material); memory.WriteU32(0xffc,0x09ffffff);
    Check(NativeTextureBindings.Resolve(0x1000)!=null,"linking ordering-table header preserves payload identity");
    for(uint i=0;i<36;i++){
        NativeTextureBindings.Bind(0x1000,9,material);memory.WriteU8(0x1000+i,memory.ReadU8(0x1000+i));
        Check(NativeTextureBindings.Resolve(0x1000)==null,$"same-value byte write {i} invalidates entire old command");
    }
    for(uint i=0;i<36;i+=2){
        NativeTextureBindings.Bind(0x1000,9,material);memory.WriteU16(0xa0001000+i,0);
        Check(NativeTextureBindings.Resolve(0x1000)==null,$"halfword/mirror write {i} invalidates old command");
    }
    NativeTextureBindings.Bind(0x1000,9,material);var queued=NativeTextureBindings.Resolve(0x1000);memory.WriteU32(0x1010,0);
    Check(ReferenceEquals(queued,material)&&NativeTextureBindings.Resolve(0x1000)==null,"queued immutable material survives later buffer reuse; new lookup does not");
    NativeTextureBindings.Bind(0x1000,9,material);NativeTextureBindings.Init(0x200000);
    Check(NativeTextureBindings.Resolve(0x1000)==null,"memory reset clears stale command provenance");
});
Test("native deferred subdivision",()=>{
    uint[] nodes=[0x5000u,0x5040u,0x5080u,0x50c0u];
    void Fill(){foreach(uint node in nodes){memory.WriteU32(node,0x09ffffff);memory.WriteU32(node+4,0x2c808080);for(uint j=1;j<9;j++)memory.WriteU32(node+4+j*4,0);}}
    Fill();
    Check(NativeTextures.BindSubdivision(memory,material,nodes[0],nodes[1],nodes[2],nodes[3]),"four finalized children inherit original DMD material");
    foreach(uint node in nodes)Check(ReferenceEquals(NativeTextureBindings.Resolve(node+4),material),$"subdivision child {node:X8} retains original asset identity");
    memory.WriteU32(nodes[0],0x09005040);
    Check(NativeTextureBindings.Resolve(nodes[0]+4)!=null,"later OT linking does not change subdivided material");
    memory.WriteU8(nodes[2]+11,0);
    Check(NativeTextureBindings.Resolve(nodes[2]+4)==null&&NativeTextureBindings.Resolve(nodes[1]+4)!=null,"rewriting one subdivided payload invalidates only that child");
    Fill();memory.WriteU32(nodes[3],0x0cffffff);
    Check(!NativeTextures.BindSubdivision(memory,material,nodes[0],nodes[1],nodes[2],nodes[3]),"stale/unfinalized child header rejects whole batch");
    Check(nodes.All(n=>NativeTextureBindings.Resolve(n+4)==null),"failed subdivision grants no partial material identity");
    Fill();
    Check(!NativeTextures.BindSubdivision(memory,material,nodes[0],nodes[1],nodes[2],nodes[2]),"duplicate child addresses rejected");
    Check(!NativeTextures.BindSubdivision(memory,material,0x1f800000,nodes[1],nodes[2],nodes[3]),"scratchpad is not a native subdivided output buffer");
});
Test("explicit source PNG policy",()=>{
    Check(asset.GetTexture() is {Width:64,Height:64,Nearest:true},"exact 4x original-ID PNG loads with categorical-alpha sampling");
    Check(ReferenceEquals(asset.GetTexture(),asset.GetTexture()),"decode cached by explicit asset object");
    foreach(string name in new[]{"missing.png","wrongsize.png","badalpha.png","corrupt.png"})
        Check(new NativeTextureAsset("bad/"+name,Path.Combine(files,name),16,16).GetTexture()==null,"safe original fallback: "+name);
    var alpha=new NativeTextureAsset("alpha",Path.Combine(files,"alpha.png"),16,16).GetTexture();
    Check(alpha!=null&&alpha.Rgba.Where((_,i)=>i%4==3).Distinct().Order().SequenceEqual(new byte[]{0,128,255}),"transparent/STP/opaque categories survive decoding");
});
Test("native material bounds",()=>{
    Check(material.Accepts(0x17a,999,0,0,15,15,255,255,0,0),"ABR changes and irrelevant direct-color CLUT do not change asset");
    Check(!material.Accepts(0x11b,0,0,0,15,15,255,255,0,0),"different native page must fall back");
    Check(!material.Accepts(0x19a,0,0,0,15,15,255,255,0,0),"different depth must fall back");
    Check(!material.Accepts(0x11a,0,0,0,16,15,255,255,0,0),"UV past original image does not clamp to an unrelated pixel");
    Check(!material.Accepts(0x11a,0,-1,0,15,15,255,255,0,0),"negative/wrapped UV is conservative");
    Check(material.Accepts(0x11a,0,0,0,31,15,239,255,0,0),"non-contiguous window masks are checked over full UV interval");
    var pal=material with{TPage=0x9a,Clut=0xabc};
    Check(pal.Accepts(0x9a,0xabc,0,0,15,15,255,255,0,0)&&!pal.Accepts(0x9a,0xabd,0,0,15,15,255,255,0,0),"palette animation cannot borrow another original palette");
});
Test("native source record linker",()=>{
    byte[] dmd=new byte[80];void W(int o,uint n)=>BinaryPrimitives.WriteUInt32LittleEndian(dmd.AsSpan(o,4),n);
    int p=32;dmd[p]=4;dmd[p+1]=1;dmd[p+2]=9;dmd[p+3]=9;W(p+16,0x2c7f7f7f);
    W(p+20,0x00000f0f);W(p+24,0x011a0f00);W(p+28,0x0000000f);W(p+32,0);
    var image=new NativeTextures.ImageRecord(3,0xef20,2,640,256,16,16,0,0,0,0,asset);
    var model=new NativeTextures.Model("ORIGINAL/FILE.DMD",dmd,[image]);
    var found=NativeTextures.ResolveSource(model,p);
    Check(found!=null&&ReferenceEquals(found.Asset,asset),"DMD primitive offset links directly to original bank record");
    Check(ReferenceEquals(found,NativeTextures.ResolveSource(model,p)),"stable native material cache");
    Check(NativeTextures.ResolveSource(model,p+1)==null,"arbitrary source offset cannot claim a material");
    var other=new NativeTextures.Model("DIFFERENT/FILE.DMD",dmd,[image with{Asset=new("different-id",Path.Combine(files,"green.png"),16,16)}]);
    Check(NativeTextures.ResolveSource(other,p)?.Asset.Key=="different-id","same placement in different source banks stays distinct");
    var ambiguous=new NativeTextures.Model("AMBIGUOUS",dmd,[image,image with{Ordinal=4,Id=99}]);
    Check(NativeTextures.ResolveSource(ambiguous,p)==null,"ambiguous original-bank records are not guessed");
    dmd[p+19]=0x28;
    Check(NativeTextures.ResolveSource(new("UNTEXTURED",dmd,[image]),p)==null,"untextured primitive left alone");
    foreach(byte op in new byte[]{0x24,0x2c,0x34,0x3c})
        Check(NativeTextures.CommandLength(op)==(op switch{0x24=>7,0x2c=>9,0x34=>9,_=>12}),$"GPU packet size {op:X2}");
});
Test("bank validation",()=>{
    foreach(byte[] b in new[]{new byte[0],new byte[8],new byte[20]}){
        bool rejected=false;try{NativeTextures.ParseBank("X.TMS",b,files);}catch(Exception){rejected=true;}
        Check(rejected,"malformed/truncated original bank rejected");
    }
    Check(NativeTextures.Normalize("cdrom:\\STARTUP\\TITLE.DMD;1")=="STARTUP/TITLE.DMD","native path normalization");
});
// Optional validation against the user's extracted ORIGINAL data, never packaged test copies.
if(args.Length==1)Test("real original banks",()=>{
    string root=Path.GetFullPath(args[0]);int textures=0,banks=0;
    foreach(string path in Directory.EnumerateFiles(root,"*.TMS",SearchOption.AllDirectories)){
        var images=NativeTextures.ParseBank(Path.GetRelativePath(root,path),File.ReadAllBytes(path),files);textures+=images.Length;banks++;
        Check(images.All(i=>i.Width>0&&i.Height>0),Path.GetFileName(path)+": original records valid");
    }
    Check(banks==34&&textures==1406,$"USA disc coverage: {banks} banks, {textures} original textures");
});
Console.WriteLine($"RESULT native asset tests: {passed} passed; {failed} failed.");return failed==0?0:1;
