using System.Security.Cryptography;
using RecompOne.Runtime.Cdrom;
using RecompOne.Runtime.Context;
using RecompOne.Runtime.Memory;
using RecompOne.Runtime.Sdk;
using RecompOne.Runtime.Dispatch;
using R = RecompOne.Runtime.Runtime;

if (args.Length != 1) { Console.Error.WriteLine("Usage: CdReadRegression <Jet Moto USA.cue>"); return 2; }
try
{
    using var fs = DiscFs.Open(Path.GetFullPath(args[0]));
    const string expectedBoot = "f1ad5aa4a092c9fc2a7f2d6795951a60a4419d02a0a3d0ea2a200fabc7a0ce48";
    Check(Convert.ToHexString(SHA256.HashData(fs.ReadFile("SCUS_943.09"))).Equals(expectedBoot, StringComparison.OrdinalIgnoreCase), "correct boot executable");
    var m = new PSMemory(); var c = new CpuContext();
    m.SetCd(new CdController(fs, m)); R.SetContext(c, m);
    LibCd.CdInit(c, m); Check(c.V0 == 1, "CD initialized");
    const uint file = 0x80004000, name = 0x80003000, buffer = 0x80010000, result = 0x80005000;
    string path = @"\STARTUP\SCEAPRES.BS;1";
    for (uint i = 0; i < path.Length; i++) m.WriteU8(name + i, (byte)path[(int)i]);
    m.WriteU8(name + (uint)path.Length, 0);
    c.A0 = file; c.A1 = name; LibCd.CdSearchFile(c, m); Check(c.V0 == file, "startup asset located");
    var expected = fs.ReadFile(path); Check(m.ReadU32(file + 4) == expected.Length, "file size matches filesystem");
    c.A0 = 2; c.A1 = file; c.A2 = 0; LibCd.CdControl(c, m); Check(c.V0 == 1, "Setloc accepted");
    c.A0 = (uint)((expected.Length + 2047) / 2048); c.A1 = buffer; c.A2 = 0x81;
    LibCd.CdRead(c, m); Check(c.V0 == 1, "bulk read succeeded");
    Check(m.Ram.Slice((int)(buffer & 0x1fffff), expected.Length).SequenceEqual(expected), "every asset byte matches the original disc");
    c.A0 = 0; c.A1 = result; LibCd.CdReadSync(c, m);
    Check(c.V0 == 0, "read synchronization completed");
    Check(m.ReadU8(result) == 0x22, "completed read status is motor+read (0x22), not stale 0x02");
    var callback = new ReadyCallbackOverlay();
    Dispatcher.Register(callback.Name, callback); Dispatcher.Load(callback.Name);
    c.A0 = 0x80006000; LibCd.CdReadyCallback(c, m);
    c.A0 = 6; c.A1 = 0; c.A2 = 0; LibCd.CdControl(c, m);
    c.A0 = 1; c.A1 = result; LibCd.CdSync(c, m);
    Check(callback.Calls > 0, "ready callback actually ran during sync");
    Check(c.V0 == 2, "data-ready interrupt does not overwrite command-complete result");
    c.A0 = 9; c.A1 = 0; c.A2 = 0; LibCd.CdControl(c, m);
    Console.WriteLine("PASS: all CD regression checks passed.");
    return 0;
}
catch (Exception e) { Console.Error.WriteLine("FAIL: " + e); return 1; }
static void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); Console.WriteLine("PASS: " + name); }

sealed class ReadyCallbackOverlay : IOverlay
{
    public string Name => "cd-regression";
    public int Calls;
    public IReadOnlyDictionary<uint, Action<CpuContext, IMemory>> Functions { get; }
    public ReadyCallbackOverlay() => Functions = new Dictionary<uint, Action<CpuContext, IMemory>>
    { [0x80006000] = (c, m) => { if (c.A0 != 1) throw new InvalidOperationException("Expected DataReady=1"); Calls++; } };
}
