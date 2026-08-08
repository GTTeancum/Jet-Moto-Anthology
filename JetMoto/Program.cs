using RecompOne.Runtime.Memory;
using Recompiled;

// Disc path: argv[0], else JETMOTO_CUE, else the copy checked into the repo tree.
// The runtime will prompt for a disc if none of these resolve.
string cue = args.Length > 0
    ? args[0]
    : Environment.GetEnvironmentVariable("JETMOTO_CUE")
      ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
                      "JetMotoPS1image", "Jet Moto (USA).cue");

var mem = new PSMemory();
Entry.Run(mem, File.Exists(cue) ? cue : null, "Jet Moto");
return 0;
