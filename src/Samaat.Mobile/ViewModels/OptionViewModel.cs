using CommunityToolkit.Mvvm.Input;

namespace Samaat.Mobile.ViewModels;

/// <summary>A selectable choice: a noise mode (with its icon) or an EQ preset.</summary>
public sealed class OptionViewModel(string label, string? description, bool isSelected, Func<Task> select, string? iconKey = null)
{
    public string? IconKey { get; } = iconKey;

    public bool HasIcon => IconKey is not null;

    public string Label { get; } = label;

    public string? Description { get; } = description;

    public bool HasDescription => Description is not null;

    public bool IsSelected { get; } = isSelected;

    public IAsyncRelayCommand SelectCommand { get; } = new AsyncRelayCommand(select);
}
