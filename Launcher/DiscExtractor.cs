using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace JetMotoLauncher;

/// <summary>
/// Extract a disc image into a loose-file tree the port can boot from: the disc
/// root unwrapped, the CD-DA tracks as ogg, and a manifest under .disc/.
///
/// The port reads the disc by sector, not by file, so the tree keeps the
/// original LBAs and the runtime rebuilds a byte-faithful data track from it.
/// This mirrors tools/extract-disc.py, which is the reference implementation
/// and also carries a --verify mode.
/// </summary>
static class DiscExtractor
{
    const int Raw = 2352;
    const int User = 2048;

    sealed record Track(int Number, string Mode, string Bin, int Index0, int Index1, int Base)
    {
        public int StartLba => Base + Index1;
        public bool IsAudio => Mode == "AUDIO";
    }

    sealed record Entry(string Path, string Iso, int Lba, long Size, bool Raw, bool Audio);

    public static bool Run(string disc, string outDir, GameProfile game)
    {
        if (Directory.Exists(disc))
        {
            Console.Error.WriteLine("--extract needs a disc image; that is already a loose folder.");
            return false;
        }
        if (string.IsNullOrWhiteSpace(outDir))
            outDir = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(disc))!,
                                  game.Key == "jm1" ? "JetMoto_loose" : "JetMoto2_loose");

        var tracks = ParseCue(disc);
        var data = tracks.FirstOrDefault(t => !t.IsAudio);
        if (data is null) { Console.Error.WriteLine("no data track in the cue"); return false; }

        var meta = Path.Combine(outDir, ".disc");
        if (Directory.Exists(outDir) && Directory.EnumerateFileSystemEntries(outDir).Any())
        {
            // Only ever clear something this tool produced.
            if (!File.Exists(Path.Combine(meta, "disc.json")))
            {
                Console.Error.WriteLine($"{outDir} exists and is not a previous extraction; refusing to overwrite.");
                return false;
            }
            Directory.Delete(outDir, true);
        }
        Directory.CreateDirectory(meta);
        Directory.CreateDirectory(Path.Combine(outDir, "cdaudio"));

        using var img = File.OpenRead(data.Bin);
        int total = (int)(img.Length / Raw);
        Console.WriteLine($"[extract] data track: {total} sectors");

        var files = new List<Entry>();
        var covered = new bool[total];
        WalkIso(img, files, total);

        foreach (var f in files)
        {
            if (f.Audio) continue;
            int n = Math.Max(1, (int)((f.Size + User - 1) / User));
            for (int i = f.Lba; i < Math.Min(f.Lba + n, total); i++) covered[i] = true;
        }

        int written = 0;
        foreach (var f in files.Where(f => !f.Audio))
        {
            var dest = Path.Combine(outDir, f.Path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            ExtractFile(img, f, dest);
            written++;
        }
        int audioRefs = files.Count(f => f.Audio);
        Console.WriteLine($"[extract] {written} files, {audioRefs} CD-DA references");

        var ranges = WriteStructure(img, covered, total, Path.Combine(meta, "structure.bin"));
        Console.WriteLine($"[extract] structure: {ranges.Sum(r => r.Count)} sectors");

        var oggs = EncodeAudio(tracks, outDir);

        long leadout = tracks.Select(t => t.Bin).Distinct()
            .Where(File.Exists).Sum(b => new FileInfo(b).Length / Raw);

        WriteManifest(Path.Combine(meta, "disc.json"), disc, total, (int)leadout,
                      tracks, files, ranges, oggs);

        Console.WriteLine($"[extract] wrote {outDir}");
        Console.WriteLine("[extract] the port prefers this folder next time it starts.");
        return true;
    }

    // ---- ISO -------------------------------------------------------------

    static byte[] SectorUser(byte[] raw) =>
        raw[15] == 2 ? raw[24..(24 + User)] : raw[16..(16 + User)];

    static bool IsForm2(byte[] raw) => raw[15] == 2 && (raw[18] & 0x20) != 0;

    static byte[] ReadRaw(FileStream img, int lba)
    {
        var b = new byte[Raw];
        img.Position = (long)lba * Raw;
        img.ReadExactly(b);
        return b;
    }

    static void WalkIso(FileStream img, List<Entry> outList, int total)
    {
        var pvd = SectorUser(ReadRaw(img, 16));
        int rootLba = BitConverter.ToInt32(pvd, 156 + 2);
        int rootSize = BitConverter.ToInt32(pvd, 156 + 10);
        Walk(img, rootLba, rootSize, "", outList, total);
    }

    static void Walk(FileStream img, int lba, int size, string path, List<Entry> outList, int total)
    {
        var blob = new byte[((size + User - 1) / User) * User];
        for (int i = 0; i * User < blob.Length; i++)
            SectorUser(ReadRaw(img, lba + i)).CopyTo(blob, i * User);

        int off = 0;
        while (off < blob.Length)
        {
            int len = blob[off];
            if (len == 0)
            {
                off = (off / User + 1) * User;
                continue;
            }
            int ex = BitConverter.ToInt32(blob, off + 2);
            int sz = BitConverter.ToInt32(blob, off + 10);
            int flags = blob[off + 25];
            int nlen = blob[off + 32];
            var name = Encoding.Latin1.GetString(blob, off + 33, nlen);
            if (name != "\0" && name != "")
            {
                string child = path + "/" + name;
                if ((flags & 2) != 0) Walk(img, ex, sz, child, outList, total);
                else
                {
                    string rel = string.Join("/", child.Trim('/').Split('/')
                        .Select(p => p.Split(';')[0]));
                    bool audio = ex >= total;
                    bool raw = !audio && HasForm2(img, ex, sz, total);
                    outList.Add(new Entry(rel, child.Replace('/', '\\'), ex, sz, raw, audio));
                }
            }
            off += len;
        }
    }

    static bool HasForm2(FileStream img, int lba, long size, int total)
    {
        int n = Math.Max(1, (int)((size + User - 1) / User));
        for (int i = 0; i < n && lba + i < total; i++)
            if (IsForm2(ReadRaw(img, lba + i))) return true;
        return false;
    }

    static void ExtractFile(FileStream img, Entry f, string dest)
    {
        int n = Math.Max(1, (int)((f.Size + User - 1) / User));
        using var w = File.Create(dest);
        if (f.Raw)
        {
            // Form 2 carries 2324 bytes that no 2048-byte view can hold, so
            // those files are kept as whole sectors and served back verbatim.
            for (int i = 0; i < n; i++) w.Write(ReadRaw(img, f.Lba + i));
            return;
        }
        long left = f.Size;
        for (int i = 0; i < n && left > 0; i++)
        {
            var u = SectorUser(ReadRaw(img, f.Lba + i));
            int take = (int)Math.Min(User, left);
            w.Write(u, 0, take);
            left -= take;
        }
    }

    sealed record Range(int Lba, int Count);

    static List<Range> WriteStructure(FileStream img, bool[] covered, int total, string blobPath)
    {
        var ranges = new List<Range>();
        using var w = File.Create(blobPath);
        int start = -1, count = 0;

        void Flush()
        {
            if (start >= 0) ranges.Add(new Range(start, count));
            start = -1; count = 0;
        }

        for (int lba = 0; lba < total; lba++)
        {
            if (covered[lba]) { Flush(); continue; }
            var sec = ReadRaw(img, lba);
            if (SectorUser(sec).All(b => b == 0)) { Flush(); continue; }
            if (start < 0) start = lba;
            count++;
            w.Write(sec);
        }
        Flush();
        return ranges;
    }

    // ---- cue and audio ---------------------------------------------------

    static List<Track> ParseCue(string cuePath)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(cuePath))!;
        var tracks = new List<Track>();
        string? file = null;
        int fileBase = 0, number = 0; string mode = "";
        var idx = new Dictionary<int, int>();

        void Commit()
        {
            if (number == 0) return;
            tracks.Add(new Track(number, mode, file!,
                idx.GetValueOrDefault(0), idx.GetValueOrDefault(1), fileBase));
            number = 0; idx = [];
        }

        foreach (var raw in File.ReadLines(cuePath))
        {
            var line = raw.Trim();
            var m = Regex.Match(line, "^FILE\\s+\"(.+)\"", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                Commit();
                if (file is not null && File.Exists(file))
                    fileBase += (int)(new FileInfo(file).Length / Raw);
                file = Path.Combine(dir, m.Groups[1].Value);
                continue;
            }
            m = Regex.Match(line, @"^TRACK\s+(\d+)\s+(\S+)", RegexOptions.IgnoreCase);
            if (m.Success) { Commit(); number = int.Parse(m.Groups[1].Value); mode = m.Groups[2].Value.ToUpperInvariant(); continue; }
            m = Regex.Match(line, @"^INDEX\s+(\d+)\s+(\d+):(\d+):(\d+)", RegexOptions.IgnoreCase);
            if (m.Success && number != 0)
                idx[int.Parse(m.Groups[1].Value)] =
                    (int.Parse(m.Groups[2].Value) * 60 + int.Parse(m.Groups[3].Value)) * 75
                    + int.Parse(m.Groups[4].Value);
        }
        Commit();
        return tracks;
    }

    static Dictionary<int, (string Ogg, int Sectors)> EncodeAudio(List<Track> tracks, string outDir)
    {
        var result = new Dictionary<int, (string, int)>();
        var audio = tracks.Where(t => t.IsAudio).ToList();
        if (audio.Count == 0) return result;

        if (!HasFfmpeg())
        {
            Console.Error.WriteLine(
                "[extract] ffmpeg not found on PATH -- skipping the soundtrack.\n" +
                "[extract] the game still runs; install ffmpeg and re-extract for music.");
            return result;
        }

        foreach (var t in audio)
        {
            if (!File.Exists(t.Bin)) continue;
            string name = $"track{t.Number:D2}.ogg";
            string dest = Path.Combine(outDir, "cdaudio", name);
            long skip = (long)(t.Index1 - t.Index0) * Raw;   // the pregap inside the file
            long bytes = new FileInfo(t.Bin).Length - skip;

            var psi = new ProcessStartInfo("ffmpeg")
            {
                RedirectStandardInput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (var a in new[] { "-hide_banner", "-loglevel", "error", "-y",
                                      "-f", "s16le", "-ar", "44100", "-ac", "2",
                                      "-i", "pipe:0", "-c:a", "libvorbis", "-q:a", "6", dest })
                psi.ArgumentList.Add(a);

            using var p = Process.Start(psi)!;
            using (var src = File.OpenRead(t.Bin))
            {
                src.Position = skip;
                src.CopyTo(p.StandardInput.BaseStream, 1 << 20);
            }
            p.StandardInput.Close();
            p.WaitForExit();
            if (p.ExitCode != 0) { Console.Error.WriteLine($"[extract] track {t.Number}: ffmpeg failed"); continue; }

            result[t.Number] = ($"cdaudio/{name}", (int)(bytes / Raw));
            Console.WriteLine($"[extract] track {t.Number:D2} -> {name}");
        }
        return result;
    }

    static bool HasFfmpeg()
    {
        try
        {
            var psi = new ProcessStartInfo("ffmpeg", "-version")
            { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            using var p = Process.Start(psi)!;
            p.WaitForExit();
            return p.ExitCode == 0;
        }
        catch { return false; }
    }

    static void WriteManifest(string path, string source, int dataSectors, int leadout,
                              List<Track> tracks, List<Entry> files, List<Range> ranges,
                              Dictionary<int, (string Ogg, int Sectors)> oggs)
    {
        using var s = File.Create(path);
        using var w = new Utf8JsonWriter(s, new JsonWriterOptions { Indented = true });
        w.WriteStartObject();
        w.WriteNumber("version", 1);
        w.WriteString("source", Path.GetFileName(source));

        w.WriteStartObject("dataTrack");
        w.WriteNumber("sectors", dataSectors);
        w.WriteEndObject();
        w.WriteNumber("leadoutLba", leadout);

        w.WriteStartArray("tracks");
        foreach (var t in tracks)
        {
            if (t.IsAudio && !oggs.ContainsKey(t.Number)) continue;
            w.WriteStartObject();
            w.WriteNumber("number", t.Number);
            w.WriteString("type", t.IsAudio ? "audio" : "data");
            w.WriteNumber("startLba", t.StartLba);
            if (t.IsAudio)
            {
                w.WriteString("ogg", oggs[t.Number].Ogg);
                w.WriteNumber("sectors", oggs[t.Number].Sectors);
            }
            w.WriteEndObject();
        }
        w.WriteEndArray();

        w.WriteStartArray("files");
        foreach (var f in files)
        {
            w.WriteStartObject();
            w.WriteString("path", f.Path);
            w.WriteString("iso", f.Iso);
            w.WriteNumber("lba", f.Lba);
            w.WriteNumber("size", f.Size);
            if (f.Raw) w.WriteBoolean("raw", true);
            if (f.Audio) w.WriteBoolean("audio", true);
            w.WriteEndObject();
        }
        w.WriteEndArray();

        w.WriteStartObject("structure");
        w.WriteString("blob", "structure.bin");
        w.WriteStartArray("ranges");
        foreach (var r in ranges)
        {
            w.WriteStartArray();
            w.WriteNumberValue(r.Lba);
            w.WriteNumberValue(r.Count);
            w.WriteEndArray();
        }
        w.WriteEndArray();
        w.WriteEndObject();

        w.WriteEndObject();
    }
}
