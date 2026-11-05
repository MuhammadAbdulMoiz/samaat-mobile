using System.Diagnostics;

namespace Samaat.Mobile.Diagnostics;

/// <summary>
/// Where the phone app writes errors it handles but mustn't hide: the recent entries in memory, the trace output, and
/// whatever the platform head adds (<see cref="Written"/>; Android forwards to logcat). Small on purpose: the desktop's
/// own logging is still being designed, and the phone should adopt that when it lands in the shared code.
/// </summary>
public static class MobileLog
{
    /// <summary>How many recent entries stay in memory.</summary>
    public const int Capacity = 100;

    private static readonly Lock Gate = new();
    private static readonly Queue<string> Entries = new();

    /// <summary>Raised for every entry, on the thread that wrote it.</summary>
    public static event Action<string>? Written;

    /// <summary>The recent entries, oldest first.</summary>
    public static IReadOnlyList<string> Recent()
    {
        lock (Gate)
        {
            return [.. Entries];
        }
    }

    /// <summary>Records an error with what Samaat was doing when it happened.</summary>
    public static void Error(string doing, Exception exception)
    {
        var line = $"{DateTimeOffset.UtcNow:O} {doing}: {exception.GetType().Name}: {exception.Message}";
        lock (Gate)
        {
            Entries.Enqueue(line);
            while (Entries.Count > Capacity)
            {
                Entries.Dequeue();
            }
        }

        Trace.TraceError(line);
        Written?.Invoke(line);
    }

    /// <summary>
    /// For a task nobody awaits: logs its fault (instead of losing it) and hands the exception to
    /// <paramref name="onError"/>, which runs on a pool thread.
    /// </summary>
    public static void Observe(this Task task, string doing, Action<Exception>? onError = null)
    {
        task.ContinueWith(t =>
        {
            var exception = t.Exception!.GetBaseException();
            Error(doing, exception);
            onError?.Invoke(exception);
        }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }
}
