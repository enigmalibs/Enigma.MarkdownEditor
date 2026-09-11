using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Threading;
using Enigma.MarkdownEditor.Core.Session;
using Enigma.MarkdownEditor.Core.Settings;
using Enigma.MarkdownEditor.Desktop.Controls;
using Enigma.MarkdownEditor.Desktop.ViewModels;

namespace Enigma.MarkdownEditor.Desktop.Views;

/// <summary>
/// The application shell window: toolbar, three panes with splitters, and the status bar.
/// </summary>
/// <remarks>
/// <para>
/// Most of the logic here is mirroring the ViewModel's pane flags onto the grid's column definitions.
/// Hiding a pane is not enough on its own — a <see cref="Grid"/> column keeps whatever width it was
/// given, so a collapsed pane would leave a gap. Column widths cannot be bound instead:
/// <see cref="ColumnDefinition"/> is not part of the visual tree, so no DataContext reaches it.
/// </para>
/// <para>
/// The rest is the two pieces of input the window itself has to own: the closing handshake, which has
/// to be able to refuse to close, and the tab-switching gesture, which has to be taken before focus
/// navigation claims it.
/// </para>
/// <para>
/// And the window's own half of the saved session. Where the window is and how the columns are divided
/// are facts only the view holds — a <see cref="ColumnDefinition"/> is not in the visual tree, and a
/// screen arrangement is not something a ViewModel can see — so the window reads them, hands them to
/// the shell, and applies what the shell hands back.
/// </para>
/// </remarks>
public partial class MainWindow : Window
{
    private static readonly GridLength Collapsed = new(0, GridUnitType.Pixel);

    // Captured from the XAML so it stays the single source of the pane defaults, and updated
    // whenever a pane is collapsed so a splitter drag survives a hide/show round trip.
    private GridLength _sidebarWidth;
    private GridLength _editorWidth;
    private GridLength _previewWidth;
    private readonly double _sidebarMinWidth;
    private readonly double _editorMinWidth;
    private readonly double _previewMinWidth;

    private MainWindowViewModel? _viewModel;

    // The window's bounds as they were the last time it was not maximized, which is what a session
    // remembers: unmaximizing after a restart has to put the window back where it was, and a maximized
    // window reports the screen's geometry rather than its own.
    private WindowPlacement? _normalBounds;

    // Nothing is recorded until the window has finished opening. Everything before that is the window
    // being built rather than the user moving anything, and recording it would rewrite the session at
    // every start.
    private bool _opened;

    // The closing handshake is asynchronous and the event is not, so the first attempt is always
    // cancelled and the window closed again once the questions have been answered. These two say
    // which pass we are on, and stop a second Alt+F4 from opening a second stack of dialogs.
    private bool _closeConfirmed;
    private bool _closing;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();

        _sidebarWidth = SidebarColumn.Width;
        _editorWidth = EditorColumn.Width;
        _previewWidth = PreviewColumn.Width;
        _sidebarMinWidth = SidebarColumn.MinWidth;
        _editorMinWidth = EditorColumn.MinWidth;
        _previewMinWidth = PreviewColumn.MinWidth;

        // Tunnelling, not bubbling: Ctrl+Tab is focus navigation to Avalonia, and by the time a
        // bubbling handler or a KeyBinding sees it the focus has already moved somewhere else.
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);

        // The preview renderer is built by the XAML, so it has no logger of its own; the pane's
        // ViewModel is what records the failures it survived. Same seam the editor pane reports a
        // highlighting failure through. Never detached: the control dies with this window.
        PreviewRenderer.RenderFailed += OnPreviewRenderFailed;

        // Same seam, same reason: the renderer reports the click and the pane's ViewModel decides
        // whether it means the browser, a tab or the operating system.
        PreviewRenderer.LinkActivated += OnPreviewLinkActivated;

        // And again for a task box: the renderer knows where the character was in the markdown it
        // was handed, and the pane's ViewModel is what knows which buffer that markdown came from.
        PreviewRenderer.TaskToggled += OnPreviewTaskToggled;

        // And once more for a copy: the renderer resolves whether it is the selection or the whole
        // note, because only it knows what is selected, and the pane's ViewModel is what reaches the
        // clipboard. All three surfaces — the gesture, the header button and the context menu — come
        // through here, so the rule cannot come out differently on one of them.
        PreviewRenderer.CopyRequested += OnPreviewCopyRequested;

        // A splitter drag is the only thing that changes how the columns are divided, and it changes
        // nothing else — no resize, no move — so it is the one event the pane layout is recorded from.
        SidebarSplitter.DragCompleted += (_, _) => RecordPanes();
        PreviewSplitter.DragCompleted += (_, _) => RecordPanes();

        // The window's own geometry, from both of the ways it changes. Both are debounced by the
        // session service, so a drag across the desktop is one write rather than one per frame.
        PositionChanged += (_, _) => RecordPlacement();
        Resized += (_, _) => RecordPlacement();

        // Scroll sync, and the only place the two panes talk to each other. It is wired here rather
        // than through the ViewModels because it is a view concern from end to end: a line number out
        // of one control and an offset into another, with nothing in between that a ViewModel could
        // own. Never detached: both controls die with this window.
        Editor.TopVisibleLineChanged += OnEditorTopVisibleLineChanged;
    }

    private ColumnDefinition SidebarColumn => PaneGrid.ColumnDefinitions[Grid.GetColumn(SidebarPane)];

    private ColumnDefinition EditorColumn => PaneGrid.ColumnDefinitions[Grid.GetColumn(EditorPane)];

    private ColumnDefinition PreviewColumn => PaneGrid.ColumnDefinitions[Grid.GetColumn(PreviewPane)];

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as MainWindowViewModel;

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

            // In this order: the remembered widths replace the XAML defaults, ApplyPaneWidths then
            // puts them onto the columns that are actually shown, and the window is placed last
            // because placing it is the only step that can be seen.
            RestorePanes(_viewModel.RestoredPanes);
            ApplyPaneWidths(_viewModel);
            RestoreWindow(_viewModel.RestoredWindow);
        }

        base.OnDataContextChanged(e);
    }

    /// <inheritdoc />
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        // Seeded from the window as it actually opened, for the first run that had nothing to restore.
        // Without it, maximizing before ever moving the window would be remembered as a maximized
        // window with no bounds to come back to.
        _normalBounds ??= CurrentBounds();
        _opened = true;

        // Not awaited, and deliberately: OnOpened is void, and behind it are an InfoBar and the tabs
        // the last session left open. Failures inside it reach the Dispatcher's unhandled-exception
        // handler, which is installed before this window is ever shown.
        _ = _viewModel?.OnOpenedAsync();
    }

    /// <inheritdoc />
    /// <remarks>
    /// The one place a document's unsaved work can still be rescued. Cancelling the event is what
    /// makes "Cancel" in the prompt mean the application keeps running — so the close is always
    /// refused first, and only granted once every dirty tab has been answered for.
    /// </remarks>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (!_closeConfirmed && (_viewModel?.Documents is { HasUnsavedChanges: true } documents))
        {
            e.Cancel = true;

            if (!_closing)
            {
                _closing = true;

                // Not awaited, and deliberately: OnClosing is void. The task closes the window itself
                // when the user says so, and any failure inside it reaches the Dispatcher's unhandled
                // exception handler, which is installed before this window is ever shown.
                _ = ConfirmThenCloseAsync(documents);
            }

            return;
        }

        // The session's last word. Here rather than in OnClosed because a window that has closed no
        // longer has a position to read.
        RecordPlacement();
        RecordPanes();

        base.OnClosing(e);
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        // The ViewModel outlives the window (it is a container singleton), so leaving this handler
        // attached would keep the whole visual tree alive.
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel = null;
        }

        base.OnClosed(e);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // Maximizing and restoring change what the session has to remember without necessarily
        // changing the bounds it remembers, so the state gets its own trigger.
        if (change.Property == WindowStateProperty)
        {
            RecordPlacement();
        }
    }

    // A button inside a flyout does not dismiss it: the command runs and the flyout stays open over
    // whatever it just changed. Both of the switcher's rows close it from here — through the button
    // that owns the flyout, because a flyout is not in the window's name scope and so has no field of
    // its own.
    //
    // Posted rather than called: Click is raised BEFORE the button executes its Command, and closing
    // the flyout tears the row out of the tree, which clears the very Command and CommandParameter
    // bindings the button is about to read. Hiding on the next dispatcher pass lets the click finish
    // first — without it, clicking a workspace in the switcher does nothing at all.
    private void OnWorkspaceFlyoutActionClick(object? sender, RoutedEventArgs e)
        => Dispatcher.UIThread.Post(() => WorkspaceSwitcherButton.Flyout?.Hide());

    private async Task ConfirmThenCloseAsync(DocumentManagerViewModel documents)
    {
        try
        {
            if (!await documents.ConfirmCloseAllAsync())
            {
                return;
            }

            _closeConfirmed = true;
            Close();
        }
        finally
        {
            // Cleared either way: a user who cancelled must be able to try again.
            _closing = false;
        }
    }

    private void OnPreviewRenderFailed(object? sender, Exception failure)
        => _viewModel?.Preview.OnRenderFailed(failure);

    // Not awaited, and deliberately: this is a pointer handler. Everything the activation can fail at
    // is reported inside it — in the info bar for a file, in the log for an address — and anything it
    // does not catch reaches the Dispatcher's unhandled-exception handler.
    private void OnPreviewLinkActivated(object? sender, string url)
        => _ = _viewModel?.Preview.ActivateLinkAsync(url);

    // Synchronous, unlike the link above, and it has to be: the renderer has already flipped the box
    // and needs the answer now to decide whether to leave it flipped. Writing one character into a
    // buffer already in memory is not work worth deferring anyway.
    private void OnPreviewTaskToggled(object? sender, MarkdownTaskToggledEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        e.Applied = _viewModel?.Preview.ToggleTask(e.SourceOffset, e.IsChecked) ?? false;
    }

    // Not awaited, for the same reason the link activation is not: this is reached from a key
    // handler and from a button, and everything the write can fail at is reported inside it.
    private void OnPreviewCopyRequested(object? sender, MarkdownCopyRequest request)
        => _ = _viewModel?.Preview.CopyAsync(request);

    // The button in the pane's caption and the context menu's Copy, which are the same act as the
    // gesture: the renderer decides what the copy is of and reports it back through the handler
    // above. Serving both from one handler is what keeps them from drifting apart.
    private void OnPreviewCopyClick(object? sender, RoutedEventArgs e) => PreviewRenderer.RequestCopy();

    private void OnPreviewSelectAllClick(object? sender, RoutedEventArgs e) => PreviewRenderer.SelectAll();

    // In Split only. There is nothing to track in the other two modes — one of the panes is not on
    // screen — and following a hidden editor would leave the preview somewhere the reader never
    // scrolled it to when they came back.
    private void OnEditorTopVisibleLineChanged(object? sender, int line)
    {
        if (_viewModel?.ViewMode == ViewMode.Split)
        {
            PreviewRenderer.ScrollToSourceLine(line);
        }
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if ((e.Key != Key.Tab) || !e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            return;
        }

        if (_viewModel?.Documents is not { } documents)
        {
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            documents.PreviousDocumentCommand.Execute(null);
        }
        else
        {
            documents.NextDocumentCommand.Execute(null);
        }

        e.Handled = true;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not MainWindowViewModel viewModel)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(MainWindowViewModel.IsSidebarVisible):
                ApplyPaneWidth(SidebarColumn, viewModel.IsSidebarVisible, ref _sidebarWidth, _sidebarMinWidth);
                break;
            case nameof(MainWindowViewModel.IsEditorVisible):
                ApplyPaneWidth(EditorColumn, viewModel.IsEditorVisible, ref _editorWidth, _editorMinWidth);
                break;
            case nameof(MainWindowViewModel.IsPreviewVisible):
                ApplyPaneWidth(PreviewColumn, viewModel.IsPreviewVisible, ref _previewWidth, _previewMinWidth);
                break;
            case nameof(MainWindowViewModel.ViewMode):
                // Entering Split is the one moment the sync has nothing to react to: the editor has
                // not scrolled, so it will not report a line, and the preview would otherwise sit
                // wherever it was left. This raises the notification the mode change did not.
                if (viewModel.ViewMode == ViewMode.Split)
                {
                    PreviewRenderer.ScrollToSourceLine(Editor.TopVisibleLine);
                }

                break;
            default:
                break;
        }
    }

    private void RestorePanes(PaneLayout? panes)
    {
        if (panes is null)
        {
            return;
        }

        if (panes.SidebarWidth > 0)
        {
            // Never narrower than the column's own minimum: a sidebar remembered from a build with a
            // smaller minimum would otherwise be restored to a width the grid refuses to give it.
            _sidebarWidth = new GridLength(Math.Max(panes.SidebarWidth, _sidebarMinWidth), GridUnitType.Pixel);
        }

        // Both or neither: the two are a ratio, and half of one is a window with a pane taking
        // everything.
        if ((panes.EditorWeight > 0) && (panes.PreviewWeight > 0))
        {
            _editorWidth = new GridLength(panes.EditorWeight, GridUnitType.Star);
            _previewWidth = new GridLength(panes.PreviewWeight, GridUnitType.Star);
        }
    }

    private void RestoreWindow(WindowPlacement? placement)
    {
        double scaling = Screens.Primary?.Scaling ?? RenderScaling;

        WindowPlacement? clamped = WindowPlacements.Clamp(
            placement,
            ScreenAreas(),
            ToDevicePixels(MinWidth, scaling),
            ToDevicePixels(MinHeight, scaling));

        if (clamped is null)
        {
            return;
        }

        // Manual, or the startup location would centre the window over the position just set.
        WindowStartupLocation = WindowStartupLocation.Manual;
        Position = new PixelPoint(clamped.X, clamped.Y);

        // The size is stored in device pixels and set in layout units, and the factor between them
        // belongs to the screen the window has just landed on — which is not necessarily the one it
        // was saved from.
        double landed = ScalingAt(Position);
        Width = clamped.Width / landed;
        Height = clamped.Height / landed;

        _normalBounds = clamped;

        if (clamped.IsMaximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    private void RecordPlacement()
    {
        if ((_viewModel is null) || !_opened)
        {
            return;
        }

        if (WindowState == WindowState.Normal)
        {
            _normalBounds = CurrentBounds();
        }

        if (_normalBounds is not { } bounds)
        {
            return;
        }

        _viewModel.ReportWindowPlacement(new WindowPlacement
        {
            X = bounds.X,
            Y = bounds.Y,
            Width = bounds.Width,
            Height = bounds.Height,
            IsMaximized = WindowState == WindowState.Maximized,
        });
    }

    private void RecordPanes()
    {
        if ((_viewModel is null) || !_opened)
        {
            return;
        }

        _viewModel.ReportPaneLayout(new PaneLayout
        {
            SidebarWidth = WidthOf(SidebarColumn, _sidebarWidth),
            EditorWeight = WidthOf(EditorColumn, _editorWidth),
            PreviewWeight = WidthOf(PreviewColumn, _previewWidth),
        });
    }

    private WindowPlacement CurrentBounds()
    {
        PixelSize size = PixelSize.FromSize(ClientSize, RenderScaling);

        return new WindowPlacement
        {
            X = Position.X,
            Y = Position.Y,
            Width = size.Width,
            Height = size.Height,
        };
    }

    private IReadOnlyList<ScreenArea> ScreenAreas()
    {
        List<ScreenArea> areas = [];

        foreach (Screen screen in Screens.All)
        {
            PixelRect working = screen.WorkingArea;
            areas.Add(new ScreenArea(working.X, working.Y, working.Width, working.Height));
        }

        return areas;
    }

    private double ScalingAt(PixelPoint point)
    {
        foreach (Screen screen in Screens.All)
        {
            if (screen.WorkingArea.Contains(point))
            {
                return screen.Scaling;
            }
        }

        return Screens.Primary?.Scaling ?? RenderScaling;
    }

    // A collapsed pane's column sits at zero, which is not the width it should come back at — the one
    // it had before it was hidden is in the field the restore reads.
    private static double WidthOf(ColumnDefinition column, GridLength whenCollapsed)
        => column.Width.Value > 0 ? column.Width.Value : whenCollapsed.Value;

    private static int ToDevicePixels(double layoutUnits, double scaling)
        => Math.Max(1, (int)Math.Round(layoutUnits * scaling));

    private void ApplyPaneWidths(MainWindowViewModel viewModel)
    {
        ApplyPaneWidth(SidebarColumn, viewModel.IsSidebarVisible, ref _sidebarWidth, _sidebarMinWidth);
        ApplyPaneWidth(EditorColumn, viewModel.IsEditorVisible, ref _editorWidth, _editorMinWidth);
        ApplyPaneWidth(PreviewColumn, viewModel.IsPreviewVisible, ref _previewWidth, _previewMinWidth);
    }

    private static void ApplyPaneWidth(ColumnDefinition column, bool visible, ref GridLength restoreTo, double minWidth)
    {
        if (visible)
        {
            // MinWidth first: setting Width while MinWidth is still 0 would let the pane appear at
            // its collapsed size for one layout pass.
            column.MinWidth = minWidth;
            column.Width = restoreTo;
            return;
        }

        if (column.Width.Value > 0)
        {
            restoreTo = column.Width;
        }

        column.MinWidth = 0;
        column.Width = Collapsed;
    }
}
