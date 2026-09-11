using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Enigma.Avalonia.Desktop.Controls;
using Enigma.MarkdownEditor.Core.Settings;
using Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;
using Enigma.MarkdownEditor.Desktop.ViewModels;
using Enigma.MarkdownEditor.Desktop.Views;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Views;

/// <summary>
/// The welcome dialog's content, through the real view.
/// </summary>
/// <remarks>
/// The bindings are compiled — <c>x:DataType</c> and <c>AvaloniaUseCompiledBindingsByDefault</c> — so
/// a wrong property name is a build failure rather than a runtime one; what is left for a test is that
/// the markup loads at all (the three <c>SettingsCard</c> rows and their icons included), that each
/// control found what it was pointed at, and the one decision the view makes on its own: whether the
/// error line is there. How it looks is a manual reading.
/// </remarks>
public sealed class FirstRunViewTests : IDisposable
{
    private readonly TempSettings _settings = new();

    [AvaloniaFact]
    public void TheDialog_AsksTheThreeFirstRunQuestionsInOrder()
    {
        FirstRunView view = Show(Create());

        Assert.Equal(
            ["Vim emulation", "Theme", "Notes folder"],
            Rows(view).Select(row => row.Header?.ToString() ?? string.Empty));
    }

    [AvaloniaFact]
    public void TheRows_ShowWhatTheViewModelSays()
    {
        FirstRunViewModel viewModel = Create();
        viewModel.VimEnabled = false;
        viewModel.Theme = AppTheme.Dark;

        FirstRunView view = Show(viewModel);

        Assert.False(Assert.IsType<ToggleSwitch>(Row(view, "VimRow").Content).IsChecked);

        ComboBox themes = Assert.IsType<ComboBox>(Row(view, "ThemeRow").Content);
        Assert.Same(viewModel.Themes, themes.ItemsSource);
        Assert.Equal(AppTheme.Dark, themes.SelectedItem);

        Assert.Equal(FirstRunViewModel.NoFolderChosenText, FolderPath(view).Text);
    }

    [AvaloniaFact]
    public void TogglingTheSwitch_ReachesTheViewModel()
    {
        FirstRunViewModel viewModel = Create();
        FirstRunView view = Show(viewModel);

        Assert.IsType<ToggleSwitch>(Row(view, "VimRow").Content).IsChecked = false;

        // Two-way, and it has to be: the settings object the switch writes into is the one the editor
        // pane re-reads the moment the dialog closes.
        Assert.False(viewModel.VimEnabled);
    }

    [AvaloniaFact]
    public async Task ChoosingAFolder_ShowsThePathInTheRow()
    {
        FakeFolderPicker picker = new() { Picked = "/notes" };
        FirstRunViewModel viewModel = Create(picker);
        FirstRunView view = Show(viewModel);

        await viewModel.ChooseFolderCommand.ExecuteAsync(null);

        Assert.Equal("/notes", FolderPath(view).Text);
    }

    [AvaloniaFact]
    public void WithNothingWrong_TheErrorLineIsAbsentRatherThanBlank()
    {
        FirstRunView view = Show(Create());

        Assert.False(Named(view, "Error").IsVisible);
    }

    [AvaloniaFact]
    public async Task AFolderPickerThatCannotOpen_ShowsItsErrorLine()
    {
        FirstRunViewModel viewModel = Create(
            new FakeFolderPicker { Failure = new InvalidOperationException("no storage provider") });

        FirstRunView view = Show(viewModel);

        await viewModel.ChooseFolderCommand.ExecuteAsync(null);

        // Inside the dialog, because a ContentDialog cannot stack a second one on top of itself.
        Assert.True(Named(view, "Error").IsVisible);
    }

    public void Dispose() => _settings.Dispose();

    private FirstRunViewModel Create(FakeFolderPicker? picker = null)
        => new(
            _settings.Config,
            new FakeThemeService(),
            new FakeWorkspaceService(),
            picker ?? new FakeFolderPicker(),
            NullLogger<FirstRunViewModel>.Instance);

    // The rows themselves, and then their own Content: a SettingsCard keeps what it hosts behind a
    // template, so a walk of the tree does not reach it — the same route
    // SettingsPreviewSectionTests takes through the settings window.
    private static SettingsCard Row(FirstRunView view, string name)
        => view.GetLogicalDescendants().OfType<SettingsCard>().Single(card => card.Name == name);

    private static IEnumerable<SettingsCard> Rows(FirstRunView view)
        => view.GetLogicalDescendants().OfType<SettingsCard>();

    // The folder row hosts two controls rather than one, so its path is reached through the panel.
    private static TextBlock FolderPath(FirstRunView view)
        => Assert.IsType<StackPanel>(Row(view, "FolderRow").Content)
            .Children
            .OfType<TextBlock>()
            .Single();

    // By name rather than through a generated field: what is asserted is the markup, and a control that
    // lost its x:Name should fail this rather than stop compiling somewhere else.
    private static Control Named(FirstRunView view, string name)
        => view.GetLogicalDescendants().OfType<Control>().Single(control => control.Name == name);

    // Inside a window, and shown: IsVisible and the item containers are what a layout pass produced,
    // not what the markup asked for.
    private static FirstRunView Show(FirstRunViewModel viewModel)
    {
        FirstRunView view = new() { DataContext = viewModel };
        Window window = new() { Content = view };

        window.Show();

        return view;
    }
}
