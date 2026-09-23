using System.IO.Compression;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using RecompOne.Runtime;
using RecompOne.Runtime.Assets.Native;
using RecompOne.Runtime.Memory;
using StbImageSharp;

namespace JetMoto;

/// <summary>Lighting metadata is compiled from the exact original static DMD graph.
/// No original buffers, projection registers, textures, draw order or water geometry change.</summary>
public static class WorldLighting
{
    public sealed record Instance(int Shift,WorldBasis Rotation,Vector3 Translation);
    public sealed record Polygon(int Kind,Vector3 Normal,Vector3 Point);
    public sealed class Mesh{public int Offset;public Instance[] Instances=[];public Dictionary<int,Polygon> Polygons=[];}
    public sealed class Scene{public WorldScene Gpu=null!;public Dictionary<int,Mesh> Meshes=[];public Dictionary<int,Mesh?> PolygonOwners=[];}
    private static readonly Dictionary<string,Scene> Scenes=[];
    private static readonly Dictionary<uint,WorldSurface> Deferred=[];
    private static WorldCamera? _camera;
    private static int _cameraSlot=-1;private static long _epoch,_calibrated,_matched,_rejected,_faces;
    private static uint _lastMesh;private static WorldBasis _lastR;private static Vector3 _lastT;
    private static Instance? _lastInstance;private static Scene? _lastScene;
    private static long _attempts,_knownMesh,_knownPoly,_badRotation;private static int _dump;private static long _waterFaces;
    private static long _lastVblank;private static double _clock;
    public static string Summary=>$"lighting[epoch={_epoch},camera={_cameraSlot},calibrated={_calibrated},matched={_matched},mismatch={_rejected},faces={_faces},attempt={_attempts},mesh={_knownMesh},poly={_knownPoly},badR={_badRotation},water={_waterFaces}] {WorldSurfaceBindings.Summary}";
    private static Vector3 V(JsonElement a)=>new(a[0].GetSingle(),a[1].GetSingle(),a[2].GetSingle());
    private static WorldBasis R(JsonElement a,int i)=>new(a[i].GetSingle(),a[i+1].GetSingle(),a[i+2].GetSingle(),a[i+3].GetSingle(),a[i+4].GetSingle(),a[i+5].GetSingle(),a[i+6].GetSingle(),a[i+7].GetSingle(),a[i+8].GetSingle());
    public static void Configure(NativeTextures.Model model,string root)
    {
        WorldSurfaceBindings.FillSurfaceProvider = ResolveFillSurface;
        WorldSurfaceBindings.RectSurfaceProvider = ResolveRectSurface;
        RiderGeometry.Configure(model);
        string name=model.Name.Split('/')[0];string path=Path.Combine(root,"Lighting",name+".json.gz");
        if(!File.Exists(path))return;
        if(Scenes.ContainsKey(model.Name))return;
        try{
            if(new FileInfo(path).Length>32*1024*1024)throw new InvalidDataException("Geometry catalog too large");
            using var gzip=new GZipStream(File.OpenRead(path),CompressionMode.Decompress);
            using var decoded=new MemoryStream();byte[] buffer=new byte[65536];int read;
            while((read=gzip.Read(buffer))>0){if(decoded.Length+read>64*1024*1024)throw new InvalidDataException("Expanded geometry catalog too large");decoded.Write(buffer,0,read);}
            using var json=JsonDocument.Parse(decoded.ToArray());var d=json.RootElement;
            if(d.GetProperty("format").GetInt32()!=1||!Convert.ToHexString(SHA256.HashData(model.Original)).Equals(d.GetProperty("modelSha256").GetString(),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Original DMD fingerprint mismatch");
            byte[] pixels=File.ReadAllBytes(Path.Combine(root,"Lighting",name+"-height.png"));
            if(!Convert.ToHexString(SHA256.HashData(pixels)).Equals(d.GetProperty("heightSha256").GetString(),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Height data fingerprint mismatch");
            int size=d.GetProperty("mapSize").GetInt32();
            if(size!=1024||pixels.Length<33||pixels.Length>16*1024*1024||
               !pixels.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10})||
               System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(pixels.AsSpan(16,4))!=1024||
               System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(pixels.AsSpan(20,4))!=1024)
                throw new InvalidDataException("Height dimensions/format");
            var height=ImageResult.FromMemory(pixels,ColorComponents.RedGreenBlueAlpha);if(height.Width!=size||height.Height!=size)throw new InvalidDataException("Decoded height dimensions");
            var origin=d.GetProperty("origin");var extent=d.GetProperty("size");var range=d.GetProperty("heightRange");
            var scene=new Scene{Gpu=new WorldScene(name,height.Data,size,new(origin[0].GetSingle(),origin[1].GetSingle()),new(extent[0].GetSingle(),extent[1].GetSingle()),new(range[0].GetSingle(),range[1].GetSingle()),V(d.GetProperty("lightDirection")),WaterTint(name))};
            foreach(var e in d.GetProperty("meshes").EnumerateArray()){
                var mesh=new Mesh{Offset=e.GetProperty("offset").GetInt32()};
                if(mesh.Offset<0||mesh.Offset>=model.Original.Length-24)throw new InvalidDataException("Mesh address");
                mesh.Instances=e.GetProperty("instances").EnumerateArray().Select(a=>new Instance(a[0].GetInt32(),R(a,1),new(a[10].GetSingle(),a[11].GetSingle(),a[12].GetSingle()))).ToArray();
                if(mesh.Instances.Length==0||mesh.Instances.Any(a=>a.Shift<0||a.Shift>12||!a.Rotation.IsRotation))continue;
                int vp=e.GetProperty("vertices").GetInt32();
                foreach(var q in e.GetProperty("polygons").EnumerateArray()){
                    int off=q[0].GetInt32(),kind=q[1].GetInt32();if(off<0||off>model.Original.Length-24||kind is not (1 or 2 or 4))throw new InvalidDataException("Surface address/kind");
                    int index=System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(model.Original.AsSpan(off+4,2));int vertex=checked(vp+index*8);if(vertex<0||vertex>model.Original.Length-6)throw new InvalidDataException("Original vertex bounds");
                    float S(int i)=>System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(model.Original.AsSpan(vertex+i*2,2));
                    mesh.Polygons[off]=new Polygon(kind,new(q[2].GetSingle(),q[3].GetSingle(),q[4].GetSingle()),new(S(0),S(1),S(2)));
                }
                scene.Meshes[mesh.Offset]=mesh;
                foreach(int polygon in mesh.Polygons.Keys) {
                    if(scene.PolygonOwners.TryGetValue(polygon,out var previous)&&previous!=mesh)scene.PolygonOwners[polygon]=null;
                    else scene.PolygonOwners[polygon]=mesh;
                }
            }
            Scenes[model.Name]=scene;Console.WriteLine($"[JetMoto:lighting] Original scene {name}: {scene.Meshes.Count} mesh receivers, 1024x1024 static geometry height field.");
        }catch(Exception e) when(e is not OutOfMemoryException){Console.WriteLine($"[JetMoto:lighting] Original-render fallback for {model.Name}: {e.Message}");}
    }
    public static void BeginCamera(uint slot)
    {
        _epoch++;_cameraSlot=(int)slot;_camera=null;_lastInstance=null;_lastScene=null;Deferred.Clear();
        long v=Interrupts.VBlankCount;if(Widescreen.Active&&slot==0&&_lastVblank>0)_clock+=Math.Clamp(v-_lastVblank,0,30)/60.0;_lastVblank=v;
    }
    public static void PrepareEmission(PSMemory mem,uint list,uint count,uint rotations,IEnumerable<NativeTextures.Model> active)
    {
        if(!Widescreen.Active||_camera!=null||count==0||count>Widescreen.SceneCapacity||!mem.IsWorkMemoryRange(list,(long)count*32))return;
        // Calibrate BEFORE the first emitted water tile. Draw-list entries expose
        // the same object-relative translation and matrix slot consumed by
        // original 800DD348..800DD4E8. No GTE or gameplay state is modified.
        for(uint i=0;i<count;i++){
            uint entry=list+i*32,guest=mem.ReadU32(entry),slot=mem.ReadU32(entry+24);
            if(slot>=RenderArena.MatrixCapacity||!mem.IsWorkMemoryRange(rotations+slot*32,18))continue;
            foreach(var model in active){
                if(!Scenes.TryGetValue(model.Name,out var scene))continue;
                long mo=(long)(guest&0x1fffffff)-(model.Destination&0x1fffffff);
                if(mo<0||mo>int.MaxValue||!scene.Meshes.TryGetValue((int)mo,out var mesh)||mesh.Instances.Length!=1)continue;
                uint a=rotations+slot*32;float S(uint o)=>(short)mem.ReadU16(a+o)/4096f;
                var gr=new WorldBasis(S(0),S(2),S(4),S(6),S(8),S(10),S(12),S(14),S(16));if(!gr.IsCameraTransform)continue;
                var relative=new Vector3((int)mem.ReadU32(entry+4),(int)mem.ReadU32(entry+8),(int)mem.ReadU32(entry+12));
                var ins=mesh.Instances[0];var vr=gr*ins.Rotation.Transpose();var vt=gr.Apply(relative)/MathF.Pow(2,ins.Shift)-vr.Apply(ins.Translation);
                if(!vr.IsCameraTransform||!float.IsFinite(vt.X+vt.Y+vt.Z))continue;
                // All material scroll rates complete whole texture repeats at 1000s.
                _camera=new WorldCamera(scene.Gpu,vr,vt,(float)(_clock%1000));_calibrated++;
                if(_calibrated<8)Console.WriteLine($"[JetMoto:lighting:prepass] {scene.Gpu.Name} mesh={mo:X} slot={slot} eye={_camera.Eye}");
                return;
            }
        }
    }
    private static WorldBasis GteRotation(){uint a=Gte.ReadControl(0),b=Gte.ReadControl(1),c=Gte.ReadControl(2),d=Gte.ReadControl(3),e=Gte.ReadControl(4);return new((short)a/4096f,(short)(a>>16)/4096f,(short)b/4096f,(short)(b>>16)/4096f,(short)c/4096f,(short)(c>>16)/4096f,(short)d/4096f,(short)(d>>16)/4096f,(short)e/4096f);}
    private static Vector3 WaterTint(string scene)=>scene.StartsWith("ISLAND",StringComparison.Ordinal)?new(32/255f,16/255f,112/255f):
        scene.StartsWith("SWAMP",StringComparison.Ordinal)?new(20/255f,23/255f,5/255f):Vector3.Zero;
    private static WorldSurface? ResolveFillSurface(int x,int y,int w,int h,ushort color)
    {
        if(_camera==null||!_camera.Scene.Name.StartsWith("ISLAND",StringComparison.Ordinal)||w<300||h<200||y!=0)return null;
        float cx=(int)Gte.ReadControl(24)/65536f,cy=(int)Gte.ReadControl(25)/65536f,proj=(ushort)Gte.ReadControl(26);
        if(!(proj>0)){cx=x+w*.5f;cy=y+h*.5f;proj=320;}
        _faces++;_waterFaces++;
        return new WorldSurface(_camera,new Vector3(_camera.Eye.X,_camera.Eye.Y,-120f),Vector3.UnitZ,4,cx,cy,proj,screenFill:true);
    }
    private static WorldRectSurface? ResolveRectSurface(int x,int y,int w,int h)
    {
        if(_camera==null||!_camera.Scene.Name.StartsWith("ISLAND",StringComparison.Ordinal)||w<300||h<200||y!=0)return null;
        float cx=(int)Gte.ReadControl(24)/65536f,cy=(int)Gte.ReadControl(25)/65536f,proj=(ushort)Gte.ReadControl(26);
        if(!(proj>0)){cx=x+w*.5f;cy=y+h*.5f;proj=320;}
        float planeZ=-120f,delta=planeZ-_camera.Eye.Z;
        var inv=_camera.Rotation.Inverse;
        bool HitsWater(float sy)
        {
            var ray=inv.Apply(new(((x+w*.5f)-cx)/proj,(sy-cy)/proj,1));
            if(!float.IsFinite(ray.Z)||MathF.Abs(ray.Z)<1e-6f)return false;
            float depth=delta/ray.Z;
            return float.IsFinite(depth)&&depth>.02f&&depth<50000f;
        }
        int top;
        if(HitsWater(y))top=y;
        else if(!HitsWater(y+h-1))top=y+h/2;
        else{
            int lo=y,hi=y+h-1;
            for(int i=0;i<12&&hi-lo>1;i++){int mid=(lo+hi)/2;if(HitsWater(mid))hi=mid;else lo=mid;}
            top=hi;
        }
        top=Math.Clamp(top,y,y+h-1);
        if(y+h-top<8)return null;
        _faces++;_waterFaces++;
        var surface=new WorldSurface(_camera,new Vector3(_camera.Eye.X,_camera.Eye.Y,planeZ),Vector3.UnitZ,4,cx,cy,proj,screenFill:true);
        return new WorldRectSurface(surface,top,y+h-top);
    }
    public static WorldSurface? Resolve(NativeTextures.Model model,int offset,uint guestMesh)
    {
        if(!Widescreen.Active)return null;
        var rider = RiderGeometry.Resolve(model, offset, _camera, GteRotation());
        if (rider != null) return rider;
        if(!Scenes.TryGetValue(model.Name,out var scene))return null;
        _attempts++;
        long mo=(long)(guestMesh&0x1fffffff)-(model.Destination&0x1fffffff);
        if(mo<0||mo>int.MaxValue)return null;
        scene.Meshes.TryGetValue((int)mo,out var mesh);

        // Native UV animation replaces a mesh header's pointers with a selected
        // original frame. Source polygon addresses still identify that EXACT
        // frame; do not assume they remain owned by the original header slot.
        if(mesh==null||!mesh.Polygons.ContainsKey(offset))scene.PolygonOwners.TryGetValue(offset,out mesh);
        if(mesh==null)return null;
        _knownMesh++;if(!mesh.Polygons.TryGetValue(offset,out var polygon))return null;_knownPoly++;
        var gr=GteRotation();Vector3 gt=new((int)Gte.ReadControl(5),(int)Gte.ReadControl(6),(int)Gte.ReadControl(7));
        if(!gr.IsCameraTransform){_badRotation++;if(_dump++<5)Console.WriteLine($"[JetMoto:lighting:badR] mesh={mo:X} r={gr.A},{gr.B},{gr.C}/{gr.D},{gr.E},{gr.F}/{gr.G},{gr.H},{gr.I} T={gt}");return null;}

        Instance? instance=null;
        if(_lastMesh==(uint)mesh.Offset&&ReferenceEquals(_lastScene,scene)&&_lastR.Distance(gr)<.00001f&&_lastT==gt)instance=_lastInstance;
        else {
            if(_camera==null||!ReferenceEquals(_camera.Scene,scene.Gpu)){
                // Only an unambiguous original static instance may establish the
                // camera. Shared/animated source addresses cannot guess an origin.
                if(mesh.Instances.Length!=1)return null;
                instance=mesh.Instances[0];var vr=gr*instance.Rotation.Transpose();var vt=gt/MathF.Pow(2,instance.Shift)-vr.Apply(instance.Translation);
                if(!vr.IsCameraTransform||!float.IsFinite(vt.X)||!float.IsFinite(vt.Y)||!float.IsFinite(vt.Z))return null;
                _camera=new WorldCamera(scene.Gpu,vr,vt,(float)(_clock%1000));_calibrated++;
                if(_calibrated<8)Console.WriteLine($"[JetMoto:lighting:calibrate] {scene.Gpu.Name} mesh={mo:X} shift={instance.Shift} eye={_camera.Eye}");
            }else{
                float best=float.MaxValue;
                foreach(var ins in mesh.Instances){float er=(_camera.Rotation*ins.Rotation).Distance(gr);if(er>.04f)continue;var predicted=_camera.Rotation.Apply(ins.Translation)+_camera.Translation;float error=Vector3.Distance(predicted,gt/MathF.Pow(2,ins.Shift));if(error<best){best=error;instance=ins;}}
                if(best>4){_rejected++;if(_rejected<12)Console.WriteLine($"[JetMoto:lighting:mismatch] {mo:X} instances={mesh.Instances.Length} error={best:F3}");instance=null;}else _matched++;
            }
            _lastMesh=(uint)mesh.Offset;_lastScene=scene;_lastR=gr;_lastT=gt;_lastInstance=instance;
        }
        if(instance==null||_camera==null)return null;
        var point=instance.Rotation.Apply(polygon.Point/MathF.Pow(2,instance.Shift))+instance.Translation;
        var normal=instance.Rotation.Apply(polygon.Normal);if(!float.IsFinite(normal.X)||normal.LengthSquared()<.5f)return null;
        if(Math.Abs(normal.Z)>.85f&&normal.Z<0)normal=-normal;
        float x=(int)Gte.ReadControl(24)/65536f,y=(int)Gte.ReadControl(25)/65536f,h=(ushort)Gte.ReadControl(26);
        _faces++;if(polygon.Kind is 2 or 4)_waterFaces++;return new WorldSurface(_camera,point,normal,polygon.Kind,x,y,h);
    }
    public static void Defer(uint header,WorldSurface? surface){if(surface!=null)Deferred[header&0x1fffffff]=surface;}
    public static void FinalizeSubdivision(uint original,uint a,uint b,uint c,PSMemory mem)
    {
        if(!Deferred.Remove(original&0x1fffffff,out var surface))return;
        Span<uint> nodes=[original,a,b,c];Span<int> lengths=stackalloc int[4];
        for(int i=0;i<4;i++){uint n=nodes[i];if(!mem.IsWorkMemoryRange(n,8))return;int count=(int)(mem.ReadU32(n)>>24);byte op=(byte)(mem.ReadU32(n+4)>>24);if(op<0x20||op>0x3f||count!=NativeTextures.CommandLength(op)||!mem.IsWorkMemoryRange(n,4+count*4))return;lengths[i]=count;}
        for(int i=0;i<4;i++)WorldSurfaceBindings.Bind(nodes[i]+4,lengths[i],surface);
    }
}
