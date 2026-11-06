using System.Text.RegularExpressions;
using Avalonia.Headless.XUnit;
using Samaat.Core.Devices;
using Samaat.Mobile.Diagnostics;
using Samaat.Mobile.Runtime;
using Samaat.Mobile.Simulation;
using Samaat.Mobile.ViewModels;

namespace Samaat.Mobile.Tests;

/// <summary>The Diagnostics sub-page: its sections, Copy report, and the way into and out of it.</summary>
public sealed class DiagnosticsTests
{
    private static IEarbudDevice Device(MobileRuntime runtime, string name) => runtime.Registry.Devices.Single(d => d.Identity.Name == name);

    private static DeviceInfoViewModel Make(string name, Func<string, Task> copy, Func<int, IReadOnlyList<string>>? log = null)
    {
        var runtime = SimulatedMobile.Create();
        runtime.Registry.RefreshAsync().GetAwaiter().GetResult();
        var device = Device(runtime, name);
        return new DeviceInfoViewModel(device, copy, a => a(), recentLog: log ?? (n => ["2026-11-06T10:00:00.000Z ERROR Mobile: earlier trouble | Boom: x"]),
            profile: runtime.Profiles.Match(device.Identity));
    }

    [Fact]
    public void Sections_SayWhatWorksFirst_AndGroupWhatDoesNot()
    {
        using var info = Make("Simulated Buds Lite", _ => Task.CompletedTask);

        Assert.Matches(new Regex(@"^\d+ of \d+$"), info.FeatureCountText);
        Assert.InRange(info.AvailablePercent, 1, 99);
        Assert.NotEmpty(info.AvailableRows);
        Assert.Equal("Standard support", info.SupportText);
        // The Lite buds have no equalizer: one group, with its reason.
        var equalizer = Assert.Single(info.UnavailableGroups, g => g.Module == "Equalizer");
        Assert.Contains("feature", equalizer.Title);
        Assert.NotEmpty(equalizer.Items);
        Assert.Contains(info.TechnicalRows, r => r.Label == "Address" && r.Value == "02:00:5E:10:00:02");
        Assert.True(info.HasNoEvents);
    }

    [Fact]
    public async Task TheEventLog_ListsConnectionChanges()
    {
        var runtime = SimulatedMobile.Create();
        await runtime.Registry.RefreshAsync();
        var device = (Samaat.Simulator.SimulatedEarbudDevice)Device(runtime, "Simulated Buds Pro");
        using var info = new DeviceInfoViewModel(device, _ => Task.CompletedTask, a => a(), recentLog: n => []);

        device.SetConnected(device.Connection != ConnectionState.Connected);

        Assert.False(info.HasNoEvents);
        Assert.Single(info.Events);
    }

    [Fact]
    public async Task CopyReport_PutsTheReportOnTheClipboard_AndConfirms()
    {
        string? copied = null;
        using var info = Make("Simulated Buds Pro", text =>
        {
            copied = text;
            return Task.CompletedTask;
        });

        await info.CopyReportCommand.ExecuteAsync(null);

        Assert.NotNull(copied);
        Assert.Contains("Name: Simulated Buds Pro", copied);
        Assert.Contains("Device ID: sim-buds-pro", copied);
        Assert.Contains("Recent log:", copied);
        Assert.Contains("earlier trouble", copied);
        Assert.Equal("Report copied", info.CopyStatus);
        Assert.False(info.HasCopyError);
    }

    [Fact]
    public async Task CopyReport_ThatFails_ShowsAnErrorLine_AndLogsIt()
    {
        var marker = Guid.NewGuid().ToString("N");
        using var info = Make("Simulated Buds Pro", _ => throw new InvalidOperationException(marker));

        await info.CopyReportCommand.ExecuteAsync(null);

        Assert.True(info.HasCopyError);
        Assert.Equal("Couldn't copy the report. Try again.", info.CopyError);
        Assert.Null(info.CopyStatus);
        Assert.Contains(MobileLog.Recent(), line => line.Contains(marker));
    }

    [AvaloniaFact]
    public async Task Navigation_GoesDeviceToDiagnosticsAndBackOneStepAtATime()
    {
        var runtime = SimulatedMobile.Create();
        using var shell = new MainViewModel(runtime, a => a());
        await shell.StartAsync();
        var buds = shell.Devices.Single(d => d.Name == "Simulated Buds Pro");

        shell.OpenCommand.Execute(buds);
        Assert.True(shell.ShowDevice);

        buds.OpenDiagnosticsCommand.Execute(null);
        Assert.True(shell.ShowDiagnostics);
        Assert.False(shell.ShowDevice);
        Assert.NotNull(shell.Diagnostics);
        Assert.Equal("Diagnostics", shell.Title);
        Assert.True(shell.CanGoBack);

        Assert.True(shell.GoBack());
        Assert.Null(shell.Diagnostics);
        Assert.True(shell.ShowDevice);
        Assert.Equal("Simulated Buds Pro", shell.Title);

        Assert.True(shell.GoBack());
        Assert.True(shell.ShowDeviceList);
    }

    [AvaloniaFact]
    public async Task TheDiagnosticsPage_UsesTheClipboardTheViewSupplies()
    {
        var runtime = SimulatedMobile.Create();
        using var shell = new MainViewModel(runtime, a => a());
        await shell.StartAsync();
        string? copied = null;
        shell.CopyToClipboard = text =>
        {
            copied = text;
            return Task.CompletedTask;
        };
        var buds = shell.Devices.Single(d => d.Name == "Simulated Buds Pro");
        shell.OpenCommand.Execute(buds);
        buds.OpenDiagnosticsCommand.Execute(null);

        await shell.Diagnostics!.CopyReportCommand.ExecuteAsync(null);

        Assert.Contains("Simulated Buds Pro", copied);
    }
}
