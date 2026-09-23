using JetMoto;
using System.Text.Json;
using RecompOne.Runtime.Assets.Native;

int passed=0,failed=0;
void Check(bool b,string why){if(b){passed++;Console.WriteLine("PASS: "+why);}else{failed++;Console.Error.WriteLine("FAIL: "+why);}}
string temp=Path.Combine(Path.GetTempPath(),"JetMoto-EffectTests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
string fixtures=Path.Combine(AppContext.BaseDirectory,"Fixtures");
try {
 string file=Path.Combine(temp,"test.png"),side=file+".material.json";
 string key="ISLAND1/ISLAND1.TMS#62:0000EF77";
 Dictionary<string,object> Meta()=>new(){{"format","jetmoto-effect-material-1"},{"sourceKey",key},{"sourceWidth",16},{"sourceHeight",16},{"alphaMode","coverage"}};
 void Write(Dictionary<string,object> m)=>File.WriteAllText(side,JsonSerializer.Serialize(m));
 NativeTextureAsset Asset(bool allowed=true,string? identity=null)=>new(identity??key,file,16,16,allowed);
 File.Copy(Path.Combine(fixtures,"coverage-64.png"),file,true);Write(Meta());
 var a=Asset();var t=a.GetTexture();Check(t is {Coverage:true,Nearest:false}&&a.CoverageLoaded,"verified sidecar permits smooth alpha / linear filtering");
 Check(ReferenceEquals(t,a.GetTexture()),"effect image cache reuses decoded object");
 Check(!a.WaterSpray,"coverage alone does not enable airborne water geometry");
 Check(!new NativeTextureAsset(key,file,16,16,false,true).WaterSpray,"water spray cannot bypass the verified effect allowlist");
 Check(new NativeTextureAsset(key,file,16,16,true,true).WaterSpray,"explicit water effect classification enables the additional droplet layer");
 Check(Asset(false).GetTexture()==null,"non-effect original identity cannot opt into coverage");
 Check(Asset(identity:key+"-other").GetTexture()==null,"neighboring original identity cannot borrow an effect sidecar");
 foreach(var kv in new KeyValuePair<string,object>[] {new("format","wrong"),new("sourceKey","other"),new("sourceWidth",17),new("sourceHeight",17),new("alphaMode","STP"),new("sourceWidth","16"),new("alphaMode",true)}) {
  var m=Meta();m[kv.Key]=kv.Value;Write(m);Check(Asset().GetTexture()==null,"reject changed metadata "+kv.Key+"="+kv.Value);
 }
 foreach(string property in Meta().Keys){var m=Meta();m.Remove(property);Write(m);Check(Asset().GetTexture()==null,"reject missing required metadata "+property);}
 foreach(string broken in new[]{"{}","null","[]","{","\"bad\"",new string(' ',4097)}){File.WriteAllText(side,broken);Check(Asset().GetTexture()==null,"reject malformed/type/size metadata, length "+broken.Length);}
 File.Delete(side);Check(Asset().GetTexture()==null,"smooth alpha without sidecar is not silently interpreted as STP");
 File.Copy(Path.Combine(fixtures,"coverage-128.png"),file,true);var legacy=Asset().GetTexture();
 Check(legacy is {Coverage:false,Nearest:true},"old categorical effect override remains compatible without sidecar");
 Write(Meta());Check(Asset().GetTexture() is {Coverage:true,Nearest:false},"same alpha 128 explicitly means coverage only with matching metadata");
 File.WriteAllBytes(file,new byte[100]);Check(Asset().GetTexture()==null,"invalid PNG remains rejected even with valid effect metadata");
 File.Copy(Path.Combine(fixtures,"coverage-64.png"),file,true);Write(Meta());
 var active=Asset();active.GetTexture();NativeTextureBindings.Init(0x200000);NativeTextureBindings.OriginalAssetsOnly=true;
 var material=new NativeTextureMaterial(active,0,0,16,16,0x11a,0);long before=NativeTextureBindings.EffectCommandsResolved;
 Check(NativeTextureBindings.Bind(0x80040000,9,material),"effect binds explicit original material to original command address");
 Check(ReferenceEquals(NativeTextureBindings.Resolve(0x80040000),material),"effect command resolves by native provenance");
 Check(NativeTextureBindings.EffectCommandsResolved==before+1&&active.BoundCommands==1&&active.ResolvedCommands==1,"per-source effect telemetry records actual command resolution");
 NativeTextureBindings.Invalidate(0x80040004,1);Check(NativeTextureBindings.Resolve(0x80040000)==null,"packet rewrite invalidates effect provenance instead of stale replacement");
 foreach(uint shadow in new uint[]{0xEF79,0xB9D2})foreach(string bank in new[]{"ISLAND1/ISLAND1.TMS","SWAMP1/SWAMP1.TMS","SELECT/SELECT.TMS"})
  for(int ord=0;ord<150;ord++) if(NativeTextures.IsEffectTexture(bank,ord,shadow)) throw new Exception("Shadow incorrectly whitelisted");
 Check(true,"bike and scenery shadows cannot enter the particle coverage allowlist");
 Check(!NativeTextures.IsEffectTexture("DARK/DARK.TMS",62,0xEF77),"unrelated DARK bank cannot borrow a water texture ID");
 Check(!NativeTextures.IsEffectTexture("ISLAND1/ISLAND1.TMS",61,0xEF77),"wrong original record ordinal rejected");
 Check(NativeTextures.IsEffectTexture("cdrom:\\island1\\island1.tms;1",62,0xEF77),"original game path normalization preserves intended identity");
 if(args.Length==2) {
  using var doc=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(args[1],"pack-manifest.json")));int all=0,effects=0;
  foreach(var bank in doc.RootElement.GetProperty("banks").EnumerateArray()) {
   string original=bank.GetProperty("source").GetString()!;string root=Path.Combine(temp,"load-root");Directory.CreateDirectory(root);
   // Parse real original TMS records, then use the exact ship path/identity.
   var records=NativeTextures.ParseBank(original,File.ReadAllBytes(Path.Combine(args[0],original)),root);
   foreach(var record in records){all++;bool allow=NativeTextures.IsEffectTexture(original,record.Ordinal,record.Id);Check(record.Asset.AllowsEffectCoverage==allow,"real TMS whitelist "+record.Asset.Key);
    if(!allow)continue;effects++;
    string rel=original[..^4]+$"/{record.Ordinal:D4}-{record.Id:X8}.png";
    var real=new NativeTextureAsset(record.Asset.Key,Path.Combine(args[1],rel),record.Width,record.Height,allow);
    Check(real.GetTexture() is {Coverage:true},"authored effect loads through real original TMS identity "+record.Asset.Key);
   }
  }
  Check(all==1406&&effects==15,"complete source audit: 1406 original entries, exactly 15 allowed effects");
 }
 else if(args.Length!=0)throw new ArgumentException("Usage: EffectTextureTests [original-asset-tree complete-pack]");
} catch(Exception ex){Check(false,ex.ToString());} finally{NativeTextureBindings.OriginalAssetsOnly=false;Directory.Delete(temp,true);}
Console.WriteLine($"RESULT: {passed} passed; {failed} failed.");return failed==0?0:1;
