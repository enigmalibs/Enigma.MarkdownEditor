using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Enigma.MarkdownEditor.Desktop.ViewModels;
using Enigma.MarkdownEditor.Desktop.Views;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Views;

/// <summary>
/// The About dialog's content, through the real view.
/// </summary>
/// <remarks>
/// The bindings are compiled — <c>x:DataType</c> and <c>AvaloniaUseCompiledBindingsByDefault</c> — so
/// a wrong property name is a build failure rather than a runtime one; what is left for a test is
/// that the markup loads at all, that each control found what it was pointed at, and the one decision
/// the view makes on its own: whether the build line is there. How it looks is a manual reading.
/// </remarks>
public sealed class AboutViewTests
{
    [AvaloniaFact]
    public void TheView_ShowsWhatTheViewModelSays()
    {
        AboutViewModel viewModel = Create("1.2.3", "abcdef1");
        AboutView view = Show(viewModel);

        string[] texts = [.. view.GetLogicalDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

        Assert.Contains(viewModel.DisplayName, texts);
        Assert.Contains("1.2.3", texts);
        Assert.Contains("abcdef1", texts);
        Assert.Contains(viewModel.Copyright, texts);
    }

    [AvaloniaFact]
    public void TheIcon_IsAResourceThatResolves()
    {
        AboutView view = Show(Create("1.2.3", "abcdef1"));

        // A missing or unreadable AvaloniaResource fails here rather than silently drawing nothing:
        // the Source converter opens the asset as the markup is loaded.
        Image icon = Assert.Single(view.GetLogicalDescendants().OfType<Image>());

        Assert.NotNull(icon.Source);
    }

    [AvaloniaFact]
    public void AStampedBuild_ShowsItsBuildLine()
    {
        AboutView view = Show(Create("1.2.3", "abcdef1"));

        Assert.True(Named(view, "BuildLine").IsVisible);
        Assert.Equal("abcdef1", Assert.IsType<SelectableTextBlock>(Named(view, "BuildSha")).Text);
    }

    [AvaloniaFact]
    public void ABuildWithNoRevision_CollapsesTheBuildLine()
    {
        AboutView view = Show(Create("1.2.3", buildSha: null));

        // Absent, not blank. The line is the only thing under the version, so an empty one would read
        // as a gap the layout got wrong.
        Assert.False(Named(view, "BuildLine").IsVisible);
    }

    [AvaloniaFact]
    public void NoCopyrightNotice_CollapsesItsLine()
    {
        AboutView view = Show(new AboutViewModel("1.2.3", "abcdef1", string.Empty, AboutViewModel.DefaultCredits));

        Assert.False(Named(view, "Copyright").IsVisible);
    }

    [AvaloniaFact]
    public void TheCreditsList_IsTheViewModelsOwn()
    {
        AboutViewModel viewModel = Create("1.2.3", "abcdef1");
        AboutView view = Show(viewModel);

        ItemsControl credits = Assert.IsType<ItemsControl>(Named(view, "Credits"));

        Assert.Same(viewModel.Credits, credits.ItemsSource);
    }

    [AvaloniaFact]
    public void EveryCredit_IsDrawnWithItsLicence()
    {
        AboutViewModel viewModel = Create("1.2.3", "abcdef1");
        AboutView view = Show(viewModel);

        string[] texts = [.. view.GetLogicalDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

        Assert.All(viewModel.Credits, credit =>
        {
            Assert.Contains(credit.Name, texts);
            Assert.Contains(credit.License, texts);
        });
    }

    private static AboutViewModel Create(string version, string? buildSha)
        => new(version, buildSha, "Copyright © 2026 Josué Clément", AboutViewModel.DefaultCredits);

    // By name rather than through a generated field: what is asserted is the markup, and a control
    // that lost its x:Name should fail this rather than stop compiling somewhere else.
    private static Control Named(AboutView view, string name)
        => view.GetLogicalDescendants().OfType<Control>().Single(control => control.Name == name);

    // Inside a window, and shown: IsVisible and the item containers are what a layout pass produced,
    // not what the markup asked for.
    private static AboutView Show(AboutViewModel viewModel)
    {
        AboutView view = new() { DataContext = viewModel };
        Window window = new() { Content = view };

        window.Show();

        return view;
    }
}
