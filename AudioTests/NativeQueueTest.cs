using System.Reflection;
using RecompOne.Runtime;

static class NativeQueueTest
{
    public static void Run()
    {
        Environment.SetEnvironmentVariable("ALSOFT_DRIVERS", "null");
        Environment.SetEnvironmentVariable("JETMOTO_AUDIO_PROBE", "1");
        Environment.SetEnvironmentVariable("JETMOTO_MUTE", "1");
        Type host = typeof(Spu).Assembly.GetType("RecompOne.Runtime.Host.Audio", true)!;
        object? Field(string name) => host.GetField(name, BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null);
        void Call(string name, params object?[] values) => host.GetMethod(name, BindingFlags.Static | BindingFlags.Public)!.Invoke(null, values);
        var spu = new Spu();
        object sync = typeof(Spu).GetField("_sync", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(spu)!;
        Call("Initialize");
        try
        {
            Call("Attach", spu);
            Thread.Sleep(300);
            long before = (long)Field("_mixedFrames")!;
            long restarts = (long)Field("_restarts")!;
            for (int i = 0; i < 5; i++)
            {
                // Block only this test SPU, not the host OS or another process.
                lock (sync) Thread.Sleep(200);
                Thread.Sleep(200);
            }
            object queue = Field("Queue")!;
            int count = (int)queue.GetType().GetProperty("Queued")!.GetValue(queue)!;
            bool playing = (bool)queue.GetType().GetProperty("IsPlaying")!.GetValue(queue)!;
            if ((long)Field("_mixedFrames")! <= before || (long)Field("_restarts")! < restarts + 5 || count != 8 || !playing)
                throw new Exception("Native OpenAL queue failed to recover from injected mixer stalls.");
            Console.WriteLine("PASS: native OpenAL null device recovered from five 200 ms mixer stalls; eight buffers retained, playback advancing.");
        }
        finally { Call("Shutdown"); }
    }
}
