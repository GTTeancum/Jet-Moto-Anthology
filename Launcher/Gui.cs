using System.Runtime.InteropServices;

namespace JetMotoLauncher;

/// <summary>
/// The little bit of user interface a windowed launcher still needs when
/// something goes wrong.
///
/// This ships as a windowed application, so there is no console for an error to
/// be written to: a failure would otherwise be a program that starts and
/// silently disappears. Everything that used to end up on stderr goes here.
/// </summary>
static class Gui
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern int MessageBoxW(nint hWnd, string text, string caption, uint type);

    const uint MbIconError = 0x00000010;
    const uint MbIconInfo = 0x00000040;
    const uint MbOk = 0x00000000;

    public static void Error(string message, string caption = "Jet Moto")
    {
        Console.Error.WriteLine(message);
        try { MessageBoxW(0, message, caption, MbOk | MbIconError); } catch { }
    }

    public static void Info(string message, string caption = "Jet Moto")
    {
        Console.WriteLine(message);
        try { MessageBoxW(0, message, caption, MbOk | MbIconInfo); } catch { }
    }
}
