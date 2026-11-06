using CommunityToolkit.Mvvm.ComponentModel;
using Samaat.Core.Devices;
using Samaat.Mobile.Diagnostics;
using Samaat.Mobile.Runtime;

namespace Samaat.Mobile.ViewModels;

// The Diagnostics sub-page of a device (opened from the device page), and the phone's font size.
public sealed partial class MainViewModel
{
    /// <summary>The open Diagnostics page, or null. Created when the page opens and disposed when it closes.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowDevice), nameof(ShowDiagnostics), nameof(Title), nameof(CanGoBack))]
    public partial DeviceInfoViewModel? Diagnostics { get; set; }

    /// <summary>Puts text on the clipboard; the main view supplies the platform's. Fails until a view is attached.</summary>
    public Func<string, Task> CopyToClipboard { get; set; } = _ => Task.FromException(new InvalidOperationException("No clipboard is available."));

    /// <summary>Reads the phone's font size setting again and applies it (the Android activity calls this on resume).</summary>
    public void RefreshFontScale()
    {
        if (Avalonia.Application.Current is { } app)
        {
            FontScaling.Apply(app, _runtime.Platform.FontScale);
        }
    }

    private void OpenDiagnostics(IEarbudDevice device)
    {
        var item = Devices.FirstOrDefault(vm => vm.Device == device);
        if (item is null)
        {
            return;
        }

        CloseDiagnostics();
        SelectedDevice = item;
        Diagnostics = new DeviceInfoViewModel(device, text => CopyToClipboard(text), _post, _runtime.Time, _runtime.Profiles.Match(device.Identity));
    }

    private void CloseDiagnostics()
    {
        var open = Diagnostics;
        Diagnostics = null;
        open?.Dispose();
    }

    partial void OnSelectedDeviceChanged(DeviceItemViewModel? value) => CloseDiagnostics();

    partial void OnTabChanged(MobileTab value) => CloseDiagnostics();
}
