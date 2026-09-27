using JetMoto;
using RecompOne.Runtime;
using RecompOne.Runtime.Hle;
using RecompOne.Runtime.Memory;
int passed = 0, failed = 0;
void Check(bool ok, string message) { if(ok) {passed++;Console.WriteLine("PASS " + message);} else {failed++;Console.WriteLine("FAIL " + message);} }
var mem = new PSMemory();
const uint context0 = 0x801D55C8, context1 = 0x801D5700;
mem.WriteU32(Widescreen.CameraPointers, context0);
mem.WriteU32(Widescreen.CameraPointers + 4, context1);
// Exact single-player and split-screen horizontal plane constants from SCUS_943.09.
var profiles = new[] { (id:0u, x:(short)3547, y:(short)2048), (id:1u,x:(short)3355,y:(short)2349), (id:9u,x:(short)3137,y:(short)2632), (id:2u,x:(short)3454,y:(short)2200), (id:3u,x:(short)3454,y:(short)2200), (id:5u,x:(short)3547,y:(short)2048), (id:6u,x:(short)3547,y:(short)2048) };
foreach(var p in profiles)
{
    uint s=Widescreen.ProfileTable + 0x60*p.id;
    mem.WriteU32(s+0x10,320);
    mem.WriteU16(s+0x30,unchecked((ushort)-p.x)); mem.WriteU16(s+0x32,(ushort)p.y);
    mem.WriteU16(s+0x38,(ushort)p.x); mem.WriteU16(s+0x3A,(ushort)p.y);
}
GpuHle.WideAspect=0;
Check(!Widescreen.Active,"initial menus are 4:3");
using(var race=Widescreen.EnterRace())
{
    Check(Widescreen.Active && Math.Abs(GpuHle.WideAspect-16f/9f)<1e-6,"race enters 16:9");
    using(var p=new Widescreen.DeferredMenuScope())
    {
        Check(Widescreen.Active,"ordinary per-frame pause check does not change aspect");
        p.Activate();p.Activate();
        Check(!Widescreen.Active && GpuHle.RetainDisplayMargins && GpuHle.WideAspect==Widescreen.GameplayAspect && GpuHle.TargetAspect==Widescreen.GameplayAspect,"pause freezes gameplay and preserves widescreen, activation idempotent");
        using(var nested=new Widescreen.DeferredMenuScope()) {nested.Activate();Check(!Widescreen.Active && GpuHle.WideAspect==Widescreen.GameplayAspect,"nested menus preserve widescreen");}
        Check(!Widescreen.Active,"closing nested menu does not resume the race early");
    }
    Check(Widescreen.Active && !GpuHle.RetainDisplayMargins,"closing pause resumes widescreen and ordinary margin clears");
    foreach(var p in profiles)
    foreach(uint camera in new uint[]{0,1})
    {
        uint context=camera==0?context0:context1;
        mem.WriteU32(Widescreen.CameraProfiles+4*camera,p.id);
        for(uint i=0;i<0x80;i++)mem.WriteU8(context+0x40+i,0x5A);
        byte[] before=Enumerable.Range(0,0x80).Select(i=>mem.ReadU8(context+0x40+(uint)i)).ToArray();
        Gte.WriteControl(26,190);Gte.WriteControl(24,160u<<16);Gte.WriteControl(25,120u<<16);
        Widescreen.PrepareCamera(mem,camera);
        byte[] after=Enumerable.Range(0,0x80).Select(i=>mem.ReadU8(context+0x40+(uint)i)).ToArray();
        var changed=new HashSet<int>(new[]{0,1,2,3,6,7,8,9,12,13,14,15});
        Check(Enumerable.Range(0,0x80).All(i=>changed.Contains(i)||before[i]==after[i]),$"profile {p.id} camera {camera}: near/far, vertical planes and camera transform untouched");
        Check(Gte.ReadControl(26)==190 && Gte.ReadControl(24)==160u<<16 && Gte.ReadControl(25)==120u<<16,$"profile {p.id}: projection/centre/vertical field of view unchanged");
        Widescreen.PrepareCamera(mem,camera);
        Check(after.SequenceEqual(Enumerable.Range(0,0x80).Select(i=>mem.ReadU8(context+0x40+(uint)i))),$"profile {p.id}: camera updates do not accumulate widening");
        short nx=(short)mem.ReadU16(context+0x40),ny=(short)mem.ReadU16(context+0x46);
        double oldLen=Math.Sqrt((double)p.x*p.x+(double)p.y*p.y),newLen=Math.Sqrt((double)nx*nx+(double)ny*ny);
        Check(Math.Abs(oldLen-newLen)<1.0,$"profile {p.id}: sphere-test normal length preserved");
        double visibleSlope=(double)p.y/p.x*(4.0/3.0);
        bool all=true;
        foreach(double z in new[]{10.0,100.0,10000.0})
        foreach(double side in new[]{-1.0,1.0})
        {
            double x=side*visibleSlope*z;
            // The two horizontal plane dot products at the widened visible edges.
            all &= nx*x+ny*z >= 0 && -nx*x+ny*z >= 0;
        }
        Check(all,$"profile {p.id}: both new viewport edges survive side-plane rejection at near/mid/far distances");
        Check(p.y- p.x*visibleSlope<0,$"profile {p.id}: original planes would reject the new edge (regression is meaningful)");
        using(var pause=new Widescreen.DeferredMenuScope())
        {
            pause.Activate();Widescreen.PrepareCamera(mem,camera);
            Check((short)mem.ReadU16(context+0x40)==-p.x && (short)mem.ReadU16(context+0x46)==p.y,$"profile {p.id}: menu camera restores exact original planes");
        }
    }
}
Check(!Widescreen.Active && !GpuHle.RetainDisplayMargins && GpuHle.WideAspect==0,"leaving race restores 4:3 and ordinary margin clears");
for(int i=0;i<100;i++) {using var r=Widescreen.EnterRace();using var m=new Widescreen.DeferredMenuScope();m.Activate();}
Check(!Widescreen.Active && GpuHle.WideAspect==0,"100 repeated race/menu transitions balance scopes");
try {using var r=Widescreen.EnterRace();throw new Exception("scope test");} catch{}
Check(!Widescreen.Active,"exception unwinding restores menu policy");
Console.WriteLine($"RESULT: {passed} passed; {failed} failed.");return failed==0?0:1;
