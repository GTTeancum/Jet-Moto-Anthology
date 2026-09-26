using System.Reflection;
using System.Diagnostics;
using RecompOne.Runtime;
using RecompOne.Runtime.Hle;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

// No disc needed. Default: check source contracts and render on all three backends.
// --static-only runs without a window. --pixels-only isolates actual renderer tests.
// Each backend runs in its own process: Silk keeps global native windowing state.
// Optional --backend Gl21|Gl33|Gl45 selects one backend; unavailable contexts fail, not skip.
int passed = 0, failed = 0;
void Check(bool ok, string message)
{
    if (ok) { passed++; Console.WriteLine("PASS: " + message); }
    else { failed++; Console.Error.WriteLine("FAIL: " + message); }
}
void Test(string name, Action action)
{
    try { action(); }
    catch (Exception ex) { Check(false, name + ": " + ex); }
}
bool staticOnly = args.Contains("--static-only"), pixelsOnly = args.Contains("--pixels-only");
if (staticOnly && pixelsOnly) { Console.Error.WriteLine("Conflicting test modes."); return 2; }
GlBackendKind[] kinds = [GlBackendKind.Gl45, GlBackendKind.Gl33, GlBackendKind.Gl21];
for (int i = 0; i < args.Length; i++)
{
    if (args[i] is "--static-only" or "--pixels-only") continue;
    if (args[i] == "--backend" && ++i < args.Length && Enum.TryParse(args[i], out GlBackendKind k) && kinds.Contains(k))
        kinds = [k];
    else { Console.Error.WriteLine("Unknown/incomplete option."); return 2; }
}

if (!pixelsOnly)
{
    Test("shader contracts", () =>
    {
        var shaders = typeof(GlCore).Assembly.GetType("RecompOne.Runtime.Hle.GlShaders", true)!;
        foreach (string name in new[] { "PrimVs", "PrimFs", "PrimVs120", "PrimFs120" })
        {
            string source = (string)shaders.GetField(name, BindingFlags.Public | BindingFlags.Static)!.GetRawConstantValue()!;
            Check(!source.Contains("vDither") && !source.Contains("ditherTbl") && !source.Contains("c8 + d"),
                name + " contains no executable dither branch, varying or matrix");
            if (name is "PrimFs" or "PrimFs120")
            {
                int rider = source.IndexOf("if(vSurface.w>2.5 && vSurface.w<3.5)", StringComparison.Ordinal);
                int heightShadow = source.IndexOf("originalHeight(samplePoint.xy)", StringComparison.Ordinal);
                int riderShadow = source.IndexOf(name == "PrimFs" ? "texture(uRiderShadow" : "texture2D(uRiderShadow", StringComparison.Ordinal);
                Check(rider >= 0 && rider < heightShadow && rider < riderShadow,
                    name + " resolves rider lighting before any receiver shadow sampling");
            }
        }
    });
    Test("guest GPU status", () =>
    {
        var gpu = new Gpu();
        var envMethod = typeof(Gpu).GetMethod("CurEnv", BindingFlags.NonPublic | BindingFlags.Instance)!;
        foreach (bool requested in new[] { false, true, false, true })
        {
            gpu.WriteGp0(0xE1000000u | (requested ? 0x200u : 0u));
            Check(((gpu.ReadStat() & 0x200u) != 0) == requested, "GPUSTAT preserves guest request " + requested);
            var env = (HleDrawEnv)envMethod.Invoke(gpu, null)!;
            Check(!env.Dither, "guest request " + requested + " is not forwarded to renderer");
        }
    });
    Test("vertex flag packing", () =>
    {
        var core = new GlCore(null!, null!); // V only packs data; no GL calls.
        core.SetDrawEnv(new HleDrawEnv { Dither = true });
        var method = typeof(GlCore).GetMethod("V", BindingFlags.NonPublic | BindingFlags.Instance)!;
        bool allClear = true;
        for (int bits = 0; bits < 16; bits++)
        {
            var f = new PrimFlags { Textured = (bits & 1) != 0, Gouraud = (bits & 2) != 0,
                RawTexture = (bits & 4) != 0, UseImage = (bits & 8) != 0, TPage = 0xFFFF };
            var v = new HleVertex { R = 128, G = 128, B = 128 };
            // Permit the pre-patch method shape so this test can demonstrate a red baseline.
            object[] call = method.GetParameters().Length == 3 ? [v, f, true] : [v, f];
            object packed = method.Invoke(core, call)!;
            float tpage = (float)packed.GetType().GetField("Texpage")!.GetValue(packed)!;
            allClear &= (((int)tpage) & 0x400) == 0;
        }
        Check(allClear, "all 16 vertex-flag combinations suppress packed dither bit");
    });
}

if (!staticOnly && !args.Contains("--backend"))
{
    // Avoid creating/destroying incompatible GL contexts in a shared Silk process.
    foreach (var backend in kinds)
    Test(backend + " isolated process", () =>
    {
        string host = Environment.ProcessPath ?? throw new InvalidOperationException("No executable path.");
        var start = new ProcessStartInfo(host) { UseShellExecute = false };
        if (Path.GetFileNameWithoutExtension(host).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        start.ArgumentList.Add("--pixels-only");
        start.ArgumentList.Add("--backend");
        start.ArgumentList.Add(backend.ToString());
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start renderer test.");
        if (!process.WaitForExit(120000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Renderer test exceeded its 120-second safety limit.");
        }
        Check(process.ExitCode == 0, backend + " isolated renderer suite");
    });
    Console.WriteLine($"RESULT (static checks + child suites): {passed} passed; {failed} failed.");
    return failed == 0 ? 0 : 1;
}

if (!staticOnly)
foreach (var kind in kinds)
Test(kind + " renderer", () =>
{
    var version = kind == GlBackendKind.Gl45 ? new APIVersion(4, 5) :
        kind == GlBackendKind.Gl33 ? new APIVersion(3, 3) : new APIVersion(2, 1);
    var options = WindowOptions.Default with
    {
        Size = new Vector2D<int>(64, 64), IsVisible = false, VSync = false,
        Title = "Jet Moto renderer regression",
        API = new GraphicsAPI(ContextAPI.OpenGL,
            kind == GlBackendKind.Gl21 ? ContextProfile.Compatability : ContextProfile.Core,
            ContextFlags.Default, version)
    };
    using var window = Window.Create(options);
    window.Initialize();
    window.GLContext!.MakeCurrent();
    using var gl = GL.GetApi(window);
    Console.WriteLine($"CONTEXT {kind}: {gl.GetStringS(StringName.Version)} / {gl.GetStringS(StringName.Renderer)}");
    foreach (int scale in new[] { 1, 2, 4 })
    {
        GlVram.Scale = scale;
        gl.Enable(EnableCap.Dither); // Force the host default back on before initialization.
        var core = (GlCore)GpuBackendFactory.Create(gl, kind);
        try
        {
            core.InitGl();
            Check(core.Ready, $"{kind} {scale}x: all renderer shaders compile/link");
            if (!core.Ready) continue;
            Check(!gl.IsEnabled(EnableCap.Dither), $"{kind} {scale}x: InitGl disables host dithering");
            WorldLightingPixels.Run(core, $"{kind} {scale}x world", Check);
            PerspectivePixels.Run(core, $"{kind} {scale}x perspective", Check);
            NativeTexturePixels.Run(core, $"{kind} {scale}x native assets", Check);
            EffectPixels.Run(core, $"{kind} {scale}x effect coverage", Check);
            CutoutPixels.Run(core, $"{kind} {scale}x buoy cutout", Check);
            // Source texture lives outside the 64x64 output test region.
            ushort[] texture = Enumerable.Repeat((ushort)0x4210, 64 * 64).ToArray();
            core.WriteVram(640, 256, 64, 64, texture);
            byte[] image = Enumerable.Repeat((byte)255, 64 * 64 * 4).ToArray();
            int imageId = core.RegisterImage(image, 64, 64);
            foreach (string shape in new[] { "gouraud", "textured", "raw-textured", "rect", "line", "image", "semi-transparent" })
            {
                ushort[] Render(bool request)
                {
                    core.FillRect(0, 0, 64, 64, 0);
                    core.SetDrawEnv(new HleDrawEnv { ClipX1 = 63, ClipY1 = 63, Dither = request });
                    var f = new PrimFlags { Gouraud = true,
                        Textured = shape is "textured" or "raw-textured",
                        RawTexture = shape == "raw-textured", UseImage = shape == "image", Image = imageId,
                        SemiTrans = shape == "semi-transparent", TPage = 0x11A };
                    HleVertex V(float x, float y) => new() { X = x, Y = y, R = 128, G = 128, B = 128, U = 1, V = 1 };
                    gl.Enable(EnableCap.Dither); // Ensure Flush also defends against changed host state.
                    if (shape == "line") core.DrawLine(V(8, 8), V(39, 8), f);
                    else if (shape == "rect") core.DrawRect(new HleRect { X = 8, Y = 8, W = 32, H = 32, R = 128, G = 128, B = 128 }, f);
                    else
                    {
                        core.DrawTri(V(8, 8), V(40, 8), V(8, 40), f);
                        core.DrawTri(V(40, 8), V(40, 40), V(8, 40), f);
                    }
                    core.Flush();
                    Check(!gl.IsEnabled(EnableCap.Dither), $"{kind} {scale}x {shape} request={request}: host dithering stays off");
                    ushort[] result = new ushort[64 * 64];
                    core.ReadVram(0, 0, 64, 64, result);
                    return result;
                }
                ushort[] off = Render(false), on = Render(true);
                Check(off.AsSpan().SequenceEqual(on), $"{kind} {scale}x {shape}: guest dither request cannot alter any pixel");
                var colors = on.Select(p => p & 0x7FFF).Where(p => p != 0).Distinct().ToArray();
                Check(colors.Length == 1, $"{kind} {scale}x {shape}: filled pixels have one color, not a dither pattern ({colors.Length} colors)");
            }
            gl.Enable(EnableCap.Dither);
            core.PresentDisplay(0, 0, 64, 64);
            Check(!gl.IsEnabled(EnableCap.Dither), $"{kind} {scale}x: presentation with empty batch disables host dithering");
            Check(gl.GetError() == GLEnum.NoError, $"{kind} {scale}x: no OpenGL errors");
            // True Hor+ regression: these polygons are wholly outside the original
            // 320-column viewport. They must be rendered, not produced by stretching.
            GpuHle.SourceAspect = 4f / 3f; GpuHle.OutputAspect = 4f / 3f;
            GpuHle.WideAspect = 0;
            GpuHle.NotifyDisplay(0, 0, 320, 240);
            core.SetDrawEnv(new HleDrawEnv { ClipX1 = 319, ClipY1 = 239 });
            void Rect(int x, int y, int w, int h, byte r, byte g, byte b) =>
                core.DrawRect(new HleRect { X=x,Y=y,W=w,H=h,R=r,G=g,B=b }, new PrimFlags());
            Rect(0,0,320,240,32,32,32);
            var menu = core.PresentDisplay(0,0,320,240);
            Check(menu.w==320*scale && Math.Abs(menu.aspect-4f/3f)<1e-6,
                $"{kind} {scale}x: front-end has original 4:3 canvas");
            Rect(150,90,20,20,248,0,0); // Deliberately leave a pending batch in old FBO.
            long beforeVersion=GpuHle.RectVersion;
            GpuHle.WideAspect=16f/9f; // Do not notify display: aspect alone must invalidate cache.
            Rect(-45,90,20,20,0,248,0);
            Rect(345,90,20,20,0,0,248);
            var wide=core.PresentDisplay(0,0,320,240);
            Check(GpuHle.RectVersion==beforeVersion && wide.w==428*scale && wide.h==240*scale,
                $"{kind} {scale}x: aspect-only transition reallocates an extended render target");
            Check(Math.Abs(wide.aspect-16f/9f)<1e-6,$"{kind} {scale}x: gameplay presents at 16:9");
            byte[] Capture(uint tex, int w, int h)
            {
                byte[] result=new byte[w*h*4];
                gl.BindTexture(TextureTarget.Texture2D,tex);
                unsafe {fixed(byte* ptr=result)gl.GetTexImage(TextureTarget.Texture2D,0,PixelFormat.Rgba,PixelType.UnsignedByte,ptr);}
                return result;
            }
            bool Has(byte[] p,int channel,int x0,int x1,int y0,int y1)
            {
                int width=p.Length/(240*scale*4);
                for(int y=y0*scale;y<y1*scale;y++)for(int x=x0*scale;x<x1*scale;x++)
                {
                    int q=(y*width+x)*4;
                    if(p[q+channel]>200 && p[q+(channel+1)%3]<40 && p[q+(channel+2)%3]<40)return true;
                }
                return false;
            }
            byte[] full=Capture(wide.tex,wide.w,wide.h);
            Check(Has(full,1,5,45,80,120),$"{kind} {scale}x: left off-screen-only polygon is visible");
            Check(Has(full,2,385,425,80,120),$"{kind} {scale}x: right off-screen-only polygon is visible");
            Check(Has(full,0,195,230,80,120),$"{kind} {scale}x: pending centre batch survived FBO replacement");
            foreach(int idleFrames in new[]{5,301,1000})
            {
                for(int idle=0;idle<idleFrames;idle++)core.AdvanceFrame();
                var held=core.PresentDisplay(0,0,320,240);var heldPixels=Capture(held.tex,held.w,held.h);
                Check(held.w==428*scale && Math.Abs(held.aspect-16f/9f)<1e-6 && heldPixels.AsSpan().SequenceEqual(full),
                    $"{kind} {scale}x: {idleFrames} idle presents retain exact 16:9 frame and both wings");
            }
            GpuHle.WideAspect=0;
            var paused=core.PresentDisplay(0,0,320,240); // no redraw, as in a paused game.
            byte[] centre=Capture(paused.tex,paused.w,paused.h);
            Check(paused.w==320*scale && Math.Abs(paused.aspect-4f/3f)<1e-6 && Has(centre,0,145,175,80,120),
                $"{kind} {scale}x: pause crops centre of the previous wide frame without squeezing it");
            for(int idle=0;idle<1000;idle++)core.AdvanceFrame();
            var heldPause=core.PresentDisplay(0,0,320,240);
            Check(heldPause.w==320*scale && Math.Abs(heldPause.aspect-4f/3f)<1e-6 &&
                Capture(heldPause.tex,heldPause.w,heldPause.h).AsSpan().SequenceEqual(centre),
                $"{kind} {scale}x: long static pause keeps the original centred 4:3 frame");
            Check(!Has(centre,1,0,320,0,240) && !Has(centre,2,0,320,0,240),
                $"{kind} {scale}x: old widescreen wings never leak into the pillarboxed menu");
            // Exercise actual hi-res menu -> low-res race target changes too.
            GpuHle.NotifyDisplay(0,0,640,480);
            core.SetDrawEnv(new HleDrawEnv {ClipX1=639,ClipY1=479});
            Rect(0,0,640,480,32,32,32);
            var hi=core.PresentDisplay(0,0,640,480);
            Check(hi.w==640*scale && hi.h==480*scale && Math.Abs(hi.aspect-4f/3f)<1e-6,
                $"{kind} {scale}x: 640x480 menus retain their own resolution and 4:3 aspect");
            for(int cycle=0;cycle<3;cycle++)
            {
                GpuHle.WideAspect=16f/9f;GpuHle.NotifyDisplay(0,0,320,240);
                core.SetDrawEnv(new HleDrawEnv {ClipX1=319,ClipY1=239});
                Rect(-54,0,428,240,32,32,32);Rect(-45,90,20,20,0,248,0);Rect(345,90,20,20,0,0,248);
                var again=core.PresentDisplay(0,0,320,240);
                var pixels=Capture(again.tex,again.w,again.h);
                Check(again.w==428*scale && Has(pixels,1,5,45,80,120) && Has(pixels,2,385,425,80,120),
                    $"{kind} {scale}x: race/menu cycle {cycle+1} restores both wings");
                GpuHle.WideAspect=0;core.PresentDisplay(0,0,320,240);
            }
            // Blackwater Falls regression: its sky is an ordinary full-screen
            // solid rectangle, not a sky dome. It must clear both extra side strips.
            GpuHle.WideAspect=16f/9f;GpuHle.NotifyDisplay(0,0,320,240);
            core.SetDrawEnv(new HleDrawEnv {ClipX1=319,ClipY1=239});
            Rect(0,0,320,240,197,191,139);
            var sky=core.PresentDisplay(0,0,320,240);
            var skyPixels=Capture(sky.tex,sky.w,sky.h);
            bool SamePixel(byte[] p, int ax, int ay, int bx, int by, int width)
            {
                int a=(ay*scale*width+ax*scale)*4,b=(by*scale*width+bx*scale)*4;
                return p.AsSpan(a,3).SequenceEqual(p.AsSpan(b,3)) && p[a]>100;
            }
            Check(SamePixel(skyPixels,5,10,214,10,sky.w) && SamePixel(skyPixels,423,10,214,10,sky.w),
                $"{kind} {scale}x: full-screen sky clear covers both wings with the original colour");
            Rect(0,0,320,20,248,0,0); // A partial HUD band must not extend into the wings.
            var band=core.PresentDisplay(0,0,320,240);var bandPixels=Capture(band.tex,band.w,band.h);
            Check(!Has(bandPixels,0,0,45,0,20) && !Has(bandPixels,0,383,428,0,20),
                $"{kind} {scale}x: partial solid HUD rectangles do not expand");
            // Same clear on the alternate, x-offset guest framebuffer.
            GpuHle.NotifyDisplay(320,0,320,240);
            core.SetDrawEnv(new HleDrawEnv {ClipX0=320,ClipX1=639,ClipY1=239});
            Rect(320,0,320,240,197,191,139);
            var offsetSky=core.PresentDisplay(320,0,320,240);var offsetPixels=Capture(offsetSky.tex,offsetSky.w,offsetSky.h);
            Check(SamePixel(offsetPixels,5,10,214,10,offsetSky.w) && SamePixel(offsetPixels,423,10,214,10,offsetSky.w),
                $"{kind} {scale}x: x-offset double-buffer sky also covers both wings");
            GpuHle.WideAspect=0;
            var skyMenu=core.PresentDisplay(320,0,320,240);
            Check(skyMenu.w==320*scale && Math.Abs(skyMenu.aspect-4f/3f)<1e-6,
                $"{kind} {scale}x: sky fix does not widen the menu presentation");
            // Split-screen: translated camera centres, unchanged object width,
            // independent clears, outside polygons and an uncrossable centre divider.
            foreach(int baseX in new[]{0,320})
            {
                GpuHle.WideAspect=16f/9f;GpuHle.NotifyDisplay(baseX,0,320,240);
                core.SetDrawEnv(new HleDrawEnv {ClipX0=baseX,ClipX1=baseX+159,ClipY1=239});
                Rect(baseX,0,160,240,197,191,139);
                Rect(baseX+70,90,20,20,248,248,248);
                Rect(baseX-15,90,10,20,248,0,0);
                Rect(baseX+130,150,100,10,248,0,0); // Attempts to cross the divider.
                core.SetDrawEnv(new HleDrawEnv {ClipX0=baseX+160,ClipX1=baseX+319,ClipY1=239});
                Rect(baseX+160,0,160,240,197,191,139);
                Rect(baseX+230,90,20,20,248,248,248);
                Rect(baseX+325,90,10,20,0,0,248);
                Rect(baseX+80,150,110,10,0,0,248); // Attempts to cross in the opposite direction.
                var split=core.PresentDisplay(baseX,0,320,240);var sp=Capture(split.tex,split.w,split.h);
                (double centre,int width) WhiteBounds(int start,int end)
                {
                    var xs=Enumerable.Range(start*scale,(end-start)*scale).Where(x=> {
                        int q=(100*scale*split.w+x)*4;
                        return sp[q]>200 && sp[q+1]>200 && sp[q+2]>200;
                    }).ToArray();
                    return xs.Length==0?(-1,0):((xs[0]+xs[^1]+1)/2.0/scale,xs.Length);
                }
                var leftMark=WhiteBounds(0,214);var rightMark=WhiteBounds(214,428);
                Console.WriteLine($"SPLIT CENTRES {kind} {scale}x buffer {baseX}: left={leftMark.centre:F5}/{leftMark.width}px right={rightMark.centre:F5}/{rightMark.width}px");
                // PresentDisplay resamples the exact 426 2/3-wide view to a 428-wide texture.
                // Therefore measure the quarter points of the PRESENTED texture here.
                Check(Math.Abs(leftMark.centre-107)<=.6/scale &&
                      Math.Abs(rightMark.centre-321)<=.6/scale,
                    $"{kind} {scale}x buffer {baseX}: both split-screen cameras have correct new centres");
                Check(leftMark.width==20*scale && rightMark.width==20*scale,
                    $"{kind} {scale}x buffer {baseX}: split-screen object widths are not stretched");
                Check(Has(sp,0,5,30,80,120) && Has(sp,2,400,425,80,120),
                    $"{kind} {scale}x buffer {baseX}: split-screen outside-edge polygons render");
                Check(!Has(sp,0,214,428,150,160) && !Has(sp,2,0,214,150,160),
                    $"{kind} {scale}x buffer {baseX}: neither player can draw across the centre divider");
                Check(SamePixel(sp,5,10,214,10,split.w) && SamePixel(sp,423,10,214,10,split.w),
                    $"{kind} {scale}x buffer {baseX}: independent half-width sky clears fill both outer edges");
            }
            GpuHle.NotifyDisplay(0,0,320,240);
            core.SetDrawEnv(new HleDrawEnv {ClipX1=319,ClipY1=119});
            Rect(0,0,320,120,197,191,139);
            core.SetDrawEnv(new HleDrawEnv {ClipY0=120,ClipX1=319,ClipY1=239});
            Rect(0,120,320,120,128,180,240);
            var stacked=core.PresentDisplay(0,0,320,240);var stackedPixels=Capture(stacked.tex,stacked.w,stacked.h);
            Check(SamePixel(stackedPixels,5,60,214,60,stacked.w) && SamePixel(stackedPixels,423,60,214,60,stacked.w),
                $"{kind} {scale}x: top split-screen sky fills its full width");
            Check(SamePixel(stackedPixels,5,180,214,180,stacked.w) && SamePixel(stackedPixels,423,180,214,180,stacked.w),
                $"{kind} {scale}x: bottom split-screen sky fills its full width independently");
            GpuHle.WideAspect=0;core.PresentDisplay(0,0,320,240);
            Check(!gl.IsEnabled(EnableCap.Dither),$"{kind} {scale}x: dithering stays disabled through all widescreen transitions");
            Check(gl.GetError()==GLEnum.NoError,$"{kind} {scale}x: widescreen transitions produce no GL errors");

        }
        finally { core.Dispose(); }
    }
});
Console.WriteLine($"RESULT: {passed} passed; {failed} failed.");
return failed == 0 ? 0 : 1;
