using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RecompOne.Recompiler.CodeGen;
using RecompOne.Recompiler.Config;
using RecompOne.Runtime.Cdrom;

namespace JetMotoLauncher;

/// <summary>
/// Turns the player's disc into a runnable assembly, here rather than at
/// packaging time.
///
/// This is the whole reason the release can exist: the recompiled executable is
/// the game's own program, so it cannot be shipped. Producing it on the
/// player's machine from the player's disc keeps the distribution free of game
/// code while still handing them something that just runs.
/// </summary>
static class Recompile
{
    public static Assembly LoadOrBuild(string disc, GameProfile game, bool rebuild)
    {
        string cacheDir = Path.Combine(AppContext.BaseDirectory, "cache");
        Directory.CreateDirectory(cacheDir);
        string key = CacheKey(disc, game);
        string cached = Path.Combine(cacheDir, $"{game.Key}-{key}.dll");

        if (!rebuild && File.Exists(cached))
        {
            Console.WriteLine("[Launcher] using cached build");
            return Assembly.LoadFrom(cached);
        }

        var sw = Stopwatch.StartNew();
        Console.WriteLine("[Launcher] first run for this disc -- recompiling it.");
        Console.WriteLine("[Launcher] this takes a minute; later launches are instant.");

        string work = Path.Combine(Path.GetTempPath(), "jetmoto-recomp-" + key);
        if (Directory.Exists(work)) Directory.Delete(work, true);
        Directory.CreateDirectory(work);

        try
        {
            var sources = Translate(disc, game, work);
            Console.WriteLine($"[Launcher] translated {sources.Count} file(s) in {sw.Elapsed.TotalSeconds:F0}s, compiling");
            Emit(sources, cached, game);
            Console.WriteLine($"[Launcher] ready in {sw.Elapsed.TotalSeconds:F0}s");
            return Assembly.LoadFrom(cached);
        }
        finally
        {
            try { Directory.Delete(work, true); } catch { }
        }
    }

    /// <summary>Run the recompiler against the disc, returning the emitted C#.</summary>
    static List<string> Translate(string disc, GameProfile game, string outDir)
    {
        // The configuration ships inside this executable and points at a disc
        // path that only existed on the machine it was authored on, so the cue
        // is rewritten to the player's disc before use.
        string cfgPath = Path.Combine(outDir, game.Resource);
        File.WriteAllText(cfgPath, PointConfigAtDisc(game.ReadConfig(), disc));

        var config = ConfigLoader.Load(cfgPath);
        config.Game.Output = outDir;

        using var fs = CueFs.Open(disc);
        OverlayWriter.Write(config, fs, outDir);

        return [.. Directory.EnumerateFiles(outDir, "*.cs", SearchOption.AllDirectories)];
    }

    static string PointConfigAtDisc(string json, string disc)
    {
        string quoted = System.Text.Json.JsonSerializer.Serialize(Path.GetFullPath(disc));
        return System.Text.RegularExpressions.Regex.Replace(
            json, "(\"cue\"\\s*:\\s*)\"(?:[^\"\\\\]|\\\\.)*\"",
            m => m.Groups[1].Value + quoted,
            System.Text.RegularExpressions.RegexOptions.None);
    }

    static void Emit(List<string> sources, string outputDll, GameProfile game)
    {
        var parse = new CSharpParseOptions(LanguageVersion.Latest);
        var trees = sources.AsParallel().Select(f =>
            CSharpSyntaxTree.ParseText(SourceText(f), parse, path: f)).ToList();

        // The port project builds with ImplicitUsings, and the generated code
        // relies on it -- Dictionary<,>, Action<,> and friends are never
        // imported explicitly. A raw compilation has no such thing, so supply
        // the same set.
        trees.Add(CSharpSyntaxTree.ParseText("""
            global using System;
            global using System.Collections.Generic;
            global using System.IO;
            global using System.Linq;
            global using System.Net.Http;
            global using System.Threading;
            global using System.Threading.Tasks;
            """, parse, path: "ImplicitUsings.cs"));

        var compilation = CSharpCompilation.Create(
            "Recompiled." + game.Key,
            trees.ToArray(),
            References(),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release,
                allowUnsafe: true,
                // the generated code is machine-written; its style warnings are noise
                specificDiagnosticOptions: new Dictionary<string, ReportDiagnostic>
                {
                    ["CS8632"] = ReportDiagnostic.Suppress,
                    ["CS0164"] = ReportDiagnostic.Suppress,
                    ["CS0219"] = ReportDiagnostic.Suppress,
                }));

        var result = compilation.Emit(outputDll);
        if (result.Success) return;

        File.Delete(outputDll);
        var errors = result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Take(10).Select(d => d.ToString());
        throw new InvalidOperationException(
            "compiling the recompiled game failed:\n  " + string.Join("\n  ", errors));
    }

    static Microsoft.CodeAnalysis.Text.SourceText SourceText(string path)
    {
        using var s = File.OpenRead(path);
        return Microsoft.CodeAnalysis.Text.SourceText.From(s, Encoding.UTF8);
    }

    /// <summary>
    /// Everything already loaded, which is the framework plus the RecompOne
    /// runtime the generated code calls into.
    /// </summary>
    static IEnumerable<MetadataReference> References()
    {
        // TRUSTED_PLATFORM_ASSEMBLIES is exactly the managed assembly set the
        // host was started with. Globbing the framework folder instead pulls in
        // native shims like System.IO.Compression.Native.dll, which Roslyn
        // rejects as "not a PE image with managed metadata".
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tpa = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string) ?? "";
        foreach (var p in tpa.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            if (File.Exists(p) && seen.Add(p))
                yield return MetadataReference.CreateFromFile(p);

        foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (a.IsDynamic || string.IsNullOrEmpty(a.Location)) continue;
            if (seen.Add(a.Location)) yield return MetadataReference.CreateFromFile(a.Location);
        }
    }

    /// <summary>
    /// Identifies a build: which port, and the disc's own executable. A
    /// different rip of the same game reuses the cache; a different game or a
    /// changed launcher does not.
    /// </summary>
    static string CacheKey(string disc, GameProfile game)
    {
        var h = SHA256.Create();
        void Feed(string s) { var b = Encoding.UTF8.GetBytes(s); h.TransformBlock(b, 0, b.Length, null, 0); }

        Feed(game.Key);
        Feed(game.ReadConfig());
        Feed(typeof(Recompile).Assembly.GetName().Version?.ToString() ?? "");
        try
        {
            using var fs = CueFs.Open(disc);
            var exe = fs.ReadFile("\\" + game.BootExe + ";1");
            var d = SHA256.HashData(exe.AsSpan(0, Math.Min(exe.Length, 1 << 20)));
            Feed(Convert.ToHexString(d));
        }
        catch { Feed(Path.GetFullPath(disc)); }

        h.TransformFinalBlock([], 0, 0);
        return Convert.ToHexString(h.Hash!)[..12].ToLowerInvariant();
    }
}
