using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using Enigma.MarkdownEditor.Core.Settings;
using Enigma.MarkdownEditor.Core.Workspaces;
using Enigma.MarkdownEditor.Desktop.Controls;
using Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;
using Enigma.MarkdownEditor.Desktop.ViewModels;
using Enigma.MarkdownEditor.Desktop.Views;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Views;

/// <summary>
/// The tree's <em>Colour</em> submenu, through the real view.
/// </summary>
/// <remarks>
/// <para>
/// The one part of this feature a compiled binding cannot vouch for. Each item carries its colour as
/// an <c>{x:Static}</c> command parameter into a command typed on a nullable enum, and a mismatch
/// there does not fail the build — it produces nine items that are drawn disabled, or one exception
/// per click. So the menu is opened for real and the parameters and the command are read off the
/// items the markup produced.
/// </para>
/// <para>
/// The swatches are read the same way, because they are the second half of the same claim: the colour
/// a user picks by is the colour the row is then painted with, and the two come from one place only if
/// the <c>{x:Static}</c> in the markup and <see cref="FolderColorPalette.Brush"/> agree.
/// </para>
/// </remarks>
public sealed class FileTreeColourMenuTests : IDisposable
{
    private static readonly FolderColor[] Offered =
    [
        FolderColor.None,
        FolderColor.Red,
        FolderColor.Orange,
        FolderColor.Yellow,
        FolderColor.Green,
        FolderColor.Teal,
        FolderColor.Blue,
        FolderColor.Purple,
        FolderColor.Pink,
    ];

    private readonly string _root;
    private readonly FakeUiDispatcher _dispatcher = new();
    private readonly FakeDirectoryWatcherFactory _watchers = new();
    private readonly FakeWorkspaceService _workspaces = new();

    public FileTreeColourMenuTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "Enigma.MarkdownEditor.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [AvaloniaFact]
    public async Task TheMenu_OffersNoneAndTheEightColours()
    {
        using FileTreeViewModel tree = await ShowAsync();

        Assert.Equal(
            ["None", "Red", "Orange", "Yellow", "Green", "Teal", "Blue", "Purple", "Pink"],
            ColourItems().Select(item => item.Header?.ToString() ?? string.Empty));
    }

    [AvaloniaFact]
    public async Task EachItem_CarriesItsOwnColourAsTheCommandsParameter()
    {
        using FileTreeViewModel tree = await ShowAsync();

        // A string that looks like the enum's name would compile and then be refused at execution
        // time, so the parameter is asserted as the typed value it has to be.
        Assert.Equal(Offered, [.. ColourItems().Select(item => Assert.IsType<FolderColor>(item.CommandParameter))]);
    }

    [AvaloniaFact]
    public async Task EachItem_IsWiredToTheCommandAndReadyToRun()
    {
        using FileTreeViewModel tree = await ShowAsync();

        tree.SelectedNode = tree.Roots[0];

        foreach (MenuItem item in ColourItems())
        {
            Assert.Same(tree.SetColorCommand, item.Command);

            // What a non-nullable enum parameter would get wrong: the toolkit's command refuses a
            // null parameter outright for a value type, and every item would be drawn disabled.
            Assert.True(item.Command?.CanExecute(item.CommandParameter));
        }
    }

    [AvaloniaFact]
    public async Task WithNothingColourable_TheWholeSubmenuIsDisabled()
    {
        using FileTreeViewModel tree = await ShowAsync();
        MenuItem colour = ColourMenu();

        // Nothing selected yet, and a lone root is deliberately not assumed the way it is for
        // "New file": colouring "this folder" has to mean the folder that was right-clicked.
        Assert.False(colour.IsEnabled);

        tree.SelectedNode = tree.Roots[0].Children.First(child => child.IsDirectory);
        Assert.True(colour.IsEnabled);

        tree.SelectedNode = tree.Roots[0].Children.First(child => child.IsFile);
        Assert.False(colour.IsEnabled);
    }

    [AvaloniaFact]
    public async Task TheSwatches_ArePaintedInTheVerySameBrushesTheRowsUse()
    {
        using FileTreeViewModel tree = await ShowAsync();

        foreach (MenuItem item in ColourItems())
        {
            FolderColor colour = Assert.IsType<FolderColor>(item.CommandParameter);
            IBrush? expected = FolderColorPalette.Brush(colour);

            if (expected is null)
            {
                // 'None' has no swatch to show, and a Border with a null background would be an empty
                // box in the icon column rather than nothing.
                Assert.Null(item.Icon);

                continue;
            }

            Assert.Same(expected, Assert.IsType<Border>(item.Icon).Background);
        }
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

    private FileTreeView View { get; set; } = null!;

    // The submenu's own item, found by its header because that is what the markup declares.
    private MenuItem ColourMenu()
        => Menu().Items.OfType<MenuItem>().Single(item => item.Header?.ToString() == "Colour");

    private IEnumerable<MenuItem> ColourItems() => ColourMenu().Items.OfType<MenuItem>();

    private ContextMenu Menu()
        => View.GetVisualDescendants().OfType<TreeView>().Single().ContextMenu
            ?? throw new InvalidOperationException("The tree has no context menu.");

    // Opened for real: a ContextMenu lives in a popup of its own rather than in the tree's visual
    // chain, so nothing in it is bound — Command included — until it is up.
    private async Task<FileTreeViewModel> ShowAsync()
    {
        string workspace = Path.Combine(_root, "notes");
        Directory.CreateDirectory(workspace);
        Directory.CreateDirectory(Path.Combine(workspace, "drafts"));
        File.WriteAllText(Path.Combine(workspace, "a.md"), "# a");

        _workspaces.Seed("Work", active: true, workspace);

        FileTreeViewModel viewModel = new(
            _workspaces,
            SettingsLoader.Load(Path.Combine(_root, AppPaths.SettingsFileName)).Config,
            _watchers,
            new FakeDocumentOpener(),
            new FakeFileOperationPrompts(),
            new FakeTrashService(),
            _dispatcher,
            new FakeInfoBarService(),
            NullLogger<FileTreeViewModel>.Instance);

        await _dispatcher.DrainAsync();

        View = new FileTreeView { DataContext = viewModel };
        Window window = new() { Content = View };
        window.Show();

        TreeView tree = View.GetVisualDescendants().OfType<TreeView>().Single();
        Menu().Open(tree);
        window.UpdateLayout();

        return viewModel;
    }
}
