using System.Numerics;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;

namespace JetMotoLauncher;

/// <summary>
/// A small window shown while first-run work happens, so double-clicking the
/// program never leaves a blank screen.
///
/// Extracting a disc moves about half a gigabyte and recompiling its executable
/// takes a minute; both used to report progress by writing to the console. That
/// is fine for a developer running it from a terminal and useless for anyone
/// else -- and once the program is a windowed application there is no console
/// for the text to go to at all. So the same progress goes here instead.
///
/// It is deliberately its own window rather than part of the game's: it has to
/// exist before the recompiled assembly does, and the game's host window is
/// built around the emulator loop.
/// </summary>
static class SetupWindow
{
    /// <summary>
    /// Show the window and run <paramref name="work"/> on a worker thread,
    /// pumping the window until it finishes. The callback handed to
    /// <paramref name="work"/> updates the line of text on screen.
    /// </summary>
    public static bool Run(string caption, Func<Action<string>, bool> work)
    {
        string status = "Starting...";
        object gate = new();
        bool done = false, result = false;
        Exception? failure = null;

        void Report(string s) { lock (gate) status = s; }

        var worker = new Thread(() =>
        {
            try { result = work(Report); }
            catch (Exception e) { failure = e; result = false; }
            finally { Volatile.Write(ref done, true); }
        }) { IsBackground = true, Name = "launcher-setup" };
        worker.Start();

        // Only put a window up if the work is actually going to take a moment.
        // A cached launch reaches the game in a fraction of a second, and a
        // window that appears and vanishes again is worse than none.
        for (int i = 0; i < 40 && !Volatile.Read(ref done); i++) Thread.Sleep(10);
        if (Volatile.Read(ref done))
        {
            worker.Join();
            if (failure is not null) throw failure;
            return result;
        }

        IWindow? window = null;
        GL? gl = null;
        ImGuiController? imgui = null;
        IInputContext? input = null;

        try
        {
            var options = WindowOptions.Default with
            {
                Size = new Vector2D<int>(560, 190),
                Title = caption,
                WindowBorder = WindowBorder.Fixed,
                VSync = true,
                API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core,
                                      ContextFlags.ForwardCompatible, new APIVersion(3, 3)),
            };
            window = Silk.NET.Windowing.Window.Create(options);
            window.Initialize();
            input = window.CreateInput();
            gl = GL.GetApi(window);
            imgui = new ImGuiController(gl, window, input);
        }
        catch (Exception e)
        {
            // No window is not a reason to refuse to work; it is already
            // running on the worker thread. Just wait it out.
            Console.Error.WriteLine($"[Launcher] no progress window ({e.Message}); continuing without one");
            worker.Join(TimeSpan.FromMinutes(30));
            if (failure is not null) throw failure;
            return result;
        }

        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (!Volatile.Read(ref done) && !window.IsClosing)
        {
            window.DoEvents();
            imgui.Update(1f / 60f);

            var size = window.Size;
            ImGui.SetNextWindowPos(Vector2.Zero);
            ImGui.SetNextWindowSize(new Vector2(size.X, size.Y));
            ImGui.Begin("setup", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove |
                                 ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoBringToFrontOnFocus);

            ImGui.Dummy(new Vector2(0, 18));
            ImGui.PushFont(ImGui.GetFont());
            ImGui.TextWrapped(caption);
            ImGui.PopFont();
            ImGui.Dummy(new Vector2(0, 10));

            string line;
            lock (gate) line = status;
            ImGui.TextWrapped(line);

            ImGui.Dummy(new Vector2(0, 14));
            // An indeterminate bar: neither step reports a percentage, and a
            // fake percentage is worse than an honest "still going".
            float t = (float)clock.Elapsed.TotalSeconds;
            float frac = 0.5f + 0.5f * MathF.Sin(t * 2.2f);
            ImGui.ProgressBar(frac, new Vector2(-1, 8), "");
            ImGui.Dummy(new Vector2(0, 8));
            ImGui.TextDisabled($"{clock.Elapsed.TotalSeconds:F0}s elapsed - this only happens once");

            ImGui.End();

            gl.Viewport(0, 0, (uint)size.X, (uint)size.Y);
            gl.ClearColor(0.09f, 0.09f, 0.11f, 1f);
            gl.Clear(ClearBufferMask.ColorBufferBit);
            imgui.Render();
            window.SwapBuffers();
        }

        worker.Join(TimeSpan.FromMinutes(30));

        try { imgui.Dispose(); } catch { }
        try { input.Dispose(); } catch { }
        try { gl.Dispose(); } catch { }
        try { window.Close(); window.Dispose(); } catch { }

        if (failure is not null) throw failure;
        return result;
    }
}
