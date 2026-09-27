using System.Numerics;
using RecompOne.Runtime.Assets.Native;
using RecompOne.Runtime.Hle;
using Silk.NET.OpenGL;

static class LightingTiming
{
    public static void Run(GlCore core, GL gl, string label, Action<bool,string> check)
    {
        // GL 3.3/4.5 core timer queries. No clock inference from emulated vblanks.
        foreach(int scenario in new[]{0,1,2})
        {
            bool blocked=scenario==1;
            const int size=128;
            byte[] height=new byte[size*size*4];
            for(int i=0;i<height.Length;i+=4){height[i]=blocked?(byte)160:(byte)0;height[i+2]=255;height[i+3]=255;}
            var scene=new WorldScene("timing",height,size,new(-2048),new(4096),new(0,256),new(-.65f,.05f,.76f));
            var camera=new WorldCamera(scene,new(1,0,0,0,-1,0,0,0,-1),new(0,0,64),0);
            if(scenario==2) {
                camera.Casters.Add(new(new(-256,-256,16),new(256,-256,16),new(-256,256,16)));
                camera.Casters.Add(new(new(256,-256,16),new(256,256,16),new(-256,256,16)));
            }
            var surface=new WorldSurface(camera,Vector3.Zero,Vector3.UnitZ,1,160,120,160);
            var flags=new PrimFlags{WorldSurface=surface,Gouraud=true};
            HleVertex V(float x,float y)=>new(){X=x,Y=y,R=160,G=150,B=140};
            void Draw(){core.DrawTri(V(0,0),V(320,0),V(0,240),flags);core.DrawTri(V(320,0),V(320,240),V(0,240),flags);core.Flush();}
            core.SetDrawEnv(new HleDrawEnv{ClipX1=319,ClipY1=239});
            // Warm resources outside the query.
            for(int i=0;i<12;i++)Draw();
            gl.Finish();
            double Measure()
            {
                uint query=gl.GenQuery();gl.BeginQuery(QueryTarget.TimeElapsed,query);
                for(int i=0;i<40;i++)Draw();
                gl.EndQuery(QueryTarget.TimeElapsed);
                gl.GetQueryObject(query,QueryObjectParameterName.Result,out ulong nanos);
                gl.DeleteQuery(query);
                return nanos/1_000_000.0/40;
            }
            var samples=new List<double>();
            for(int i=0;i<4;i++) samples.Add(Measure());
            samples.Sort();
            double elapsed=(samples[1]+samples[2])*.5;
            Console.WriteLine($"GPU TIMING {label} {(scenario==2?"rider-shadow":blocked?"shadowed":"sunlit")}: {elapsed:F4} ms per 320x240 draw");
            check(elapsed>0&&double.IsFinite(elapsed),label+": GPU timer returned valid lighting measurements");
        }
    }
}
