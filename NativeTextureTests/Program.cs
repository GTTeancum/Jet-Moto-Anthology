using JetMoto;
using System.Buffers.Binary;
using RecompOne.Runtime;
using RecompOne.Runtime.Assets.Native;
using RecompOne.Runtime.Memory;

int passed=0,failed=0;
void Check(bool ok,string s){if(ok){passed++;Console.WriteLine("PASS: "+s);}else{failed++;Console.Error.WriteLine("FAIL: "+s);}}
void Test(string name,Action action){try{action();}catch(Exception e){Check(false,name+": "+e);}}
NativeTextureBindings.OriginalAssetsOnly=true;
Test("menu artwork isolation",()=>{
    foreach(string bank in new[]{"STARTUP/TITLE.TMS","NAVIGATE/RACETYPE.TMS","NAVIGATE/SCORING.TMS",
        "MISC/OPTIONS.TMS","MISC/SOUND.TMS","MISC/LOADGAME.TMS","MISC/SAVEGAME.TMS",
        "PICKTRAC/TRACKS0.TMS","PICKTRAC/TRACKS1.TMS","PICKTRAC/TRACKS2.TMS","PICKTRAC/TRACKS3.TMS",
        "STANDING/OVERALL.TMS","STANDING/SWAMP1/STANDING.TMS"})
        Check(NativeTextures.IsMenuArtworkBank(bank),$"dedicated menu bank {bank}");
    foreach(string bank in new[]{"ISLAND1/ISLAND1.TMS","SWAMP1/SWAMP1.TMS","ALPINE1/ALPINE1.TMS",
        "NAVIGATE/PICKRIDE.TMS","PRIZES/PRIZE.TMS","MISC/UNREVIEWED.TMS","OTHER/OPTIONS.TMS"})
        Check(!NativeTextures.IsMenuArtworkBank(bank),$"unrelated/mixed bank retains existing sampling: {bank}");
    Check(NativeTextures.IsMenuArtworkBank("cdrom:\\misc\\options.tms;1"),"menu identity follows native path normalization");
});
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
Test("HUD descriptor provenance",()=>{
    byte[] dmd=new byte[80];
    void W(int o,uint n)=>BinaryPrimitives.WriteUInt32LittleEndian(dmd.AsSpan(o,4),n);
    dmd[32]=4;dmd[33]=1;dmd[34]=9;dmd[35]=9;
    W(48,0x2c808080);W(52,0x00000f0f);W(56,0x011a0f00);W(60,0x0000000f);W(64,0);
    var source=new NativeTextures.Model("HUD/TEST.DMD",dmd,
        [new NativeTextures.ImageRecord(0,1,2,640,256,16,16,0,0,0,0,asset)]){Destination=0x6000};
    var active=(List<NativeTextures.Model>)typeof(NativeTextures).GetField("Active",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!.GetValue(null)!;
    active.Add(source);
    uint owner=0x9000,descriptor=owner+0x1280,head=0x4000,list=0x4100,pageNode=0x4200,sprite=0x4300;
    for(uint i=0;i<12;i++)memory.WriteU8(descriptor+i,0);
    NativeTextures.RememberHudSource(memory,0x6020,descriptor);
    void Start(){memory.WriteU32(list,head);memory.WriteU32(head,0x00ffffff);memory.WriteU32(sprite+4,0x64808080);memory.WriteU32(sprite+8,0);memory.WriteU32(sprite+12,0);memory.WriteU32(sprite+16,0x00100010);}
    void Emit(){memory.WriteU32(head,pageNode);memory.WriteU32(pageNode,0x01000000|sprite);memory.WriteU32(pageNode+4,0xe100011a);memory.WriteU32(sprite,0x04ffffff);}
    try{
        Start();using(NativeTextures.BeginHud(memory,owner,list)){Emit();}
        Check(NativeTextureBindings.Resolve(sprite+4)?.Asset.Key==asset.Key+"/hud","HUD sprite inherits original descriptor material");
        Check(NativeTextureBindings.Resolve(sprite+4)?.Asset.SmoothCutout==true,"HUD contour filtering stays on owned material");
        Start();using(NativeTextures.BeginHud(memory,owner+0x3000,list)){Emit();}
        Check(NativeTextureBindings.Resolve(sprite+4)==null,"another HUD owner cannot borrow descriptors");
        Start();memory.WriteU8(descriptor,1);using(NativeTextures.BeginHud(memory,owner,list)){Emit();}
        Check(NativeTextureBindings.Resolve(sprite+4)==null,"rewritten descriptor loses source authority");
        memory.WriteU8(descriptor,0);
        Start();using(NativeTextures.BeginHud(memory,owner,list)){Emit();memory.WriteU32(sprite+16,0x00100011);}
        Check(NativeTextureBindings.Resolve(sprite+4)==null,"HUD crop outside original asset keeps native fallback");
        Start();Emit();using(NativeTextures.BeginHud(memory,owner,list)){}
        Check(NativeTextureBindings.Resolve(sprite+4)==null,"pre-existing ordering-table packets remain untagged");
        active.Remove(source);Start();using(NativeTextures.BeginHud(memory,owner,list)){Emit();}
        Check(NativeTextureBindings.Resolve(sprite+4)==null,"unloaded original bank cannot bind stale HUD");
    }finally{active.Remove(source);}
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
Test("source-owned menu label regions",()=>{
    string root=Path.Combine(Path.GetTempPath(),"JetMotoMenuRegions-"+Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try {
        byte[] dmd=new byte[80];int p=32;
        void W(int o,uint n)=>BinaryPrimitives.WriteUInt32LittleEndian(dmd.AsSpan(o,4),n);
        dmd[p]=4;dmd[p+1]=1;dmd[p+2]=9;W(p+16,0x2c7f7f7f);
        W(p+20,0x00300f0f);W(p+24,0x000b0f00);W(p+28,0x0000000f);W(p+32,0);
        var original=new NativeTextureAsset("MISC/OPTIONS.TMS#2:00008679",Path.Combine(root,"label.png"),16,16);
        var record=new NativeTextures.ImageRecord(2,0x8679,0,704,0,16,16,768,0,16,1,original);
        NativeTextureMaterial? Resolve(string name,NativeTextures.ImageRecord image)=>
            NativeTextures.ResolveSource(new(name,dmd,[image]),p);
        Check(ReferenceEquals(Resolve("MISC/OPTIONS.DMD",record)?.Asset,original),"missing label crop preserves original material");
        string directory=Path.Combine(root,"Regions","label");Directory.CreateDirectory(directory);
        File.Copy(Path.Combine(files,"red.png"),Path.Combine(directory,"000-000-016-016.png"));
        var isolated=Resolve("MISC/OPTIONS.DMD",record);
        Check(isolated!=null && !ReferenceEquals(isolated.Asset,original) && isolated.Asset.SmoothCutout,
            "reviewed original menu identity binds authored source crop");
        Check(isolated is {U0:0,V0:0,Width:16,Height:16} && isolated.Asset.GetTexture()!=null,
            "isolated label has original UV bounds and valid 4x image");
        Check(ReferenceEquals(Resolve("OTHER/OPTIONS.DMD",record)?.Asset,original),"same placement in other bank cannot borrow label crop");
        Check(ReferenceEquals(Resolve("MISC/OPTIONS.DMD",record with{Ordinal=1})?.Asset,original),"wrong ordinal and ID pairing cannot borrow label crop");
        File.Delete(Path.Combine(directory,"000-000-016-016.png"));
        Check(ReferenceEquals(Resolve("MISC/OPTIONS.DMD",record)?.Asset,original),"removed label crop restores original source fallback");
    }
    finally { Directory.Delete(root,true); }
});
Test("bank validation",()=>{
    foreach(byte[] b in new[]{new byte[0],new byte[8],new byte[20]}){
        bool rejected=false;try{NativeTextures.ParseBank("X.TMS",b,files);}catch(Exception){rejected=true;}
        Check(rejected,"malformed/truncated original bank rejected");
    }
    Check(NativeTextures.Normalize("cdrom:\\STARTUP\\TITLE.DMD;1")=="STARTUP/TITLE.DMD","native path normalization");
});
Test("standalone track image source gate",()=>{
    byte[] tim=new byte[20+320*240*2];
    void W(int offset,uint value)=>BinaryPrimitives.WriteUInt32LittleEndian(tim.AsSpan(offset,4),value);
    W(0,0x10);W(4,2);W(8,(uint)tim.Length-8);W(16,320|(240u<<16));
    Check(MenuBackgrounds.IsTrackOverview("ISLAND1/OVERV3L.TIM",tim),"original loading source accepted");
    Check(MenuBackgrounds.IsTrackOverview("ISLAND1/OVERV3.TIM",tim),"original post-loading source accepted");
    Check(MenuBackgrounds.IsTrackOverview("DARK/OVERV9L.TIM",tim),"last track loading source accepted");
    Check(!MenuBackgrounds.IsTrackOverview("ISLAND1/OVERV4L.TIM",tim),"wrong track and source pairing rejected");
    Check(!MenuBackgrounds.IsTrackOverview("NAVIGATE/RIDER00.TIM",tim),"portrait cannot borrow loading path");
    Check(!MenuBackgrounds.IsTrackOverview("ISLAND1/OVERV3L.TIM",tim.AsSpan(0,19)),"truncated header rejected");
    W(4,8);Check(!MenuBackgrounds.IsTrackOverview("ISLAND1/OVERV3L.TIM",tim),"paletted image rejected");
    W(4,2);W(8,12);Check(!MenuBackgrounds.IsTrackOverview("ISLAND1/OVERV3L.TIM",tim),"incorrect image block rejected");
    W(8,(uint)tim.Length-8);W(16,640|(120u<<16));
    Check(!MenuBackgrounds.IsTrackOverview("ISLAND1/OVERV3L.TIM",tim),"same byte count with wrong geometry rejected");
});
Test("standalone rider panel source gate",()=>{
    byte[] tim=new byte[20+576*192*2];
    void W(int offset,uint value)=>BinaryPrimitives.WriteUInt32LittleEndian(tim.AsSpan(offset,4),value);
    W(0,0x10);W(4,2);W(8,(uint)tim.Length-8);W(16,576|(192u<<16));
    Check(MenuBackgrounds.IsRiderPanel("NAVIGATE/RIDER00.TIM",tim),"first portrait source accepted");
    Check(MenuBackgrounds.IsRiderPanel("NAVIGATE/RIDER19.TIM",tim),"last portrait source accepted");
    foreach(string name in new[]{"NAVIGATE/RIDER20.TIM","NAVIGATE/RIDER0.TIM","OTHER/RIDER00.TIM","ISLAND1/OVERV3.TIM"})
        Check(!MenuBackgrounds.IsRiderPanel(name,tim),"unreviewed portrait identity rejected: "+name);
    Check(!MenuBackgrounds.IsRiderPanel("NAVIGATE/RIDER00.TIM",tim.AsSpan(0,19)),"truncated portrait rejected");
    W(4,0);Check(!MenuBackgrounds.IsRiderPanel("NAVIGATE/RIDER00.TIM",tim),"paletted portrait rejected");
    W(4,2);W(16,288|(384u<<16));
    Check(!MenuBackgrounds.IsRiderPanel("NAVIGATE/RIDER00.TIM",tim),"same-size wrong portrait layout rejected");
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
