using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using RecompOne.Runtime;
using RecompOne.Runtime.Cdrom;
using RecompOne.Runtime.Config;
using RecompOne.Runtime.Diagnostics;
using RecompOne.Runtime.Memory;
using R = RecompOne.Runtime.Runtime;

namespace JetMoto;

internal static class Program
{
    private const string Build = "Jet Moto build 12 / RecompOne d81dec8c9622fdcd0865d73588a3baa8d3c3a605";
    private const string BootHash = "f1ad5aa4a092c9fc2a7f2d6795951a60a4419d02a0a3d0ea2a200fabc7a0ce48";
    // Full bundle extraction changes AppContext.BaseDirectory to the cache.
    // User data and artwork belong beside the executable, never in that cache.
    private static string GameDirectory =>
        Environment.ProcessPath is string processPath &&
        Path.GetFileNameWithoutExtension(processPath).Equals("JetMoto", StringComparison.OrdinalIgnoreCase)
            ? Path.GetDirectoryName(processPath)!
            : AppContext.BaseDirectory;
    private static int _stopReason;
    private static int _snapshotBusy;

    private static string? _lastError;

    public static int Main(string[] args)
    {
        // Automated validation must never wait for someone to dismiss a Windows dialog.
        bool showDialogs = !args.Any(a => a is "--validate-disc" or "--smoke-seconds" or "--no-dialogs" or "--headless");
        int result;
        try { result = Run(args); }
        catch (Exception e)
        {
            _lastError = e.Message;
            try { Console.Error.WriteLine("[JetMoto] Startup failure: " + e); } catch { }
            result = 1;
        }
        if (showDialogs && (result is 1 or 2) && OperatingSystem.IsWindows())
        {
            string message = (_lastError ?? "The game could not start. See the log for details.") +
                "\n\nGame folder:\n" + GameDirectory +
                "\n\nDiagnostic log, when available:\n" + Path.Combine(GameDirectory, "logs", "last-run.log");
            try { MessageBoxW(IntPtr.Zero, message, "Jet Moto", 0x00000010U); } catch { }
        }
        return result;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int MessageBoxW(IntPtr window, string text, string caption, uint type);

    private static void ReportError(string message)
    {
        _lastError = message;
        Console.Error.WriteLine("[JetMoto] " + message);
    }

    private static int Run(string[] args)
    {
        string? disc = null;
        int smokeSeconds = 0;
        bool validateOnly = false, trace = true, unlockAll = false;
        bool verbose = Environment.GetEnvironmentVariable("JETMOTO_VERBOSE") == "1";
        try
        {
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--help": case "-h":
                        Console.WriteLine(Build + "\nUsage: JetMoto.exe [game.cue] [--verbose] [--smoke-seconds 30]\n" +
                            "  No arguments: automatically load the Jet Moto CUE beside this executable.\n" +
                            "  --disc PATH       Override with an extracted CUE and all its BIN tracks.\n" +
                            "  --validate-disc   Validate the selected disc without opening a window.\n" +
                            "  --smoke-seconds N Stop after N seconds (exit 3; NOT a playability check).\n" +
                            "  --no-trace        Disable PC breadcrumbs; keeps cooperative stop checks.\n" +
                            "  --no-dialogs      Suppress Windows error dialogs for automated tools.\n" +
                            "  --headless        Render offscreen with no desktop input; implies --mute.\n" +
                            "  --mute            Disable audio output for this process only.\n" +
                            "  --unlockall       Make all 10 tracks selectable for this session; preserves saved unlock progress.\n" +
                            "Exit: 0=normal return, 1=error, 2=invalid arguments/disc, 3=smoke timeout, 130=Ctrl+C.");
                        return 0;
                    case "--disc":
                        if (++i >= args.Length) throw new ArgumentException("--disc needs a CUE path.");
                        if (disc != null) throw new ArgumentException("Specify only one disc.");
                        disc = Path.GetFullPath(args[i]); break;
                    case "--verbose": verbose = true; break;
                    case "--unlockall": unlockAll = true; break;
                    case "--no-dialogs": break;
                    case "--headless":
                        Environment.SetEnvironmentVariable("JETMOTO_HEADLESS", "1");
                        Environment.SetEnvironmentVariable("JETMOTO_MUTE", "1");
                        break;
                    case "--mute": Environment.SetEnvironmentVariable("JETMOTO_MUTE", "1"); break;
                    case "--no-trace": trace = false; break;
                    case "--validate-disc": validateOnly = true; break;
                    case "--smoke-seconds":
                        if (++i >= args.Length || !int.TryParse(args[i], out smokeSeconds) || smokeSeconds < 1 || smokeSeconds > 3600)
                            throw new ArgumentException("--smoke-seconds must be an integer from 1 to 3600.");
                        break;
                    default:
                        if (args[i].StartsWith('-') || disc != null) throw new ArgumentException($"Unexpected argument: {args[i]}");
                        disc = Path.GetFullPath(args[i]); break;
                }
            }
        }
        catch (Exception e) { ReportError(e.Message); return 2; }

        // Resolve supplied relative paths before changing the working directory. Keep saves/settings next to this build.
        Directory.SetCurrentDirectory(GameDirectory);
        Directory.CreateDirectory("logs");
        using var log = new StreamWriter("logs/last-run.log", false, new UTF8Encoding(false)) { AutoFlush = true };
        TextWriter oldOut = Console.Out, oldError = Console.Error;
        object gate = new();
        Console.SetOut(new TeeWriter(oldOut, log, gate));
        Console.SetError(new TeeWriter(oldError, log, gate));
        Console.WriteLine($"[{DateTimeOffset.Now:O}] {Build}\n[JetMoto] {RuntimeInformation.OSDescription}; .NET {Environment.Version}; {RuntimeInformation.ProcessArchitecture}");
        Console.WriteLine("[JetMoto] Bring-up build: successful compilation does not establish playable game support.");
        Console.WriteLine("[JetMoto] Renderer dithering: permanently disabled; no toggle.");
        Console.WriteLine("[JetMoto] Widescreen: gameplay Hor+ 16:9; front-end and pause menus 4:3; expanded side-plane visibility and full-width sky clears.");
        try
        {
            ConfigManager.Load();
            RecompOne.Runtime.Assets.Native.WorldSurfaceBindings.Enabled=true;
            Console.WriteLine("[JetMoto] World lighting: native geometry normals, static contact/sun shadow field; original-ID water highlights/ripples, no geometry movement. No screen-color filter.");
            RecompOne.Runtime.Pgxp.Pgxp.RequirePerspectiveCorrection();
            Console.WriteLine("[JetMoto] Perspective-correct textures + subpixel projection: always on; memory-proven depth only; original culling retained.");
            // Always discover next to the executable, ignoring an obsolete saved CdPath.
            // This also works when Explorer/shortcuts launch us from another working directory.
            if (disc == null)
            {
                try { disc = DiscLocator.FindLocalDisc(GameDirectory, ValidateDisc); }
                catch (Exception e) { ReportError(e.Message); return 2; }
            }
            if (disc != null)
            {
                string? error = ValidateDisc(disc);
                if (error != null) { ReportError(error); return 2; }
                disc = Path.GetFullPath(disc);
                // Save BEFORE Initialize; otherwise the first launch can leave an obsolete disc-picker modal open.
                ConfigManager.Game.CdPath = disc;
                ConfigManager.SaveGame();
                Console.WriteLine($"[JetMoto] Disc verified: SCUS_943.09 SHA-256 {BootHash}\n[JetMoto] CUE: {disc}");
            }
            if (validateOnly)
            {
                if (disc == null) { ReportError("No Jet Moto CUE was found beside the executable."); return 2; }
                return 0;
            }
            // Accepted Build 12 presentation is also the normal-launch default.
            // Explicit diagnostic overrides remain available to test harnesses.
            foreach (string feature in new[] { "JETMOTO_WORLD_WAKE", "JETMOTO_MENU_BACKGROUNDS" })
                if (Environment.GetEnvironmentVariable(feature) == null)
                    Environment.SetEnvironmentVariable(feature, "1");
            NativeTextures.Configure(disc!, GameDirectory);
            TrackAccess.Configure(unlockAll);
            ValidationReplay.Configure();
            Console.WriteLine("[JetMoto] Spray/roost: original-ID 4x replacement art; smooth moving coverage in the replacement shader.");
            Console.WriteLine("[JetMoto] Rider/moto LOD: highest original level permanently selected; original track LOD unchanged.");
            R.DiscValidator = ValidateDisc;
            ExecutionTrace.Enabled = trace;
            ExecutionTrace.StopRequested = false;
            Log.BiosOn = Log.SdkOn = Log.CdOn = verbose;
            _stopReason = 0;
            Console.CancelKeyPress += OnCancel;
            int result = 0;
            var watch = Stopwatch.StartNew();
            using var heartbeat = new Timer(_ => Snapshot(watch), null, 5000, 5000);
            using var deadline = new Timer(_ =>
            {
                Interlocked.CompareExchange(ref _stopReason, 3, 0);
                ExecutionTrace.StopRequested = true;
            }, null, smokeSeconds > 0 ? smokeSeconds * 1000 : Timeout.Infinite, Timeout.Infinite);
            R.SetMode(RunMode.Retail);
            try
            {
                R.Run(() =>
                {
                    try
                    {
                        R.Initialize("Jet Moto");
                        Console.WriteLine("[JetMoto] Starting original entry point 0x800EC310.");
                        NativeTextures.ResetActive();
                        Recompiled.Entry.Run(new PSMemory(), disc, "Jet Moto");
                        Console.WriteLine("[JetMoto] Game entry returned.");
                    }
                    catch (HardResetSignal) { throw; }
                    catch (ExecutionTrace.StopSignal)
                    {
                        result = Volatile.Read(ref _stopReason);
                        Console.WriteLine(result == 3 ? "[JetMoto] Smoke deadline reached; not a playability verdict." : "[JetMoto] Stop requested.");
                    }
                    catch (Exception e) { result = 1; ReportError("Runtime failure: " + e); }
                    finally { Snapshot(watch); try { NativeTextures.SaveUsage(GameDirectory); } catch (Exception e) { Console.WriteLine("[JetMoto:native-textures] Usage report: " + e.Message); } }
                });
            }
            finally
            {
                heartbeat.Change(Timeout.Infinite, Timeout.Infinite);
                deadline.Change(Timeout.Infinite, Timeout.Infinite);
                Console.CancelKeyPress -= OnCancel;
                try { R.Shutdown(); } catch (Exception e) { Console.Error.WriteLine("[JetMoto] Shutdown: " + e.Message); }
            }
            Console.WriteLine($"[JetMoto] Exit code: {result}. Logs: {Path.GetFullPath("logs")}");
            return result;
        }
        catch (Exception e) { ReportError("Failure: " + e); return 1; }
        finally { Console.SetOut(oldOut); Console.SetError(oldError); }
    }

    private static void OnCancel(object? sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true;
        Interlocked.CompareExchange(ref _stopReason, 130, 0);
        ExecutionTrace.StopRequested = true;
    }

    private static string? ValidateDisc(string path)
    {
        try
        {
            if (!File.Exists(path)) return "Disc not found: " + path;
            if (!Path.GetExtension(path).Equals(".cue", StringComparison.OrdinalIgnoreCase))
                return "Use the extracted .cue, not the .7z archive or an individual BIN track.";
            string directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
            foreach (Match match in Regex.Matches(File.ReadAllText(path), "^\\s*FILE\\s+\"([^\"]+)\"", RegexOptions.Multiline | RegexOptions.IgnoreCase))
            {
                string track = Path.Combine(directory, match.Groups[1].Value);
                if (!File.Exists(track)) return "Missing track: " + track;
            }
            using var fs = DiscFs.Open(path);
            if (fs.FirstTrack != 1 || fs.LastTrack != 14) return "This build expects the supplied USA disc layout: tracks 1 through 14.";
            string actual = Convert.ToHexString(SHA256.HashData(fs.ReadFile("SCUS_943.09"))).ToLowerInvariant();
            return actual == BootHash ? null : "Different executable revision. Expected SCUS_943.09 SHA-256 " + BootHash + "; got " + actual;
        }
        catch (Exception e) { return "Cannot validate disc: " + e.Message; }
    }

    private static void Snapshot(Stopwatch watch)
    {
        if (Interlocked.Exchange(ref _snapshotBusy, 1) != 0) return;
        try
        {
            var c = R.Cpu;
            string line = $"[JetMoto:heartbeat] {WorldLighting.Summary} {Widescreen.Diagnostics} {RiderDetail.Diagnostics} {RenderArena.Diagnostics} {NativeTextures.Summary} {RecompOne.Runtime.Pgxp.PerspectiveDiagnostics.Summary} seconds={watch.Elapsed.TotalSeconds:F0} vblanks={Interrupts.VBlankCount} " +
                $"breadcrumb=0x{ExecutionTrace.LastAddress:X8} lastFunction=0x{ExecutionTrace.LastFunction:X8} ra=0x{c?.RA:X8} sp=0x{c?.SP:X8}";
            Console.WriteLine(line);
            File.WriteAllText("logs/last-state.txt", line + "\nApproximate asynchronous breadcrumbs, not a stack trace.\nRecent functions: " +
                string.Join(" ", ExecutionTrace.RecentFunctions().Select(a => $"0x{a:X8}")) + Environment.NewLine);
        }
        catch (Exception e) { Console.Error.WriteLine("[JetMoto] Snapshot failed: " + e.Message); }
        finally { Volatile.Write(ref _snapshotBusy, 0); }
    }

    private sealed class TeeWriter(TextWriter console, TextWriter file, object gate) : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
        public override void Write(char value) { lock (gate) { console.Write(value); file.Write(value); } }
        public override void Write(string? value) { lock (gate) { console.Write(value); file.Write(value); } }
        public override void WriteLine(string? value) { lock (gate) { console.WriteLine(value); file.WriteLine(value); } }
        public override void Flush() { lock (gate) { console.Flush(); file.Flush(); } }
    }
}
