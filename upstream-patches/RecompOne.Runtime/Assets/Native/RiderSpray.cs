using System.Numerics;

namespace RecompOne.Runtime.Assets.Native;

public readonly record struct SprayParticle(int RiderId, Vector3 Position, Vector3 Velocity, float Radius, float Opacity);

/// <summary>Deterministic ballistic droplets born along individual world paths.</summary>
public static class RiderSpray
{
    private static float Smooth(float a,float b,float value)
    {
        float t=Math.Clamp((value-a)/(b-a),0,1);return t*t*(3-2*t);
    }
    public static IReadOnlyList<SprayParticle> Evaluate(WorldCamera camera,IReadOnlyList<RiderPathSegment> paths)
    {
        var particles=new List<SprayParticle>();
        if(camera.Scene.FlatWaterLevel is not {} level)return particles;
        foreach(var path in paths)
        {
            float distance=path.DistanceB-path.DistanceA,dt=path.BornB-path.BornA;
            var forward=path.B-path.A;forward.Z=0;
            float length=forward.Length();
            if(distance<=0||dt<=0||length<.001f||length/dt<3||camera.Time-path.BornB>1.0f)continue;
            float speed=length/dt;
            float power=Smooth(3,45,speed);
            forward/=length;
            var side=new Vector3(-forward.Y,forward.X,0);
            for(int sample=(int)MathF.Ceiling(path.DistanceA/2);sample*2<path.DistanceB;sample++)
            {
                float t=(sample*2-path.DistanceA)/distance;
                float age=camera.Time-(path.BornA+dt*t);
                if(age<0||age>1.0f)continue;
                var birth=Vector3.Lerp(path.A,path.B,t);
                if(!camera.Scene.AllowsWaterEmission(birth))continue;
                float height=birth.Z-level;
                float contact=Smooth(0,1,height)*(1-Smooth(6,12,height));
                if(contact<=0)continue;
                for(int jet=-1;jet<=1;jet+=2)
                {
                    uint hash=unchecked((uint)(sample*73856093)^((uint)path.RiderId*19349663u)^(jet<0?83492791u:0u));
                    hash^=hash>>16;hash=unchecked(hash*0x7feb352du);hash^=hash>>15;
                    float seed=(hash&65535)/65535f;
                    float life=.65f+seed*.3f;
                    if(age>=life)continue;
                    // Airborne downwash carries craft momentum; surface foam
                    // remains in the separate world-anchored wake field.
                    var velocity=forward*(speed*.85f)+side*(jet*(8+seed*10)*power)+Vector3.UnitZ*(24+seed*12)*power;
                    birth.Z=level+.35f;
                    // Analytic drag keeps position and velocity continuous across render rates.
                    const float drag=1.8f,gravity=48;
                    float decay=MathF.Exp(-drag*age),response=(1-decay)/drag;
                    var position=birth+velocity*response-Vector3.UnitZ*(gravity*(age-response)/drag);
                    if(position.Z<=level+.1f)continue;
                    float opacity=contact*power*Smooth(0,.025f,age)*(1-Smooth(.12f,life,age))*.52f;
                    if(opacity<.01f)continue;
                    particles.Add(new(path.RiderId,position,velocity*decay-Vector3.UnitZ*(gravity*response),.10f+seed*.10f+age*.25f,opacity));
                }
            }
        }
        return particles;
    }
}
