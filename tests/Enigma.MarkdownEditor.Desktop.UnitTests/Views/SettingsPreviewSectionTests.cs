using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Enigma.Avalonia.Desktop.Controls;
using Enigma.Avalonia.Desktop.Controls.Editors;
using Enigma.Configuration;
using Enigma.MarkdownEditor.Core.Settings;
using Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;
using Enigma.MarkdownEditor.Desktop.ViewModels;
using Enigma.MarkdownEditor.Desktop.Views;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Views;

/// <summary>
/// The settings dialog's "Preview" group, through the real view.
/// </summary>
/// <remarks>
/// The one thing <see cref="SettingsViewModelTests"/> cannot say: that the rows exist and are
/// wired to their own properties. The bindings are compiled — <c>x:DataType</c> and
/// <c>AvaloniaUseCompiledBindingsByDefault</c> — so a wrong property name or a wrong type is a build
/// failure rather than a runtime one; what is left for a test is that the markup loads at all and
/// that each control found what it was pointed at. How the group looks is a manual reading.
/// </remarks>
public sealed class SettingsPreviewSectionTests : IDisposable
{
    private readonly string _root;

    public SettingsPreviewSectionTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "Enigma.MarkdownEditor.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [AvaloniaFact]
    public void ThePreviewGroup_SitsBesideTheEditorOne()
    {
        Show(out SettingsView view);

        string[] groups = [.. Groups(view).Select(group => group.Header?.ToString() ?? string.Empty)];

        Assert.Equal(["Appearance", "Editor", "Preview", "Behaviour", "Advanced"], groups);
    }

    [AvaloniaFact]
    public void ThePreviewGroup_HasARowPerSetting()
    {
        Show(out SettingsView view);

        // The two code rows sit together, under the two prose ones.
        Assert.Equal(
            ["Font family", "Font size", "Code font family", "Code font size", "Line height"],
            Rows(view).Select(row => row.Header?.ToString() ?? string.Empty));
    }

    [AvaloniaFact]
    public void EachRow_IsWiredToItsOwnSetting()
    {
        SettingsViewModel viewModel = Show(out SettingsView view);

        Control[] editors = [.. Rows(view).Select(row => (Control)row.Content!)];

        // Two font pickers of the preview's own, offering different lists: prose is chosen from every
        // installed family and code from the fixed-width ones.
        ComboBox prose = Assert.IsType<ComboBox>(editors[0]);
        ComboBox code = Assert.IsType<ComboBox>(editors[2]);

        Assert.Same(viewModel.PreviewFontChoices, prose.ItemsSource);
        Assert.Same(viewModel.PreviewFontChoice, prose.SelectedItem);
        Assert.Same(viewModel.PreviewCodeFontChoices, code.ItemsSource);
        Assert.Same(viewModel.PreviewCodeFontChoice, code.SelectedItem);

        Assert.Equal(viewModel.PreviewFontSize, Assert.IsType<IntEditor>(editors[1]).Value);
        Assert.Equal(viewModel.PreviewCodeFontSize, Assert.IsType<IntEditor>(editors[3]).Value);
        Assert.Equal(viewModel.PreviewLineHeight, Assert.IsType<DoubleEditor>(editors[4]).Value);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A leftover temp folder is not worth failing a green test over.
        }
    }

    // The groups' own content, rather than a walk of the view: an expander keeps its content behind
    // a template, and what is being asserted is the markup rather than what a layout pass realized.
    private static IEnumerable<SettingsCardExpander> Groups(SettingsView view)
        => view.GetLogicalDescendants().OfType<SettingsCardExpander>();

    private static IEnumerable<SettingsCard> Rows(SettingsView view)
        => Groups(view)
            .Single(group => group.Header?.ToString() == "Preview")
            .Content is StackPanel panel
            ? panel.Children.OfType<SettingsCard>()
            : [];

    // A UserControl since FEATURE-414B PHASE02, so it is hosted in a plain window here the way the
    // other dialog views are — the real one is the ContentDialog in the main window.
    private SettingsViewModel Show(out SettingsView view)
    {
        IConfig<AppSettings> config = SettingsLoader.Load(Path.Combine(_root, AppPaths.SettingsFileName)).Config;

        SettingsViewModel viewModel = new(
            config,
            new FakeThemeService(),
            TestFonts.Catalog(),
            NullLogger<SettingsViewModel>.Instance);

        view = new SettingsView { DataContext = viewModel };
        Window window = new() { Content = view };

        window.Show();

        return viewModel;
    }
}
