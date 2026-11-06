using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Samaat.Core.Settings;
using Samaat.Mobile.Runtime;

namespace Samaat.Mobile.ViewModels;

/// <summary>The Settings tab: the appearance choice (saved, applied at once) and the About section.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsStore? _store;
    private readonly Action<ThemeChoice> _apply;

    /// <param name="store">The settings file, or null: then the choice lasts for the session.</param>
    /// <param name="apply">Puts a theme choice on screen (<see cref="ThemePolicy.Apply"/> in the app).</param>
    public SettingsViewModel(SettingsStore? store, Action<ThemeChoice> apply)
    {
        _store = store;
        _apply = apply;
        Theme = ThemePolicy.Stored(store);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSystemTheme), nameof(IsLightTheme), nameof(IsDarkTheme))]
    public partial ThemeChoice Theme { get; private set; }

    public bool IsSystemTheme => Theme == ThemeChoice.System;

    public bool IsLightTheme => Theme == ThemeChoice.Light;

    public bool IsDarkTheme => Theme == ThemeChoice.Dark;

    [RelayCommand]
    private void SetTheme(ThemeChoice theme)
    {
        Theme = theme;
        _apply(theme);
        // The store logs the failure; say it here too so the choice isn't silently lost on restart.
        ErrorText = _store is null || _store.Save(_store.Load() with { Theme = theme }) ? null : _store.LastError;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorText { get; private set; }

    public bool HasError => ErrorText is not null;

    /// <summary>"Version 1.0.0", without the build hash (the desktop's approach, on this assembly).</summary>
    public string VersionText { get; } = "Version " + (typeof(SettingsViewModel).Assembly
        .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
        .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion.Split('+')[0]
        ?? typeof(SettingsViewModel).Assembly.GetName().Version?.ToString(3) ?? "unknown");

    /// <summary>The GPL-3.0 "Appropriate Legal Notices" shown in the About section.</summary>
    public string LicenceNotice => "Free software under the GNU GPL v3 or later. No warranty.";

    public Uri LicenceUri { get; } = new("https://www.gnu.org/licenses/gpl-3.0.html");

    public Uri SourceUri { get; } = new("https://github.com/MuhammadAbdulMoiz/samaat-mobile");
}
