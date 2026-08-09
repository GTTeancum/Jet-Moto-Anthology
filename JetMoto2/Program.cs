using RecompOne.Runtime.Memory;
using Recompiled;

string cue = args.Length > 0
    ? args[0]
    : Environment.GetEnvironmentVariable("JETMOTO2_CUE")
      ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
                      "JetMoto2_PS1image", "Jet Moto 2 (v1.1).cue");

var mem = new PSMemory();
Entry.Run(mem, File.Exists(cue) ? cue : null, "Jet Moto 2");
return 0;
