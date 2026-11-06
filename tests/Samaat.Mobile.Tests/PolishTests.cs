using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Samaat.Mobile.Tests;

/// <summary>
/// The unslop polish, as the desktop applied it (docs/decisions in the desktop repo, PR #2): selection is a neutral
/// surface step with the accent on one small thing, never a lime-tinted fill.
/// </summary>
public sealed class PolishTests
{
    private static Color Token(string key)
    {
        Assert.True(Avalonia.Application.Current!.TryGetResource(key, ThemeVariant.Dark, out var value), key);
        return ((ISolidColorBrush)value!).Color;
    }

    private static (Window Window, ContentPresenter Presenter) Show(Button button)
    {
        var window = new Window { Content = button, Width = 412, Height = 300 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var presenter = button.GetVisualDescendants().OfType<ContentPresenter>().First(p => p.Name == "PART_ContentPresenter");
        return (window, presenter);
    }

    [AvaloniaFact]
    public void SelectedOption_IsANeutralStep_WithAnAccentEdge()
    {
        var (window, presenter) = Show(new Button { Classes = { "option", "selected" }, Content = new TextBlock { Text = "Transparency" } });

        Assert.Equal(Token("Samaat.Surface2"), ((ISolidColorBrush)presenter.Background!).Color);
        Assert.Equal(Token("Samaat.Accent"), ((ISolidColorBrush)presenter.BorderBrush!).Color);
        window.Close();
    }

    [AvaloniaFact]
    public void SelectedNavKey_IsNeutral_AndOnlyItsGlyphIsLit()
    {
        var icon = new PathIcon();
        var label = new TextBlock { Text = "Earbuds" };
        var (window, presenter) = Show(new Button { Classes = { "nav", "selected" }, Content = new StackPanel { Children = { icon, label } } });

        Assert.Equal(Token("Samaat.Surface2"), ((ISolidColorBrush)presenter.Background!).Color);
        Assert.Equal(Token("Samaat.Accent"), ((ISolidColorBrush)icon.Foreground!).Color);
        Assert.Equal(Token("Samaat.Text"), ((ISolidColorBrush)label.Foreground!).Color);
        window.Close();
    }

    [AvaloniaFact]
    public void Headings_Use600_Not700()
    {
        foreach (var style in new[] { "title", "display" })
        {
            var text = new TextBlock { Text = "Samaat", Classes = { style } };
            var window = new Window { Content = text };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Assert.True(text.FontWeight <= FontWeight.SemiBold, $"TextBlock.{style} is {text.FontWeight}");
            window.Close();
        }
    }

    /// <summary>The shell style (ink background, body text size) must reach MainView, a UserControl subclass.</summary>
    [AvaloniaFact]
    public void Shell_AppliesToTheMainView()
    {
        var view = new Samaat.Mobile.Views.MainView();
        var window = new Window { Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.True(Avalonia.Application.Current!.TryGetResource("Samaat.Mobile.FontSize.Body", ThemeVariant.Dark, out var body));
        Assert.Equal((double)body!, view.FontSize);
        Assert.Equal(Token("Samaat.Bg"), ((ISolidColorBrush)view.Background!).Color);
        window.Close();
    }
}
