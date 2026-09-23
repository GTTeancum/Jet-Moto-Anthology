using System.Numerics;
using RecompOne.Runtime.Memory;

namespace RecompOne.Runtime.Assets.Native;

/// <summary>Small explicit row-major rotation, independent of host matrix conventions.</summary>
public readonly struct WorldBasis(float a,float b,float c,float d,float e,float f,float g,float h,float i)
{
    public readonly float A=a,B=b,C=c,D=d,E=e,F=f,G=g,H=h,I=i;
    public static WorldBasis Identity => new(1,0,0,0,1,0,0,0,1);
    public Vector3 Apply(Vector3 v)=>new(A*v.X+B*v.Y+C*v.Z,D*v.X+E*v.Y+F*v.Z,G*v.X+H*v.Y+I*v.Z);
    public WorldBasis Transpose()=>new(A,D,G,B,E,H,C,F,I);
    public static WorldBasis operator*(WorldBasis x,WorldBasis y){var t=y.Transpose();var v0=x.Apply(new(t.A,t.B,t.C));var v1=x.Apply(new(t.D,t.E,t.F));var v2=x.Apply(new(t.G,t.H,t.I));return new(v0.X,v1.X,v2.X,v0.Y,v1.Y,v2.Y,v0.Z,v1.Z,v2.Z);}
    public float Distance(WorldBasis o)=>MathF.Abs(A-o.A)+MathF.Abs(B-o.B)+MathF.Abs(C-o.C)+MathF.Abs(D-o.D)+MathF.Abs(E-o.E)+MathF.Abs(F-o.F)+MathF.Abs(G-o.G)+MathF.Abs(H-o.H)+MathF.Abs(I-o.I);
    public float Determinant=>A*(E*I-F*H)-B*(D*I-F*G)+C*(D*H-E*G);
    // The original engine scales projection rows (e.g. vertical 0.9). A camera
    // basis is invertible but not necessarily orthonormal; transposition is NOT
    // its inverse. Object instance rotations remain separately validated.
    public WorldBasis Inverse { get { float d=Determinant;return new((E*I-F*H)/d,(C*H-B*I)/d,(B*F-C*E)/d,(F*G-D*I)/d,(A*I-C*G)/d,(C*D-A*F)/d,(D*H-E*G)/d,(B*G-A*H)/d,(A*E-B*D)/d); } }
    public bool IsCameraTransform=>float.IsFinite(Determinant)&&MathF.Abs(Determinant)>.04f&&MathF.Abs(Determinant)<8&&float.IsFinite(A+B+C+D+E+F+G+H+I);
    public bool IsRotation=>(Transpose()*this).Distance(Identity)<.035f;
}

/// <summary>Verified original-track height data, never the screen framebuffer.</summary>
public sealed class WorldScene(string name,byte[] heightRgba,int size,Vector2 origin,Vector2 extent,Vector2 heightRange,Vector3 sunlight,Vector3? waterTint=null)
{
    public string Name {get;}=name;
    public byte[] HeightRgba {get;}=heightRgba;
    public int MapSize {get;}=size;
    public Vector2 Origin {get;}=origin;
    public Vector2 Extent {get;}=extent;
    public Vector2 HeightRange {get;}=heightRange;
    public Vector3 Sunlight {get;}=Vector3.Normalize(sunlight);
    public Vector3 WaterTint {get;}=waterTint ?? Vector3.Zero;
}

/// <summary>Immutable per-camera snapshot. Separate snapshots prevent split-screen contamination.</summary>
public sealed class WorldCamera(WorldScene scene,WorldBasis rotation,Vector3 translation,float time)
{
    public readonly List<ShadowTriangle> Casters = [];
    public WorldScene Scene {get;}=scene;
    public WorldBasis Rotation {get;}=rotation;
    public Vector3 Translation {get;}=translation;
    public Vector3 Eye {get;}=rotation.Inverse.Apply(-translation);
    public float Time {get;}=time;
}

public readonly record struct ShadowTriangle(Vector3 A, Vector3 B, Vector3 C);
public readonly record struct WorldRectSurface(WorldSurface Surface,int Y,int H);

/// <summary>Native source-primitive plane and camera, not a texture-color guess.</summary>
public sealed class WorldSurface(WorldCamera camera,Vector3 point,Vector3 normal,int kind,float centerX,float centerY,float projection,bool screenFill=false)
{
    private static readonly bool Diagnose = Environment.GetEnvironmentVariable("JETMOTO_DIAG_SURFACES") == "1";
    private static int _fallbackReports;
    public WorldCamera Camera {get;}=camera;
    public Vector3 Point {get;}=point;
    public Vector3 Normal {get;}=Vector3.Normalize(normal);
    public int Kind {get;}=kind;
    public float CenterX {get;}=centerX;
    public float CenterY {get;}=centerY;
    public float Projection {get;}=projection;
    public bool ScreenFill {get;}=screenFill;
    // Interpolate the plane in homogeneous coordinates, including rays at or
    // above its horizon. Dividing at vertices requires a discontinuous fallback.
    public bool ProjectiveAtPixel(float x,float y,out Vector4 worldQ)
    {
        worldQ=default;
        if (!(Projection>0)||!float.IsFinite(x+y)) return false;
        var ray=Camera.Rotation.Inverse.Apply(new((x-CenterX)/Projection,(y-CenterY)/Projection,1));
        float planeDistance=Vector3.Dot(Point-Camera.Eye,Normal);
        if (MathF.Abs(planeDistance)<1e-6f) return false;
        float q=Vector3.Dot(ray,Normal)/planeDistance;
        var numerator=Camera.Eye*q+ray;
        worldQ=new(numerator,q);
        return float.IsFinite(numerator.X+numerator.Y+numerator.Z+q);
    }
    public bool AtPixel(float x,float y,out Vector3 world,out float inverseDepth)
    {
        world=default;inverseDepth=0;
        if(!(Projection>0) || !float.IsFinite(x)||!float.IsFinite(y))return false;
        Vector3 ray=Camera.Rotation.Inverse.Apply(new((x-CenterX)/Projection,(y-CenterY)/Projection,1));
        float denominator=Vector3.Dot(ray,Normal);
        if(MathF.Abs(denominator)<1e-6f)return ScreenFill && FillFallback(ray,out world,out inverseDepth);
        float depth=Vector3.Dot(Point-Camera.Eye,Normal)/denominator;
        if(!float.IsFinite(depth)||depth<.02f||depth>50000)return ScreenFill && FillFallback(ray,out world,out inverseDepth);
        world=Camera.Eye+ray*depth;inverseDepth=1f/depth;
        return float.IsFinite(world.X)&&float.IsFinite(world.Y)&&float.IsFinite(world.Z);
    }
    private bool FillFallback(Vector3 ray,out Vector3 world,out float inverseDepth)
    {
        if (Diagnose && _fallbackReports++ < 16)
            Console.WriteLine($"[JetMoto:water-fallback] eye={Camera.Eye} plane={Point} ray={ray} time={Camera.Time}");
        const float depth=3200f;
        world=new(Camera.Eye.X+ray.X*depth,Camera.Eye.Y+ray.Y*depth,Point.Z);
        inverseDepth=1f/depth;
        return float.IsFinite(world.X)&&float.IsFinite(world.Y)&&float.IsFinite(world.Z);
    }
}

/// <summary>Separate provenance for verified world polygons, including untextured ones.
/// Payload mutations invalidate old identity. OT headers are deliberately not tagged.</summary>
public static class WorldSurfaceBindings
{
    public static bool Enabled;
    public static long Bound, Resolved, Invalidated, LitTriangles, WaterTriangles, RiderTriangles, FillAttempts, FillResolved, RectAttempts, RectResolved;
    public static Func<int,int,int,int,ushort,WorldSurface?>? FillSurfaceProvider;
    public static Func<int,int,int,int,WorldRectSurface?>? RectSurfaceProvider;
    private sealed class Token(uint offset,WorldSurface surface){public readonly uint Offset=offset;public readonly WorldSurface Surface=surface;public bool Valid=true;}
    private static Token?[] _words=[];private static uint _mask,_hostSize;
    public static void Init(uint size){if(size<0x200000||size>0x800000||(size&(size-1))!=0)throw new ArgumentOutOfRangeException(nameof(size));_mask=size-1;_hostSize=0;_words=new Token?[size/4];}
    public static void ConfigureHostScratch(uint size){if(size==0||size>MemoryMap.HostScratchMaxSize||(size&3)!=0||_words.Length==0)throw new ArgumentOutOfRangeException(nameof(size));if(_hostSize==size)return;if(_hostSize!=0)throw new InvalidOperationException("World provenance workspace cannot be resized");Array.Resize(ref _words,checked((int)((_mask+1+size)/4)));_hostSize=size;}
    private static bool Address(uint a,out uint offset,out uint remaining){offset=remaining=0;if(_words.Length==0)return false;uint p=a&0x1fffffff;if(p<MemoryMap.RamWindow){offset=p&_mask;remaining=_mask+1-offset;return true;}uint h=p-MemoryMap.HostScratchBase;if(h>=_hostSize)return false;offset=_mask+1+h;remaining=_hostSize-h;return true;}
    public static void Invalidate(uint address,int bytes){if(!Enabled||bytes<=0)return;for(int n=0;n<bytes;){if(!Address(address+(uint)n,out uint offset,out _))return;var t=_words[offset>>2];if(t!=null){if(t.Valid)Invalidated++;t.Valid=false;_words[offset>>2]=null;}n+=Math.Min(bytes-n,4-(int)(offset&3));}}
    public static bool Bind(uint address,int words,WorldSurface surface){if(!Enabled||(address&3)!=0||words<1||words>16||!Address(address,out uint offset,out uint remaining)||(uint)words*4>remaining)return false;var t=new Token(offset,surface);for(int i=0;i<words;i++){int slot=(int)(offset>>2)+i;if(_words[slot] is {} old)old.Valid=false;_words[slot]=t;}Bound++;return true;}
    public static WorldSurface? Resolve(uint address){if(!Enabled||(address&3)!=0||!Address(address,out uint offset,out _))return null;var t=_words[offset>>2];if(t is not {Valid:true}||t.Offset!=offset)return null;Resolved++;return t.Surface;}
    public static WorldSurface? ResolveFill(int x,int y,int w,int h,ushort color)
    {
        if(!Enabled)return null;
        FillAttempts++;
        var surface=FillSurfaceProvider?.Invoke(x,y,w,h,color);
        if(surface!=null)FillResolved++;
        return surface;
    }
    public static WorldRectSurface? ResolveRect(int x,int y,int w,int h)
    {
        if(!Enabled)return null;
        RectAttempts++;
        var surface=RectSurfaceProvider?.Invoke(x,y,w,h);
        if(surface!=null)RectResolved++;
        return surface;
    }
    public static string Summary=>$"world[bound={Bound},resolved={Resolved},litTri={LitTriangles},waterTri={WaterTriangles},riderTri={RiderTriangles},fill={FillResolved}/{FillAttempts},rect={RectResolved}/{RectAttempts}]";
}
