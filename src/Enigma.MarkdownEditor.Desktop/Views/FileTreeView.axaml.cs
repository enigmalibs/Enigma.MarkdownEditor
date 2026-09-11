using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Enigma.MarkdownEditor.Desktop.ViewModels;

namespace Enigma.MarkdownEditor.Desktop.Views;

/// <summary>
/// The sidebar's workspace file tree.
/// </summary>
/// <remarks>
/// <para>
/// The handlers here are the ones a binding cannot express: a double-click and the Enter key both mean
/// "open this" and neither is a command a <see cref="TreeView"/> exposes; F2 and Delete are keys, not
/// bindings; and a drag is a sequence of pointer events with no ViewModel-shaped equivalent at all.
/// Everything else — the contents, the expansion, the selection, the icons, the context menu's own
/// enabling — is bound.
/// </para>
/// <para>
/// No decision is taken here. A drop asks <see cref="FileTreeViewModel.CanAcceptDrop"/> only to decide
/// what cursor to show; whether the move actually happens, and what it means, is the ViewModel's and
/// the planner's to say.
/// </para>
/// </remarks>
public partial class FileTreeView : UserControl
{
    /// <summary>
    /// The format one dragged tree row travels in: its path, and nothing else. An application format
    /// rather than a platform one, because a drag between this tree and any other program is not
    /// something this view offers — and the identifier's alphabet is the one Avalonia allows.
    /// </summary>
    private static readonly DataFormat<string> PathFormat =
        DataFormat.CreateStringApplicationFormat("enigma-markdowneditor.tree-path");

    /// <summary>
    /// How far the pointer has to travel with the button down before it counts as a drag rather than
    /// as a click that wobbled.
    /// </summary>
    private const double DragThreshold = 4;

    private FileTreeNodeViewModel? _dragCandidate;
    private PointerPressedEventArgs? _dragTrigger;
    private Point _dragOrigin;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileTreeView"/> class.
    /// </summary>
    public FileTreeView()
    {
        InitializeComponent();

        // Attached in code because Avalonia's XAML has no syntax for a handler on an attached routed
        // event. Both are attached to the tree rather than to each row: rows come and go with every
        // listing, and the events bubble to here anyway.
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);

        // Tunnelling, which the XAML attribute form cannot express: a TreeViewItem marks
        // PointerPressed as handled in order to select itself, so a bubble-phase handler never sees
        // the press a drag has to start from. Going down the tree instead reaches it first — and
        // since none of these three mark the event handled, selection and expansion still work
        // exactly as before.
        AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel);
    }

    // The sender is the row's own Grid, so its DataContext is the node that was clicked — which is
    // more reliable than the tree's selection, whose update order relative to the click is not
    // guaranteed.
    private void OnNodeDoubleTapped(object? sender, TappedEventArgs e)
    {
        if ((sender is not Control row) ||
            (row.DataContext is not FileTreeNodeViewModel node) ||
            (DataContext is not FileTreeViewModel viewModel))
        {
            return;
        }

        viewModel.OpenCommand.Execute(node);
        e.Handled = true;
    }

    // A right-click does not move the selection on its own, and the context menu acts on the
    // selection — so without this, the menu would apply to whatever was selected before.
    private void OnNodeContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if ((sender is Control { DataContext: FileTreeNodeViewModel node }) &&
            (DataContext is FileTreeViewModel viewModel))
        {
            viewModel.SelectedNode = node;
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not FileTreeViewModel viewModel)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Enter when viewModel.SelectedNode is { } node:
                viewModel.OpenCommand.Execute(node);
                break;

            case Key.F2 when viewModel.RenameCommand.CanExecute(null):
                viewModel.RenameCommand.Execute(null);
                break;

            case Key.Delete when viewModel.DeleteCommand.CanExecute(null):
                viewModel.DeleteCommand.Execute(null);
                break;

            default:
                return;
        }

        e.Handled = true;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _dragCandidate = null;
            return;
        }

        // Remembered, not acted on: this is also how every ordinary click starts, and only the
        // movement in OnPointerMoved tells the two apart. Starting the drag here instead would put a
        // modal drag loop in front of every click on an expander.
        _dragCandidate = NodeAt(e.Source);
        _dragTrigger = e;
        _dragOrigin = e.GetPosition(this);
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e) => ForgetDrag();

    private async void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if ((_dragCandidate is not { } node) ||
            (_dragTrigger is not { } trigger) ||
            !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        // A root is a workspace folder, and moving one would say nothing about the workspaces file
        // that owns it. Informational rows are not entries at all.
        if (node.IsRoot || !(node.IsDirectory || node.IsFile))
        {
            ForgetDrag();
            return;
        }

        Point position = e.GetPosition(this);

        if ((Math.Abs(position.X - _dragOrigin.X) < DragThreshold) &&
            (Math.Abs(position.Y - _dragOrigin.Y) < DragThreshold))
        {
            return;
        }

        // Cleared before the await, not after: the drag runs its own nested event loop, and a second
        // one started from inside the first would be a drag of a drag.
        ForgetDrag();

        // The press is what a drag is started from, not the move that revealed it to be one — so the
        // original event is kept from OnPointerPressed and handed over here, once the pointer has
        // travelled far enough to mean it.
        DataTransfer data = new();
        data.Add(DataTransferItem.Create(PathFormat, node.FullPath));

        try
        {
            await DragDrop.DoDragDropAsync(trigger, data, DragDropEffects.Move);
        }
        catch (Exception)
        {
            // Swallowed deliberately, and this is the one place in the application where that is the
            // right answer: DoDragDrop goes through platform code whose failures are not a documented
            // set, and a drag that never started is a gesture that did not happen. Nothing has been
            // moved, nothing is half-done, and there is nothing for the user to act on — whereas an
            // exception escaping an async void handler would take the editor down.
        }
    }

    private void ForgetDrag()
    {
        _dragCandidate = null;
        _dragTrigger = null;
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = IsDropAllowed(e) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        e.Handled = true;

        if ((DataContext is not FileTreeViewModel viewModel) ||
            !IsDropAllowed(e) ||
            (SourcePathOf(e.DataTransfer) is not { } source) ||
            (TargetDirectoryOf(NodeAt(e.Source)) is not { } target))
        {
            return;
        }

        // Discarded on purpose: MoveAsync reports its own failures through the InfoBar, and a drop is
        // not something the view can wait on.
        _ = viewModel.MoveAsync(source, target);
    }

    private bool IsDropAllowed(DragEventArgs e)
        => FileTreeViewModel.CanAcceptDrop(SourcePathOf(e.DataTransfer), TargetDirectoryOf(NodeAt(e.Source)));

    private static string? SourcePathOf(IDataTransfer data) => data.TryGetValue(PathFormat);

    // Where a drop on this row would put things: into a folder, or beside the file that was dropped
    // on. A row that describes a folder rather than listing it — "Nothing here", "No access" — carries
    // that folder's own path, which is exactly the answer.
    private static string? TargetDirectoryOf(FileTreeNodeViewModel? node)
        => node switch
        {
            null => null,
            { IsDirectory: true } => node.FullPath,
            { IsFile: true } => Path.GetDirectoryName(node.FullPath),
            { IsInformational: true } => node.FullPath,
            _ => null,
        };

    private static FileTreeNodeViewModel? NodeAt(object? source)
    {
        for (Visual? visual = source as Visual; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is Control { DataContext: FileTreeNodeViewModel node })
            {
                return node;
            }
        }

        return null;
    }
}
