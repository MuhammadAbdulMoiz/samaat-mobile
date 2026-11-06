using System.Text.RegularExpressions;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Samaat.Handoff.Discovery;
using Samaat.Handoff.Storage;
using Samaat.Mobile.Diagnostics;
using Samaat.Mobile.Runtime;
using Samaat.Mobile.Simulation;
using Samaat.Mobile.ViewModels;
using Samaat.Simulator;

namespace Samaat.Mobile.Tests;

/// <summary>
/// Errors the phone used to drop: fire-and-forget tasks whose faults nobody observed, and broad catches that swallowed
/// the reason. Each now goes to MobileLog (and logcat on Android), and the start-up failure is also shown.
/// </summary>
public sealed class ErrorHandlingTests
{
    private static readonly DirectoryInfo Root = FindRoot();

    private static DirectoryInfo FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(dir.FullName, "Samaat.Mobile.slnx")))
        {
            dir = dir.Parent ?? throw new InvalidOperationException("Samaat.Mobile.slnx not found above the test output");
        }

        return dir;
    }

    /// <summary>The phone UI and the Android app; the platform layer turns its exceptions into results instead.</summary>
    private static IEnumerable<(string File, string Code)> Sources() =>
        new[] { "Samaat.Mobile", "Samaat.Android" }
            .SelectMany(p => Directory.EnumerateFiles(Path.Combine(Root.FullName, "src", p), "*.cs", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(f => (Path.GetRelativePath(Root.FullName, f), File.ReadAllText(f)));

    private sealed class FailingPlatform : IMobilePlatform
    {
        public string Name => "failing";

        public Task<bool> RequestPermissionsAsync() => Task.FromResult(true);

        public void OpenBluetoothSettings()
        {
        }

        public void KeepLinkAlive(bool enabled) => throw new InvalidOperationException("No foreground service here");
    }

    [Fact]
    public async Task Observe_LogsAFault_AndTellsTheCaller()
    {
        Exception? seen = null;
        var done = new TaskCompletionSource();

        Task.FromException(new InvalidOperationException("boom")).Observe("Testing Observe", ex =>
        {
            seen = ex;
            done.SetResult();
        });
        await done.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.IsType<InvalidOperationException>(seen);
        Assert.Contains(MobileLog.Recent(), line => line.Contains("Testing Observe") && line.Contains("boom"));
    }

    [Fact]
    public void MobileLog_KeepsOnlyTheRecentEntries()
    {
        for (var i = 0; i < MobileLog.Capacity + 20; i++)
        {
            MobileLog.Error($"Entry {i}", new InvalidOperationException("x"));
        }

        Assert.Equal(MobileLog.Capacity, MobileLog.Recent().Count);
    }

    /// <summary>Start used to be "_ = StartAsync()": a failure left an empty app and no trace of why.</summary>
    [AvaloniaFact]
    public async Task AFailedStart_IsLoggedAndShown()
    {
        var source = new SimulatorDeviceSource(TimeSpan.FromHours(1));
        await using var runtime = new MobileRuntime([source], new SimulatedPhoneAudio(source.Devices), new PeerStore(null), "Phone",
            new InMemoryBeaconBus().CreateListener(), new FailingPlatform());
        using var viewModel = new MainViewModel(runtime);

        viewModel.Start();
        var deadline = Environment.TickCount64 + 5_000;
        while (!viewModel.HasMessage && Environment.TickCount64 < deadline)
        {
            await Task.Delay(20);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.Equal("Samaat couldn't start everything. Close and reopen the app to try again.", viewModel.Message);
        Assert.Contains(MobileLog.Recent(), line => line.Contains("No foreground service here"));
    }

    [Fact]
    public void NoTaskIsFiredAndForgotten_WithoutObservingIt()
    {
        var discarded = new Regex(@"_\s*=\s*[^;]*(Async\(|ContinueWith\()");
        var found = Sources().SelectMany(s => s.Code.Split('\n').Select((line, i) => (s.File, line, number: i + 1)))
            .Where(l => discarded.IsMatch(l.line))
            .Select(l => $"{l.File}:{l.number}: {l.line.Trim()}")
            .ToList();

        Assert.True(found.Count == 0, "Use .Observe(\"what\") so a fault is logged:\n" + string.Join("\n", found));
    }

    [Fact]
    public void NoBroadCatch_SwallowsTheReason()
    {
        // A catch of Exception (or any Java exception) must at least log what happened.
        var broad = new Regex(@"catch \((?:Exception|Java\.Lang\.Exception)(?: \w+)?\)\s*\{(?<body>[^}]*)\}", RegexOptions.Singleline);
        var silent = Sources()
            .SelectMany(s => broad.Matches(s.Code).Select(m => (s.File, m)))
            .Where(x => !x.m.Groups["body"].Value.Contains("MobileLog.", StringComparison.Ordinal)
                        && !x.m.Groups["body"].Value.Contains("throw", StringComparison.Ordinal))
            .Select(x => $"{x.File}: {x.m.Value.Split('\n')[0].Trim()}")
            .ToList();

        Assert.True(silent.Count == 0, "Log the exception with MobileLog.Error:\n" + string.Join("\n", silent));
    }
}
