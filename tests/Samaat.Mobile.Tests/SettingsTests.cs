using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Samaat.Core.Settings;
using Samaat.Mobile.Runtime;
using Samaat.Mobile.Simulation;
using Samaat.Mobile.ViewModels;

namespace Samaat.Mobile.Tests;

public sealed class SettingsTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"samaat-settings-{Guid.NewGuid():N}.json");

    public void Dispose() => File.Delete(_path);

    [Theory]
    [InlineData(ThemeChoice.System)]
    [InlineData(ThemeChoice.Light)]
    [InlineData(ThemeChoice.Dark)]
    public void TheVariantMapping_MatchesTheDesktop(ThemeChoice choice)
    {
        var expected = choice switch { ThemeChoice.Light => ThemeVariant.Light, ThemeChoice.Dark => ThemeVariant.Dark, _ => ThemeVariant.Default };

        Assert.Equal(expected, ThemePolicy.VariantFor(choice));
    }

    [Fact]
    public void ChoosingATheme_AppliesIt_SavesIt_AndKeepsTheOtherSettings()
    {
        var store = new SettingsStore(_path);
        store.Save(new AppSettings { ArtworkChoices = new Dictionary<string, string> { ["dev"] = "unknown.single" } });
        var applied = new List<ThemeChoice>();
        var settings = new SettingsViewModel(store, applied.Add);
        Assert.True(settings.IsDarkTheme);

        settings.SetThemeCommand.Execute(ThemeChoice.Light);

        var reloaded = new SettingsStore(_path).Load();
        Assert.Equal(ThemeChoice.Light, reloaded.Theme);
        Assert.Equal("unknown.single", reloaded.ArtworkChoices["dev"]);
        Assert.Equal([ThemeChoice.Light], applied);
        Assert.True(settings.IsLightTheme);
    }

    [Fact]
    public void AFailedSave_IsShown_AndTheChoiceStillApplies()
    {
        var folder = _path + ".dir";
        Directory.CreateDirectory(folder); // a folder where the file should be: Save fails
        try
        {
            var applied = new List<ThemeChoice>();
            var settings = new SettingsViewModel(new SettingsStore(folder), applied.Add);

            settings.SetThemeCommand.Execute(ThemeChoice.Light);

            Assert.True(settings.HasError);
            Assert.StartsWith("Settings could not be saved", settings.ErrorText);
            Assert.Equal([ThemeChoice.Light], applied);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void WithoutAStore_TheChoiceLastsTheSession()
    {
        var settings = new SettingsViewModel(null, _ => { });

        settings.SetThemeCommand.Execute(ThemeChoice.System);

        Assert.True(settings.IsSystemTheme);
    }

    [AvaloniaTheory]
    [InlineData(ThemeChoice.Light)]
    [InlineData(ThemeChoice.System)]
    [InlineData(ThemeChoice.Dark)]
    public void TheStoredTheme_IsAppliedWhenTheMainViewIsCreated(ThemeChoice choice)
    {
        var store = new SettingsStore(_path);
        store.Save(new AppSettings { Theme = choice });
        var runtime = SimulatedMobile.Create(settings: store);
        var factory = MobileApp.RuntimeFactory;
        MobileApp.RuntimeFactory = () => runtime;
        try
        {
            MobileApp.CreateMainView();

            Assert.Equal(ThemePolicy.VariantFor(choice), Application.Current!.RequestedThemeVariant);
        }
        finally
        {
            MobileApp.RuntimeFactory = factory;
            Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        }
    }

    [AvaloniaFact]
    public void TheSettingsTab_IsTitledSettings()
    {
        using var viewModel = new MainViewModel(SimulatedMobile.Create());

        viewModel.ShowSettingsCommand.Execute(null);

        Assert.True(viewModel.IsSettings);
        Assert.Equal("Settings", viewModel.Title);
        Assert.True(viewModel.GoBack());
        Assert.Equal("Samaat", viewModel.Title);
    }
}
