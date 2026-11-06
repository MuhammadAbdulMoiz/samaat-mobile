using Samaat.Core.Diagnostics;

namespace Samaat.Mobile.Diagnostics;

/// <summary>
/// The shared <see cref="SamaatLog"/>'s sink on the phone: every entry becomes one line in <see cref="MobileLog"/>
/// (memory, trace, logcat), which also feeds Diagnostics → Copy report through <see cref="IRecentLog"/>.
/// </summary>
public sealed class MobileLogSink : ILogSink, IRecentLog
{
    /// <summary>Makes this the process's sink unless it already is. Safe to call any number of times.</summary>
    public static void Install()
    {
        if (SamaatLog.Sink is not MobileLogSink)
        {
            SamaatLog.Sink = new MobileLogSink();
        }
    }

    public void Write(LogLevel level, string source, string message, Exception? error) =>
        MobileLog.WriteLine(LogFormat.Line(DateTimeOffset.UtcNow, level, source, message, error));

    public IReadOnlyList<string> Tail(int lines)
    {
        var recent = MobileLog.Recent();
        return lines >= recent.Count ? recent : [.. recent.Skip(recent.Count - Math.Max(0, lines))];
    }
}
