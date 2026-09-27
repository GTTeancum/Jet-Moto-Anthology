using RecompOne.Runtime.Host;

int checks = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; Console.WriteLine("PASS: " + name); }
var steady = new QueueModel(true, 2);
Check(!AudioQueuePump.Service(steady) && steady.Fills == 2 && steady.Starts == 0, "running queue replaces only consumed buffers");
var idle = new QueueModel(true, 0);
Check(!AudioQueuePump.Service(idle) && idle.Fills == 0, "unconsumed audio remains untouched");
var stopped = new QueueModel(false, 8);
Check(AudioQueuePump.Service(stopped) && stopped.Queued == 8 && stopped.Fills == 8 && stopped.StaleAtPlay == 0,
    "stalled queue restarts with eight fresh buffers");
var late = new QueueModel(true, 2) { StallOnFill = 1 };
Check(AudioQueuePump.Service(late) && late.StaleAtPlay == 0 && late.DrainedBeforeRefill,
    "stall during refill drains stale audio before restart");
var old = new QueueModel(true, 2) { StallOnFill = 1 };
int processed = old.Processed;
for (int i = 0; i < processed; i++) old.FillAndQueue(old.Unqueue());
if (!old.IsPlaying) old.Play();
Check(old.StaleAtPlay == 6, "old recovery reproduces six stale buffers after the same stall");
for (int i = 0; i < 1000; i++) { stopped.ForceStall(); AudioQueuePump.Service(stopped); }
Check(stopped.Queued == 8 && stopped.StaleAtPlay == 0, "repeated recovery neither grows the queue nor replays stale buffers");
var empty = new QueueModel(false, 0, 0);
Check(!AudioQueuePump.Service(empty) && empty.Starts == 0, "empty detached queue is not restarted");
var multiply = typeof(RecompOne.Runtime.Spu).GetMethod("RevMul", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
int Reverb(int sample, short gain) => (int)multiply.Invoke(null, new object[] { sample, gain })!;
Check(Reverb(98304, 32767) == 98301, "loud positive reverb input must not wrap negative");
Check(Reverb(-98304, 32767) == -98301, "loud negative reverb input must not wrap positive");
Check(Reverb(786408, -32768) == -786408, "24-voice reverb sum retains its polarity and magnitude");
Check(Reverb(16384, 16384) == 8192, "ordinary half-gain reverb remains unchanged");
Console.WriteLine($"PASS: {checks} audio regression checks.");
if (args.Contains("--device")) NativeQueueTest.Run();

sealed class QueueModel : IAudioQueue
{
    private readonly Queue<(uint Id, bool Fresh)> _buffers = new();
    private int _processed;
    private bool _draining;
    public bool IsPlaying { get; private set; }
    public int Processed => _processed;
    public int Queued => _buffers.Count;
    public int Fills, Starts, StaleAtPlay, StallOnFill;
    public bool DrainedBeforeRefill = true;
    public QueueModel(bool playing, int processed, int count = 8) {
        IsPlaying = playing; _processed = processed;
        for (uint i = 0; i < count; i++) _buffers.Enqueue((i, false));
    }
    public void ForceStall() { IsPlaying = false; _processed = Queued; }
    public void Stop() { ForceStall(); _draining = true; }
    public uint Unqueue() {
        if (_processed <= 0) throw new Exception("Unqueue before consumption");
        _processed--; return _buffers.Dequeue().Id;
    }
    public void FillAndQueue(uint buffer) {
        if (_draining) { DrainedBeforeRefill &= Queued == 0; _draining = false; }
        if (++Fills == StallOnFill) ForceStall();
        _buffers.Enqueue((buffer, true));
    }
    public void Play() {
        StaleAtPlay = _buffers.Count(x => !x.Fresh);
        Starts++; IsPlaying = true; _processed = 0;
    }
}
