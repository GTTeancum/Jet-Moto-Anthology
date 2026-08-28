using System.Text;

namespace JetMotoLauncher;

/// <summary>
/// Redirects Console output to a callback for the duration of a call.
///
/// The extractor and the recompiler both report progress with
/// Console.WriteLine, which was the right thing when this was run from a
/// terminal. Rather than thread a progress interface through both of them, the
/// launcher borrows what they already write and puts the latest line on the
/// setup window. In a windowed build that text has nowhere else to go.
/// </summary>
sealed class StatusWriter : TextWriter
{
    readonly Action<string> _report;
    readonly TextWriter _inner;
    readonly StringBuilder _line = new();

    StatusWriter(Action<string> report, TextWriter inner)
    {
        _report = report;
        _inner = inner;
    }

    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(char value)
    {
        if (value == '\n')
        {
            var text = _line.ToString().Trim();
            _line.Clear();
            if (text.Length > 0)
            {
                // The prefix is for a log, not for someone watching a window.
                if (text.StartsWith('[')) { int c = text.IndexOf(']'); if (c > 0) text = text[(c + 1)..].Trim(); }
                if (text.Length > 0) _report(text);
            }
        }
        else if (value != '\r')
        {
            _line.Append(value);
        }
        _inner.Write(value);
    }

    /// <summary>Run <paramref name="body"/> with Console routed to <paramref name="report"/>.</summary>
    public static bool Capture(Action<string> report, Func<bool> body)
    {
        var outer = Console.Out;
        var writer = new StatusWriter(report, outer);
        Console.SetOut(writer);
        try { return body(); }
        finally { Console.SetOut(outer); }
    }
}
