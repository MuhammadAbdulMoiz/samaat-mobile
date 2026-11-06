using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Samaat.Core.Battery;
using Samaat.Core.Profiles;
using Samaat.Mobile.Diagnostics;

namespace Samaat.Mobile.ViewModels;

// Artwork: the desktop's looks, from the same devices/ photos and ArtworkLook.Resolve.
public sealed partial class DeviceItemViewModel
{
    /// <summary>Whether this device draws two buds (true) or a headset (false) in the "?" look.</summary>
    public bool HasBuds => _battery?.Components.Contains(BatteryComponent.Left) == true
        || new[] { "bud", "pods", "earphone", "tws" }.Any(k => Name.Contains(k, StringComparison.OrdinalIgnoreCase));

    public string ArtworkLabel => $"{Name} {(HasBuds ? "earbuds" : "headset")}";

    /// <summary>The model photo inside the ring; null for the "?" looks.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasArtworkImage))]
    public partial Bitmap? ArtworkImage { get; set; }

    public bool HasArtworkImage => ArtworkImage is not null;

    /// <summary>Left bud, right bud and case photos, each with its own figure (the split look).</summary>
    [ObservableProperty]
    public partial IReadOnlyList<ArtworkPartViewModel> ArtworkParts { get; set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowRing))]
    public partial bool ShowArtworkParts { get; set; }

    [ObservableProperty]
    public partial bool ShowUnknownSingle { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowRing))]
    public partial bool ShowUnknownPieces { get; set; }

    /// <summary>The ring (with a photo or the single "?" drawing) shows unless the look is in separate pieces.</summary>
    public bool ShowRing => !ShowArtworkParts && !ShowUnknownPieces;

    /// <summary>Left bud, right bud and case figures for the pieces looks; "–" when not reported.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<PieceFigureViewModel> Pieces { get; set; } = [];

    /// <summary>"Change look": Automatic, the two "?" looks, then every model with a photo.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<LookOptionViewModel> LookOptions { get; set; } = [];

    private void ChangeLook(string key)
    {
        _lookChoice = key == ArtworkLook.Automatic ? null : key;
        _runtime.Looks.Set(Device.Identity.Id, _lookChoice);
        ApplyLook();
    }

    private bool ReportsBuds => _battery?.Current is { } state
        && (state.Get(BatteryComponent.Left) is not null || state.Get(BatteryComponent.Right) is not null);

    private void ApplyLook()
    {
        var profiles = _runtime.Profiles.Profiles;
        var look = ArtworkLook.Resolve(_lookChoice, _profile, profiles, ReportsBuds);
        if (!Equals(look, _look))
        {
            _look = look;
            var oldImage = ArtworkImage;
            var oldParts = _partBitmaps;
            ArtworkImage = Load(look.Profile?.Image);
            if (look.Profile is not null && ArtworkImage is null)
            {
                look = _look = new ResolvedLook(ReportsBuds ? LookKind.UnknownPieces : LookKind.UnknownSingle); // no photo to show
            }

            _partBitmaps = [];
            if (look.Kind == LookKind.PartsPhoto && look.Profile!.PartImages is { } parts)
            {
                var bitmaps = new[] { parts.Left, parts.Right, parts.Case }.Select(Load).ToList();
                if (bitmaps.All(b => b is not null))
                {
                    _partBitmaps = bitmaps!;
                }
                else
                {
                    bitmaps.ForEach(b => b?.Dispose());
                    look = _look = new ResolvedLook(LookKind.Photo, look.Profile); // a broken part photo keeps the whole photo
                }
            }

            ShowArtworkParts = look.Kind == LookKind.PartsPhoto;
            ShowUnknownSingle = look.Kind == LookKind.UnknownSingle;
            ShowUnknownPieces = look.Kind == LookKind.UnknownPieces;
            oldImage?.Dispose();
            foreach (var bitmap in oldParts)
            {
                bitmap.Dispose();
            }
        }

        ArtworkParts = _partBitmaps.Count == 3 && Pieces.Count == 3
            ? [.. _partBitmaps.Zip(Pieces, (bitmap, figure) => new ArtworkPartViewModel(figure, bitmap))]
            : [];

        var key = ArtworkLook.MenuKey(_lookChoice, profiles);
        if (LookOptions.Count == 0 || LookOptions.Single(o => o.IsSelected).Key != key)
        {
            LookOptionViewModel Option(string k, string label) => new(k, label, k == key, new RelayCommand(() => ChangeLook(k)));
            LookOptions =
            [
                Option(ArtworkLook.Automatic, "Automatic"),
                Option(ArtworkLook.UnknownSingle, "Unknown · single"),
                Option(ArtworkLook.UnknownPieces, "Unknown · separate pieces"),
                .. profiles.Where(p => p.Image is not null).OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                    .Select(p => Option(p.Id, p.DisplayName)),
            ];
        }
    }

    private static Bitmap? Load(string? path)
    {
        try
        {
            return path is null ? null : new Bitmap(path);
        }
        catch (Exception ex)
        {
            MobileLog.Error($"Loading the artwork photo {path}", ex);
            return null; // a broken image file falls back to the drawn artwork
        }
    }
}

public sealed record ArtworkPartViewModel(PieceFigureViewModel Figure, Bitmap Image);

/// <summary>One entry of the "Change look" menu; <see cref="Key"/> is "auto", "unknown.single", "unknown.pieces" or a profile id.</summary>
public sealed record LookOptionViewModel(string Key, string Label, bool IsSelected, System.Windows.Input.ICommand Select)
{
    public string AccessibleName => IsSelected ? $"{Label}, current look" : Label;
}

