using Silk.NET.OpenAL;
using ALDevice = Silk.NET.OpenAL.Device;
using ALCtx = Silk.NET.OpenAL.Context;

namespace RecompOne.Runtime.Host;

internal static unsafe class Audio
{
    private static ALContext? _alc;
    private static AL? _al;
    private static ALDevice* _device;
    private static ALCtx* _context;


    private const int NumBuffers = 8;
    // About 93 ms total, allowing ordinary rendering/GC jitter without starvation.
    private const int FramesPerBuffer = 512;

    private static uint _source;
    private static uint[] _buffers = new uint[NumBuffers];
    private static short[] _sampleBuf = new short[FramesPerBuffer * 2];

    private static Thread? _mixerThread;
    private static volatile Spu? _spu;
    private static volatile bool _running;
    private static float _masterVolume = 1.0f;
    private static long _mixedFrames, _restarts;
    private static double _maxGapMs, _maxMixMs;
    private static readonly System.Diagnostics.Stopwatch _clock = new();
    private static MemoryStream? _capture;
    private static string? _capturePath;
    private const int CaptureBytes = 44100 * 4 * 180;

    public static void Initialize()
    {
        // A process-local null-device probe exercises mixing without host audio or input.
        bool probe = Environment.GetEnvironmentVariable("JETMOTO_AUDIO_PROBE") == "1" &&
            Environment.GetEnvironmentVariable("ALSOFT_DRIVERS") == "null";
        if (Environment.GetEnvironmentVariable("JETMOTO_MUTE") == "1" && !probe)
        {
            Console.WriteLine("[Host] Audio output disabled by process mute flag.");
            return;
        }
        try
        {
            _capturePath = Environment.GetEnvironmentVariable("JETMOTO_AUDIO_CAPTURE");
            if (!string.IsNullOrWhiteSpace(_capturePath)) _capture = new MemoryStream(CaptureBytes);
            _alc = ALContext.GetApi(true);
            _al = AL.GetApi(true);
            _device = _alc.OpenDevice("");
            if (_device == null)
            {
                Console.Error.WriteLine("[Host] no audio device, audio disabled");
                return;
            }

            _context = _alc.CreateContext(_device, null);
            _alc.MakeContextCurrent(_context);

            _source = _al.GenSource();
            _al.SetSourceProperty(_source, SourceFloat.Gain, _masterVolume);
            fixed (uint* ptr = _buffers)
            {
                _al.GenBuffers(NumBuffers, ptr);
            }

            // Prime playback with silence until the game attaches its SPU.
            Array.Clear(_sampleBuf);
            for (var i = 0; i < _buffers.Length; i++)
            {
                _al.BufferData(_buffers[i], BufferFormat.Stereo16, _sampleBuf, 44100);
                var b = _buffers[i];
                _al.SourceQueueBuffers(_source, 1, &b);
            }

            _al.SourcePlay(_source);

            _running = true;
            _mixedFrames = _restarts = 0;
            _maxGapMs = _maxMixMs = 0;
            _clock.Restart();
            _mixerThread = new Thread(MixerLoop) { IsBackground = true, Name = "spu-mixer", Priority = ThreadPriority.AboveNormal };
            _mixerThread.Start();
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"[Host] audio init failed: {e.Message}");
        }
    }

    public static void Attach(Spu? spu)
    {
        if (spu == null) return;
        _spu = spu;
        spu.VoiceGain = Config.ConfigManager.Game.SpuVolume;
        spu.XaGain = Config.ConfigManager.Game.XaVolume;
    }

    public static void Detach()
    {
        _spu = null;
    }

    public static void SetMasterVolume(float volume)
    {
        _masterVolume = Math.Clamp(volume, 0f, 1f);
        if (_al != null && _source != 0)
            _al.SetSourceProperty(_source, SourceFloat.Gain, _masterVolume);
    }

    private const int BufferMs = 2;

    private static void MixerLoop()
    {
        double previous = _clock.Elapsed.TotalMilliseconds, reportAt = 10000;
        while (_running)
        {
            double now = _clock.Elapsed.TotalMilliseconds;
            _maxGapMs = Math.Max(_maxGapMs, now - previous);
            previous = now;
            var spu = _spu;
            if (spu != null) FillBuffers(spu);
            if (now >= reportAt) { ReportAudio(); reportAt = now + 10000; }
            Thread.Sleep(spu != null ? BufferMs : 20);
        }
        ReportAudio();
    }

    private static void ReportAudio() => Console.WriteLine(
        $"[Host:audio] seconds={_clock.Elapsed.TotalSeconds:F1} frames={_mixedFrames} restarts={_restarts} maxGapMs={_maxGapMs:F1} maxMixMs={_maxMixMs:F1} cdBuffered={XaAudio.BufferedSamples} reverbOverflowCorrections={Spu.ReverbOverflowCorrections}");

    private static readonly OpenAlQueue Queue = new();

    private static void FillBuffers(Spu spu)
    {
        Queue.Spu = spu;
        if (AudioQueuePump.Service(Queue)) _restarts++;
    }

    private sealed class OpenAlQueue : IAudioQueue
    {
        public Spu Spu = null!;
        public bool IsPlaying {
            get { _al!.GetSourceProperty(_source, GetSourceInteger.SourceState, out var value); return value == (int)SourceState.Playing; }
        }
        public int Processed {
            get { _al!.GetSourceProperty(_source, GetSourceInteger.BuffersProcessed, out var value); return value; }
        }
        public int Queued {
            get { _al!.GetSourceProperty(_source, GetSourceInteger.BuffersQueued, out var value); return value; }
        }
        public void Stop() => _al!.SourceStop(_source);
        public uint Unqueue() {
            uint buffer = 0;
            _al!.SourceUnqueueBuffers(_source, 1, &buffer);
            return buffer;
        }
        public void FillAndQueue(uint buffer) {
            double beforeMix = _clock.Elapsed.TotalMilliseconds;
            Spu.Mix(_sampleBuf, FramesPerBuffer);
            if (_capture != null && _capture.Length + _sampleBuf.Length * 2 <= CaptureBytes)
                _capture.Write(System.Runtime.InteropServices.MemoryMarshal.AsBytes(_sampleBuf.AsSpan()));
            _maxMixMs = Math.Max(_maxMixMs, _clock.Elapsed.TotalMilliseconds - beforeMix);
            _mixedFrames += FramesPerBuffer;
            _al!.BufferData(buffer, BufferFormat.Stereo16, _sampleBuf, 44100);
            _al.SourceQueueBuffers(_source, 1, &buffer);
        }
        public void Play() => _al!.SourcePlay(_source);
    }
    public static void Shutdown()
    {
        if (_alc == null) return;
        _running = false;
        _mixerThread?.Join();
        if (_capture != null)
        {
            using var writer = new BinaryWriter(File.Create(_capturePath!));
            int bytes = (int)_capture.Length;
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + bytes);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
            writer.Write((short)1); writer.Write((short)2); writer.Write(44100); writer.Write(176400);
            writer.Write((short)4); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(bytes);
            writer.Write(_capture.GetBuffer(), 0, bytes);
            _capture.Dispose(); _capture = null;
        }
        if (_al != null)
        {
            _al.SourceStop(_source);
            _al.DeleteSource(_source);
            _al.DeleteBuffers(_buffers);
        }

        if (_context != null) _alc.DestroyContext(_context);
        if (_device != null) _alc.CloseDevice(_device);
    }
}
