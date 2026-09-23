using System.Text;
using System.Text.Json;
// Reproducible, exact-anchor patching. All checks finish before the original file changes.
try
{
    if (args.Length != 1) throw new ArgumentException("Usage: GeneratedPatcher <build source root>");
    string root = Path.GetFullPath(args[0]);
    string path = Path.Combine(root, "JetMoto", "generated", "main.cs");
    string text = File.ReadAllText(path).Replace("\r\n", "\n");
    var hooks = JsonSerializer.Deserialize<Hook[]>(File.ReadAllText(Path.Combine(root, "generated-hooks.json")))!;
    int added = 0;
    foreach (var hook in hooks)
    {
        string marker = "        // JETMOTO-WIDE04 " + hook.name + "\n        " + hook.code + "\n";
        string anchor = "        " + hook.anchor + "\n";
        int Count(string value) => text.Split(value, StringSplitOptions.None).Length - 1;
        if (Count(anchor) != 1) throw new InvalidDataException("Anchor is missing or ambiguous: " + hook.name);
        string replacement = hook.position switch {
            "after" => anchor + marker, "before" => marker + anchor,
            _ => throw new InvalidDataException("Invalid hook position.") };
        if (text.Contains("// JETMOTO-WIDE04 " + hook.name + "\n"))
        {
            if (Count(replacement) != 1) throw new InvalidDataException("Existing hook was modified: " + hook.name);
        }
        else { text = text.Replace(anchor, replacement); added++; }
        Console.WriteLine("Verified hook: " + hook.name);
    }
    if (added > 0)
    {
        string tmp = path + ".wide04.tmp";
        File.WriteAllText(tmp, text, new UTF8Encoding(false));
        File.Move(tmp, path, true);
    }
    Console.WriteLine($"Generated hooks verified: {hooks.Length}; newly applied: {added}.");
    return 0;
}
catch (Exception ex) { Console.Error.WriteLine("Generated patching failed: " + ex.Message); return 1; }
record Hook(string name, string position, string anchor, string code);
