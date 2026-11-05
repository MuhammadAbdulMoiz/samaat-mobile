using System.Net;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Samaat.Handoff;
using Samaat.Handoff.Discovery;
using Samaat.Handoff.Storage;
using Samaat.Mobile.Runtime;
using Samaat.Mobile.Simulation;
using Samaat.Mobile.ViewModels;
using Samaat.Mobile.Views;
using Samaat.Simulator;

namespace Samaat.Mobile.Tests;

/// <summary>
/// The phone screens with the simulator's worst-case data (break-ui, as the desktop's WorstCaseRenderTests): long,
/// short, right-to-left and emoji names, missing fields, 0 % and 100 %, unreported readings, a computer with a long name
/// and many earbuds, a long toast, and no earbuds at all. Rendered in both themes at 412 px (a common phone) and 360 px
/// (the narrowest common Android width). Set SAMAAT_STRESS_SCREENSHOTS to a folder to keep the frames; without it the
/// test still renders every state and checks that no text is cut off without an ellipsis.
/// </summary>
public sealed class WorstCaseRenderTests
{
    private const string LongComputer = "Aleksandra Wiśniewska-Kowalczyk's ThinkPad X1 Carbon (work)";

    private static readonly (ThemeVariant Variant, string Suffix)[] Themes = [(ThemeVariant.Dark, ""), (ThemeVariant.Light, "-light")];

    private static readonly double[] Widths = [412, 360];

    /// <summary>Every break found across the frames, reported together so one run lists all of them.</summary>
    private readonly List<string> _breaks = [];

    private void AssertNoBreaks() => Assert.True(_breaks.Count == 0, string.Join("\n", _breaks.Distinct()));

    private static void Settle()
    {
        for (var i = 0; i < 40; i++)
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
        }
    }

    /// <summary>
    /// Text that is wider than the space it got and neither wraps nor trims is silently cut: the reader can't tell it
    /// was ever longer. Those are the breaks that matter most on a narrow phone.
    /// </summary>
    private void AssertNoTextCutWithoutEllipsis(Window window, string frame)
    {
        var cut = window.GetVisualDescendants().OfType<TextBlock>()
            .Where(t => t.IsEffectivelyVisible && t.Bounds.Width > 0 && !string.IsNullOrEmpty(t.Text))
            .Where(t => t.TextWrapping == TextWrapping.NoWrap && t.TextTrimming == TextTrimming.None)
            .Where(t =>
            {
                t.Measure(Size.Infinity);
                return t.DesiredSize.Width - t.Margin.Left - t.Margin.Right > t.Bounds.Width + 1;
            })
            .Select(t => $"{frame}: text cut without an ellipsis: '{t.Text}' needs {t.DesiredSize.Width - t.Margin.Left - t.Margin.Right:0} px, has {t.Bounds.Width:0}");

        _breaks.AddRange(cut);
    }

    /// <summary>Nothing may stick out past the window's right edge: a phone has no horizontal scroll.</summary>
    private void AssertNothingPastTheRightEdge(Window window, string frame)
    {
        var past = window.GetVisualDescendants().OfType<Control>()
            .Where(c => c.IsEffectivelyVisible && c.Bounds.Width > 0 && c is TextBlock or Button)
            // The scrollbar's own line buttons aren't ours, and Avalonia hides them for touch.
            .Where(c => !c.GetVisualAncestors().OfType<Avalonia.Controls.Primitives.ScrollBar>().Any())
            .Select(c => (c, box: c.TranslatePoint(new Point(c.Bounds.Width, 0), window)))
            .Where(x => x.box is { } p && p.X > window.Bounds.Width + 1)
            .Select(x => $"{frame}: past the right edge: {x.c.GetType().Name} '{(x.c as TextBlock)?.Text ?? (x.c as Button)?.Content}' ends at {x.box!.Value.X:0}");

        _breaks.AddRange(past);
    }

    private void Capture(Window window, string name)
    {
        Settle();
        AssertNoTextCutWithoutEllipsis(window, name);
        AssertNothingPastTheRightEdge(window, name);
        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        if (Environment.GetEnvironmentVariable("SAMAAT_STRESS_SCREENSHOTS") is { } directory)
        {
            Directory.CreateDirectory(directory);
            frame!.Save(Path.Combine(directory, $"{name}.png"));
        }
    }

    private static void EachLook(Window window, Action<string> render)
    {
        try
        {
            foreach (var (variant, suffix) in Themes)
            {
                Application.Current!.RequestedThemeVariant = variant;
                foreach (var width in Widths)
                {
                    window.Width = width;
                    render($"-{width:0}{suffix}");
                }
            }
        }
        finally
        {
            Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        }
    }

    [AvaloniaFact]
    public async Task WorstCase_RendersEveryScreen_InBothThemes_AtPhoneWidths()
    {
        await using var runtime = SimulatedMobile.Create(time: FrozenClock.Instance, scenario: SimulatorScenario.WorstCase);
        using var viewModel = new MainViewModel(runtime);
        // Tall, so a whole device page is in one frame.
        var window = new Window { Width = 412, Height = 1600, Content = new MainView { DataContext = viewModel } };
        window.Show();
        await viewModel.StartAsync();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(4, viewModel.Devices.Count);

        EachLook(window, look =>
        {
            viewModel.ShowEarbudsCommand.Execute(null);
            Capture(window, $"phone-worst-list{look}");
            for (var i = 0; i < viewModel.Devices.Count; i++)
            {
                viewModel.OpenCommand.Execute(viewModel.Devices[i]);
                Capture(window, $"phone-worst-{i + 1}-device{look}");
                viewModel.GoBack();
            }

            viewModel.ShowComputerCommand.Execute(null);
            Capture(window, $"phone-worst-computer{look}");
        });

        window.Close();
        AssertNoBreaks();
    }

    [AvaloniaFact]
    public async Task Empty_ShowsTheEmptyState_InBothThemes_AtPhoneWidths()
    {
        await using var runtime = SimulatedMobile.Create(time: FrozenClock.Instance, scenario: SimulatorScenario.Empty);
        using var viewModel = new MainViewModel(runtime);
        var window = new Window { Width = 412, Height = 892, Content = new MainView { DataContext = viewModel } };
        window.Show();
        await viewModel.StartAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(viewModel.Devices);
        Assert.True(viewModel.ShowEmpty);
        EachLook(window, look =>
        {
            viewModel.ShowEarbudsCommand.Execute(null);
            Capture(window, $"phone-empty-list{look}");
        });

        window.Close();
        AssertNoBreaks();
    }

    /// <summary>
    /// Paired with a computer that has a long name and the worst-case earbuds, plus a long toast over the page.
    /// </summary>
    [AvaloniaFact]
    public async Task LongComputerName_ManyComputerEarbuds_AndALongToast()
    {
        var bus = new InMemoryBeaconBus();
        await using var computerDevices = new SimulatorDeviceSource(TimeSpan.FromHours(1), scenario: SimulatorScenario.WorstCase);
        var demo = new SimulatorDeviceSource(TimeSpan.FromHours(1));
        await using var _ = demo;
        PairingRequest? request = null;
        await using var computer = new PhoneLinkHost(new PeerStore(null),
            new PhoneLinkHostOptions(LongComputer) { BindAddress = IPAddress.Loopback, Port = 0, BeaconInterval = TimeSpan.FromMilliseconds(100) },
            () => [.. computerDevices.Devices, .. demo.Devices], bus.CreateSender());
        computer.PairingRequested += (_, r) => request = r;
        computer.Start();
        await using var runtime = SimulatedMobile.Create(bus.CreateListener(), time: FrozenClock.Instance, scenario: SimulatorScenario.WorstCase,
            clientOptions: new PhoneLinkClientOptions { RetryMin = TimeSpan.FromMilliseconds(100), RetryMax = TimeSpan.FromMilliseconds(500) });
        using var viewModel = new MainViewModel(runtime);
        var window = new Window { Width = 412, Height = 1600, Content = new MainView { DataContext = viewModel } };
        window.Show();
        await viewModel.StartAsync();

        viewModel.ShowComputerCommand.Execute(null);
        computer.OpenPairing();
        await UntilAsync(() => viewModel.Computer.HasNearby, "the computer in the nearby list");
        EachLook(window, look => Capture(window, $"phone-long-nearby{look}"));
        await viewModel.Computer.Nearby[0].PairCommand.ExecuteAsync(null);
        await UntilAsync(() => viewModel.Computer.ShowCode && request is not null, "codes on both sides");
        EachLook(window, look => Capture(window, $"phone-long-code{look}"));
        var accepted = request!.AcceptAsync();
        await viewModel.Computer.ConfirmCommand.ExecuteAsync(null);
        Assert.True(await accepted);
        await UntilAsync(() => viewModel.Computer.IsOnline && viewModel.Computer.HasComputerEarbuds, "the computer's earbuds");

        viewModel.ShowMessage($"Paired with {LongComputer}. Open an earbuds page to move them between this phone and the computer.");
        EachLook(window, look => Capture(window, $"phone-long-paired{look}"));

        viewModel.ShowEarbudsCommand.Execute(null);
        viewModel.OpenCommand.Execute(viewModel.Devices[0]);
        EachLook(window, look => Capture(window, $"phone-long-device{look}"));

        window.Close();
        AssertNoBreaks();
    }

    private static async Task UntilAsync(Func<bool> condition, string what)
    {
        var deadline = Environment.TickCount64 + 15_000;
        while (!condition())
        {
            Assert.True(Environment.TickCount64 < deadline, $"Timed out waiting for: {what}");
            await Task.Delay(20);
            Dispatcher.UIThread.RunJobs();
        }
    }
}
