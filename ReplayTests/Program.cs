using JetMoto;
using RecompOne.Runtime.Hardware;

int passed=0;
void Check(bool value,string description) { if(!value) throw new Exception(description); Console.WriteLine("PASS: "+description); passed++; }
string path=Path.GetTempFileName();
try
{
    File.WriteAllText(path,"""[{"Start":0,"End":100,"Buttons":16384,"Clock":"race"},{"Start":10,"End":20,"Buttons":8}]""");
    Environment.SetEnvironmentVariable("JETMOTO_REPLAY",path);
    ValidationReplay.Configure();
    Interrupts.VBlankCount=15;
    Check(Controller.ReplayButtons!()==unchecked((ushort)~8),"race input inactive before race entry; menu input retained");
    Interrupts.VBlankCount=500;
    Check(Controller.ReplayButtons!()==ushort.MaxValue,"no premature race input at later global frames");
    Widescreen.RaceEntered!();
    Check(Controller.ReplayButtons!()==unchecked((ushort)~16384),"race input begins at race entry");
    Interrupts.VBlankCount=600;
    Check(Controller.ReplayButtons!()==ushort.MaxValue,"race interval end is exclusive");
    ValidationReplay.Configure();
    Check(Controller.ReplayButtons!()==ushort.MaxValue,"reconfiguration clears prior race start");
    File.WriteAllText(path,"""[{"Start":0,"End":100,"Buttons":8,"Clock":"typo"}]""");
    bool rejected=false;
    try { ValidationReplay.Configure(); } catch(InvalidDataException) { rejected=true; }
    Check(rejected,"unknown clock rejected");
    File.WriteAllText(path,"""[{"Start":0,"End":100,"Buttons":16384,"Clock":"race"},{"Start":20,"End":100,"Buttons":32,"Clock":"race","Player":2}]""");
    ValidationReplay.Configure();
    Check(Controller.ReplayButtons2!()==ushort.MaxValue,"second controller connected but idle before race");
    Widescreen.RaceEntered!();
    Interrupts.VBlankCount+=25;
    Check(Controller.ReplayButtons!()==unchecked((ushort)~16384) && Controller.ReplayButtons2!()==unchecked((ushort)~32),"independent player buttons share the race clock without cross-talk");
    File.WriteAllText(path,"""[{"Start":0,"End":100,"Buttons":8,"Player":3}]""");
    rejected=false;
    try { ValidationReplay.Configure(); } catch(InvalidDataException) { rejected=true; }
    Check(rejected,"unsupported player rejected");
    Environment.SetEnvironmentVariable("JETMOTO_REPLAY",null);
    ValidationReplay.Configure();
    Check(Widescreen.RaceEntered==null&&Controller.ReplayButtons==null&&Controller.ReplayButtons2==null,"production launch has no replay callback or input override for either player");
    Console.WriteLine($"RESULT: {passed} passed; 0 failed.");
}
finally { File.Delete(path); Environment.SetEnvironmentVariable("JETMOTO_REPLAY",null); }

namespace JetMoto { internal static class Widescreen { internal static Action? RaceEntered; } }
namespace RecompOne.Runtime { }
namespace RecompOne.Runtime.Hardware
{
    internal static class Interrupts { internal static long VBlankCount; }
    internal static class Controller { internal static Func<ushort>? ReplayButtons, ReplayButtons2; }
}
