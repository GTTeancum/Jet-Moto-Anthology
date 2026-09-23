using JetMoto;

string root = Path.Combine(Path.GetTempPath(), "Jet Moto launcher tests " + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
string originalCwd = Directory.GetCurrentDirectory();
int passed = 0;
try
{
    Test("no CUE gives actionable error", d => ExpectThrows<FileNotFoundException>(() => Find(d), "No CUE"));
    Test("arbitrary single valid filename", d => { Put(d, "my disc.cue"); Equal(Find(d), Path.Combine(d, "my disc.cue")); });
    Test("CUE extension is case insensitive", d => { Put(d, "DISC.CUE"); Equal(Find(d), Path.Combine(d, "DISC.CUE")); });
    Test("preferred USA name wins among valid discs", d => {
        Put(d, "copy.cue"); Put(d, "Jet Moto (USA).cue"); Equal(Find(d), Path.Combine(d, "Jet Moto (USA).cue")); });
    Test("preferred filename is case insensitive", d => {
        Put(d, "copy.cue"); Put(d, "JET MOTO (USA).CUE"); Equal(Find(d), Path.Combine(d, "JET MOTO (USA).CUE")); });
    Test("invalid preferred disc never masks valid disc", d => {
        Put(d, "Jet Moto (USA).cue", "invalid"); Put(d, "backup.cue"); Equal(Find(d), Path.Combine(d, "backup.cue")); });
    Test("two nonpreferred matching discs are ambiguous", d => {
        Put(d, "a.cue"); Put(d, "b.cue"); ExpectThrows<InvalidOperationException>(() => Find(d), "More than one"); });
    Test("invalid layout/revision reports reason", d => {
        Put(d, "Jet Moto (USA).cue", "invalid"); ExpectThrows<InvalidDataException>(() => Find(d), "wrong revision or missing track"); });
    Test("other working directory cannot select another disc", d => {
        string other = Path.Combine(d, "wrong working directory"); Directory.CreateDirectory(other);
        Put(other, "wrong.cue"); Put(d, "right.cue"); Directory.SetCurrentDirectory(other);
        try { Equal(Find(d), Path.Combine(d, "right.cue")); } finally { Directory.SetCurrentDirectory(originalCwd); } });
    Test("nested CUE is not selected", d => {
        string sub = Path.Combine(d, "sub"); Directory.CreateDirectory(sub); Put(sub, "game.cue");
        ExpectThrows<FileNotFoundException>(() => Find(d), "No CUE"); });
    Test("stale settings are ignored", d => {
        Put(d, "settings.json", "{ \"CdPath\": \"Z:/old/game.cue\" }"); Put(d, "local.cue");
        Equal(Find(d), Path.Combine(d, "local.cue")); });
    Test("validator exceptions become clear errors", d => {
        Put(d, "broken.cue"); ExpectThrows<InvalidDataException>(() => DiscLocator.FindLocalDisc(d, _ => throw new IOException("unreadable")), "unreadable"); });
    Test("folder can be relocated", d => {
        Put(d, "local.cue"); string moved = d + " moved"; Directory.Move(d, moved);
        Equal(Find(moved), Path.Combine(moved, "local.cue")); });
    Test("BIN without CUE is not guessed", d => { Put(d, "track01.bin"); ExpectThrows<FileNotFoundException>(() => Find(d), "No CUE"); });
    Console.WriteLine($"PASS: {passed} launcher regression tests.");
    return 0;
}
catch (Exception e) { Console.Error.WriteLine(e); return 1; }
finally { Directory.SetCurrentDirectory(originalCwd); Directory.Delete(root, true); }

void Test(string name, Action<string> action)
{
    string directory = Path.Combine(root, "case-" + passed); Directory.CreateDirectory(directory);
    action(directory); ++passed; Console.WriteLine("PASS: " + name);
}
static string Find(string d) => DiscLocator.FindLocalDisc(d, p => File.ReadAllText(p) == "valid" ? null : "wrong revision or missing track");
static void Put(string d, string file, string contents = "valid") => File.WriteAllText(Path.Combine(d, file), contents);
static void Equal(string actual, string expected) { if (actual != expected) throw new Exception($"Expected {expected}; got {actual}"); }
static void ExpectThrows<T>(Func<string> action, string message) where T : Exception
{
    try { action(); }
    catch (T e) when (e.Message.Contains(message, StringComparison.Ordinal)) { return; }
    throw new Exception("Expected " + typeof(T).Name + " containing " + message);
}
