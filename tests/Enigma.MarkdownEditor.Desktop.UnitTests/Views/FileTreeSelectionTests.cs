using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Enigma.MarkdownEditor.Core.Settings;
using Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;
using Enigma.MarkdownEditor.Desktop.ViewModels;
using Enigma.MarkdownEditor.Desktop.Views;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Views;

/// <summary>
/// The tree's multi-selection, through the real view and real pointer input.
/// </summary>
/// <remarks>
/// What the ViewModel tests cannot vouch for: that the control and the ViewModel really share one
/// selection — a list binding that silently did not take would leave the tree selecting rows the
/// ViewModel never hears of — and that the gestures a user makes land in it. The clicks go through the
/// headless platform's input, so the control's own selection rules are what is exercised, not a
/// shortcut into the collection.
/// </remarks>
public sealed class FileTreeSelectionTests : IDisposable
{
    private readonly string _root;
    private readonly FakeUiDispatcher _dispatcher = new();
    private readonly FakeDirectoryWatcherFactory _watchers = new();
    private readonly FakeWorkspaceService _workspaces = new();

    private Window _window = null!;
    private TreeView _tree = null!;

    public FileTreeSelectionTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "Enigma.MarkdownEditor.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [AvaloniaFact]
    public async Task TheTree_LetsSeveralRowsBeSelected()
    {
        using FileTreeViewModel viewModel = await ShowAsync();

        Assert.Equal(SelectionMode.Multiple, _tree.SelectionMode);
    }

    [AvaloniaFact]
    public async Task TheTree_WorksOnTheViewModelsOwnSelection()
    {
        using FileTreeViewModel viewModel = await ShowAsync();
        FileTreeNodeViewModel a = Row(viewModel, "a.md");
        FileTreeNodeViewModel b = Row(viewModel, "b.md");

        Assert.Same(viewModel.SelectedNodes, _tree.SelectedItems);

        _tree.SelectedItems.Add(a);
        _tree.SelectedItems.Add(b);
        Assert.Equal([a, b], viewModel.SelectedNodes);
        Assert.Same(a, _tree.SelectedItem);

        viewModel.SelectedNode = b;
        Assert.Same(b, _tree.SelectedItem);
        Assert.Equal([b], _tree.SelectedItems.Cast<FileTreeNodeViewModel>());
    }

    [AvaloniaFact]
    public async Task CtrlClick_AddsARow_AndAPlainClickStartsOver()
    {
        using FileTreeViewModel viewModel = await ShowAsync();
        FileTreeNodeViewModel a = Row(viewModel, "a.md");
        FileTreeNodeViewModel b = Row(viewModel, "b.md");

        Click(a, MouseButton.Left);
        Click(b, MouseButton.Left, RawInputModifiers.Control);
        Assert.Equal([a, b], viewModel.SelectedNodes);

        Click(a, MouseButton.Left);
        Assert.Equal([a], viewModel.SelectedNodes);
    }

    [AvaloniaFact]
    public async Task ShiftClick_SelectsTheRangeBetween()
    {
        using FileTreeViewModel viewModel = await ShowAsync();

        Click(Row(viewModel, "a.md"), MouseButton.Left);
        Click(Row(viewModel, "c.md"), MouseButton.Left, RawInputModifiers.Shift);

        Assert.Equal(
            ["a.md", "b.md", "c.md"],
            viewModel.SelectedNodes.Select(node => node.Name).Order(StringComparer.Ordinal));
    }

    [AvaloniaFact]
    public async Task RightClickingInsideTheSelection_KeepsIt()
    {
        using FileTreeViewModel viewModel = await ShowAsync();
        FileTreeNodeViewModel a = Row(viewModel, "a.md");
        FileTreeNodeViewModel b = Row(viewModel, "b.md");

        viewModel.SelectedNodes.Add(a);
        viewModel.SelectedNodes.Add(b);

        Click(b, MouseButton.Right);

        Assert.Equal([a, b], viewModel.SelectedNodes);
    }

    [AvaloniaFact]
    public async Task RightClickingOutsideTheSelection_SelectsThatRowAlone()
    {
        using FileTreeViewModel viewModel = await ShowAsync();
        FileTreeNodeViewModel c = Row(viewModel, "c.md");

        viewModel.SelectedNodes.Add(Row(viewModel, "a.md"));
        viewModel.SelectedNodes.Add(Row(viewModel, "b.md"));

        Click(c, MouseButton.Right);

        Assert.Equal([c], viewModel.SelectedNodes);
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

    private static FileTreeNodeViewModel Row(FileTreeViewModel viewModel, string name)
        => viewModel.Roots[0].Children.Single(child => child.Name == name);

    // Pressed and released over the middle of the row's name, which is what a user aims at — and
    // where neither the expander nor the colour bar is.
    private void Click(FileTreeNodeViewModel node, MouseButton button, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        TextBlock name = _tree.GetVisualDescendants()
            .OfType<TextBlock>()
            .Single(text => ReferenceEquals(text.DataContext, node) && (text.Text == node.Name));

        Point centre = name.TranslatePoint(new Point(name.Bounds.Width / 2, name.Bounds.Height / 2), _window)
                       ?? throw new InvalidOperationException($"'{node.Name}' is not on screen.");

        _window.MouseDown(centre, button, modifiers);
        _window.MouseUp(centre, button, modifiers);

        // A right-click opens the tree's context menu, which would sit over the next click.
        _tree.ContextMenu?.Close();
    }

    private async Task<FileTreeViewModel> ShowAsync()
    {
        string workspace = Path.Combine(_root, "notes");
        Directory.CreateDirectory(workspace);

        foreach (string name in new[] { "a.md", "b.md", "c.md" })
        {
            File.WriteAllText(Path.Combine(workspace, name), "# note");
        }

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

        FileTreeView view = new() { DataContext = viewModel };
        _window = new Window { Content = view, Width = 400, Height = 400 };
        _window.Show();
        _window.UpdateLayout();

        _tree = view.GetVisualDescendants().OfType<TreeView>().Single();

        return viewModel;
    }
}
