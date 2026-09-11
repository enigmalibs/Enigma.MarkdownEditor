using System;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using Enigma.MarkdownEditor.Desktop.ViewModels;

namespace Enigma.MarkdownEditor.Desktop.Views;

/// <summary>
/// The editor pane: the tab strip, the external-change bar, and the one text editor they all share.
/// </summary>
/// <remarks>
/// <para>
/// Everything here is the part of a tabbed editor that a binding cannot express. One control serves
/// every tab, so the caret and the scroll position have to be carried on the ViewModels and put back
/// when a document comes round again; they are recorded as they move rather than read off at the
/// moment of the switch, because by then the control is already showing the other document.
/// </para>
/// <para>
/// The rest is input the theme does not give for free: the middle click that closes a tab, and the
/// context menu's four editing commands, which are methods on the control rather than commands
/// anything can bind to.
/// </para>
/// <para>
/// Syntax highlighting hangs off the same control and so is owned here too, in
/// <see cref="EditorAppearance"/>: it is built when the pane goes on screen, told about every theme
/// switch, and torn down when the pane leaves, because it runs a background tokenizer that should
/// not outlive what it is colouring.
/// </para>
/// <para>
/// The markdown the editor writes for you — continued lists, indented items, the formatting
/// shortcuts, a pasted image — is <see cref="MarkdownAssists"/>, which takes the keystrokes it wants
/// off this control and leaves the rest to AvaloniaEdit.
/// </para>
/// <para>
/// Vim emulation is <see cref="VimEditorBehavior"/>, built here for the same reason and before the
/// assists: in normal mode the keys they both want are commands rather than typing, so the engine has
/// to see them first. It follows the setting through the session on the pane's ViewModel, which is
/// what makes the toggle take effect on the editor that is already open.
/// </para>
/// </remarks>
public partial class EditorPaneView : UserControl
{
    // What one wheel notch moves the tab strip sideways, in pixels. The number Avalonia's own
    // ScrollViewer uses for a scroll it cannot ask the content to size, so the strip moves at the
    // speed every other list in the window does.
    private const double TabWheelStep = 50;

    /// <summary>
    /// How much clear space the line-number column gets on each side, in pixels.
    /// </summary>
    /// <remarks>
    /// Without it the numbers sit flush against the pane's left edge on one side and against the
    /// document's first character on the other, which reads as one run of digits and text rather than
    /// as a margin beside a document.
    /// </remarks>
    public const double LineNumberSpacing = 4;

    private readonly VimEditorBehavior _vim;
    private readonly MarkdownAssists _assists;

    private DocumentManagerViewModel? _manager;
    private DocumentViewModel? _current;
    private EditorAppearance? _appearance;

    // The last line the sync was told about, so the several scroll events one gesture raises do not
    // become several notifications. Zero is "nothing reported yet", which no line number can be.
    private int _reportedTopLine;

    /// <summary>
    /// Initializes a new instance of the <see cref="EditorPaneView"/> class.
    /// </summary>
    public EditorPaneView()
    {
        InitializeComponent();

        // Switched on here; what it is painted with is EditorAppearance's business, along with every
        // other brush the editor draws with.
        Editor.Options.HighlightCurrentLine = true;

        // Set on the instance rather than through a style, and it has to be: ShowLineNumbers builds
        // this margin in code and puts it in TextArea.LeftMargins, so the value that lands on it is
        // whichever one was assigned last rather than whichever selector is more specific. Assigning
        // it directly is the one way that cannot lose that race.
        foreach (LineNumberMargin margin in Editor.TextArea.LeftMargins.OfType<LineNumberMargin>())
        {
            margin.Margin = new Thickness(LineNumberSpacing, 0);
        }

        Editor.DocumentChanged += OnEditorDocumentChanged;
        Editor.TextArea.Caret.PositionChanged += OnCaretPositionChanged;
        Editor.TextArea.TextView.ScrollOffsetChanged += OnScrollOffsetChanged;

        // Here rather than in OnLoaded, unlike the appearance: neither of these needs the theme's
        // resources or a resolved variant, only the control they take keystrokes from, and the
        // handlers they install live exactly as long as that control does.
        //
        // Vim first, on purpose. Both tunnel from this control, so they see a keystroke in the order
        // they were attached, and in normal mode Enter and Tab are commands rather than a continued
        // list. The assists refuse those keys themselves while vim has the editor, so the order is a
        // second lock on the same door rather than the only one.
        // Not awaited: an ex command ends in a save or a dialog, and this is a keystroke handler with
        // nowhere to return a task to. Failures inside reach the Dispatcher's unhandled-exception
        // handler, and everything the user needs to know about a save that went wrong is reported by
        // the ViewModel itself.
        _vim = new VimEditorBehavior(
            Editor,
            () => _manager?.Vim,
            command => _ = _manager?.ExecuteExCommandAsync(command));

        _assists = new MarkdownAssists(Editor, () => _manager);
    }

    /// <summary>
    /// Raised when a different source line reaches the top of the editor's view, with that line's
    /// number, one-based.
    /// </summary>
    /// <remarks>
    /// The editor's half of the scroll sync, and all of it: what the preview does with the line is
    /// the window's business to wire up. Only raised when the line actually changes, so the several
    /// scroll events a single wheel notch produces cost one notification rather than twenty.
    /// </remarks>
    public event EventHandler<int>? TopVisibleLineChanged;

    /// <summary>
    /// Gets the source line at the top of the editor's view, one-based. <c>1</c> when there is no
    /// document, or when the view has not been laid out yet.
    /// </summary>
    public int TopVisibleLine
    {
        get
        {
            if (Editor.Document is null)
            {
                return 1;
            }

            TextView view = Editor.TextArea.TextView;

            // The built lines when they are there, because they are what is literally on screen and
            // account for wrapping. The height tree otherwise: it is maintained whether or not the
            // lines have been built, which is the case for a pane that is collapsed or not yet shown.
            if (view.VisualLinesValid && (view.VisualLines.Count > 0))
            {
                return view.VisualLines[0].FirstDocumentLine.LineNumber;
            }

            return view.GetDocumentLineByVisualTop(Math.Max(0, view.VerticalOffset))?.LineNumber ?? 1;
        }
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        Detach();

        _manager = DataContext as DocumentManagerViewModel;

        if (_manager is not null)
        {
            _manager.DocumentReloaded += OnDocumentReloaded;
            _manager.SelectionRequested += OnSelectionRequested;
            _manager.PropertyChanged += OnManagerPropertyChanged;
        }

        // The session is only reachable now that there is a ViewModel to read it from.
        _vim.Refresh();

        base.OnDataContextChanged(e);
    }

    /// <inheritdoc />
    protected override void OnLoaded(RoutedEventArgs e)
    {
        // Not the constructor: finding a brush means walking up to the application's resources, and
        // ActualThemeVariant is only resolved to light or dark, rather than left at Default, once the
        // control is in a tree. Both are true by the time this runs.
        _appearance ??= new EditorAppearance(
            Editor,
            ActualThemeVariant,
            failure => _manager?.OnHighlightingFailed(failure));

        ActualThemeVariantChanged += OnActualThemeVariantChanged;

        // Idempotent, and here because a pane that goes away and comes back has to pick the setting
        // up again; OnDataContextChanged has usually already done it by now.
        _vim.Refresh();

        base.OnLoaded(e);
    }

    /// <inheritdoc />
    protected override void OnUnloaded(RoutedEventArgs e)
    {
        ActualThemeVariantChanged -= OnActualThemeVariantChanged;

        // Disposed rather than kept: it holds a background tokenizer and a line transformer on the
        // text view. OnLoaded builds a new one if the pane ever comes back.
        _appearance?.Dispose();
        _appearance = null;

        // The ViewModel is a container singleton and outlives this control, so leaving the handlers
        // attached would keep the whole pane alive with it.
        Detach();

        base.OnUnloaded(e);
    }

    // A middle click anywhere on a tab closes it — the convention every browser and editor shares,
    // and one no template can express. PointerReleased rather than PointerPressed so a click that
    // began somewhere else and drifted over a tab does not close it.
    private void OnTabsPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Middle)
        {
            return;
        }

        if ((e.Source as Visual)?.FindAncestorOfType<ListBoxItem>()?.DataContext is not DocumentViewModel document)
        {
            return;
        }

        e.Handled = true;
        _manager?.CloseCommand.Execute(document);
    }

    // The strip has a scrollbar of its own, but the wheel has to be translated by hand all the same:
    // a mouse reports its notches as a vertical delta, and the ScrollViewer will only ever spend
    // that on a vertical offset, which this strip does not have.
    private void OnTabsPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (Tabs.Scroll is not { } scroll)
        {
            return;
        }

        double overflow = scroll.Extent.Width - scroll.Viewport.Width;

        // Nothing to scroll, and nothing to swallow either: the wheel belongs to whatever is under
        // the strip when every tab already fits.
        if (overflow <= 0)
        {
            return;
        }

        // Away from the user goes towards the first tab, which is the direction that same gesture
        // moves the content of a vertical list.
        double offset = Math.Clamp(scroll.Offset.X - (e.Delta.Y * TabWheelStep), 0, overflow);

        if (offset == scroll.Offset.X)
        {
            return;
        }

        scroll.Offset = new Vector(offset, scroll.Offset.Y);
        e.Handled = true;
    }

    // The whole window re-colours in one step on a variant switch, and the editor has to come with
    // it: its token colours and its chrome are both assigned rather than bound, so nothing else moves
    // them. ActualThemeVariant rather than the setting, so following the OS is covered too.
    private void OnActualThemeVariantChanged(object? sender, EventArgs e)
        => _appearance?.Apply(ActualThemeVariant);

    private void Detach()
    {
        if (_manager is null)
        {
            return;
        }

        _manager.DocumentReloaded -= OnDocumentReloaded;
        _manager.SelectionRequested -= OnSelectionRequested;
        _manager.PropertyChanged -= OnManagerPropertyChanged;
    }

    // The only property this control watches: vim being switched on or off has to reach the engine's
    // key handlers, and the ViewModel is where the session announces it.
    private void OnManagerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DocumentManagerViewModel.IsVimEnabled))
        {
            _vim.Refresh();
        }
    }

    private void OnEditorDocumentChanged(object? sender, EventArgs e)
    {
        // Resolved by matching the control's document rather than by reading ActiveDocument: the
        // binding and the property-changed notification do not have a guaranteed order between them,
        // and this answer cannot be wrong.
        _current = _manager?.Documents.FirstOrDefault(document => ReferenceEquals(document.Document, Editor.Document));

        // The text under the engine has been replaced wholesale, so it starts again on this one — in
        // normal mode, with a caret normal mode allows.
        _vim.OnDocumentChanged();

        // The same line number in another document is another place, so the filter has to forget what
        // it reported: otherwise a tab switch that happens to land on the same line tells the preview
        // nothing, and it keeps showing the previous note's position.
        _reportedTopLine = 0;

        RestoreViewState();
    }

    private void OnDocumentReloaded(object? sender, DocumentViewModel document)
    {
        // The buffer was replaced wholesale, which puts the caret at the top. The document kept the
        // offset it had, clamped to the new length; this is what puts the view back on it.
        if (ReferenceEquals(document, _current))
        {
            RestoreViewState();
        }
    }

    // A caret placement asked for on the document that is already on screen. Nothing swapped, so
    // OnEditorDocumentChanged will not fire and this is the only thing that would look at the request.
    private void OnSelectionRequested(object? sender, DocumentViewModel document)
    {
        // A document that is not the one showing is about to become it, and the swap's own restore
        // carries the request across. Taking it here would only race that.
        if (!ReferenceEquals(document, _current))
        {
            return;
        }

        Dispatcher.UIThread.Post(
            () =>
            {
                if (ReferenceEquals(document, _current) && ApplyPendingSelection(document))
                {
                    ReportTopVisibleLine();
                    Editor.TextArea.Focus();
                }
            },
            DispatcherPriority.Background);
    }

    private void RestoreViewState()
    {
        if (_current is not { } document)
        {
            return;
        }

        // A document opened at a particular place — a search hit — is not put back where it was last
        // left: the request is the position, and it replaces the caret and the scroll both.
        if (document.PendingSelection is null)
        {
            Editor.CaretOffset = Math.Min(document.CaretOffset, Editor.Document?.TextLength ?? 0);
        }

        // Both of these have to wait, for different reasons. Scrolling needs the text view to have
        // measured the new document — setting it now would scroll a view that still believes it is
        // empty. The focus has to come after everything else because opening a file starts as a
        // double-click in the tree, and the tree takes the focus for the row that was clicked;
        // asking for it any earlier simply loses it, and leaves the user typing into a file list.
        // Background is the priority that reliably runs after both, which is why it is not Loaded.
        Dispatcher.UIThread.Post(
            () =>
            {
                if (!ReferenceEquals(document, _current))
                {
                    return;
                }

                if (!ApplyPendingSelection(document))
                {
                    Editor.ScrollToVerticalOffset(document.VerticalScrollOffset);
                    Editor.ScrollToHorizontalOffset(document.HorizontalScrollOffset);
                }

                // Explicitly, because the scroll above may not have moved anything: two documents
                // both left at the top raise no scroll event between them, and the preview still has
                // to be told which line the new one starts at.
                ReportTopVisibleLine();

                // The TextArea, not the TextEditor around it: the inner control is the one that
                // takes keystrokes, and focusing the outer one does not always reach it.
                Editor.TextArea.Focus();
            },
            DispatcherPriority.Background);
    }

    // Taken rather than read, so whichever of the two entry points gets here first honours the
    // request and the other finds nothing to do.
    private bool ApplyPendingSelection(DocumentViewModel document)
    {
        if (Editor.Document is not { } text)
        {
            return false;
        }

        if (document.TakePendingSelection() is not { } selection)
        {
            return false;
        }

        DocumentSelectionRange range = selection.Resolve(text);

        Editor.Select(range.Offset, range.Length);
        CentreOn(range.Offset);

        return true;
    }

    // Centred rather than merely brought into view: a hit that lands on the last visible line is a hit
    // with no context under it, and reading what a match is part of is the whole reason for opening it.
    private void CentreOn(int offset)
    {
        if (Editor.Document is not { } text)
        {
            return;
        }

        TextLocation location = text.GetLocation(offset);

        // Visible first, because the height tree can only answer for a line the view has built, and
        // this is what guarantees one. The centring below then moves it the rest of the way.
        Editor.ScrollTo(location.Line, location.Column);

        TextView view = Editor.TextArea.TextView;
        double top = view.GetVisualTopByDocumentLine(location.Line);

        Editor.ScrollToVerticalOffset(Math.Max(0, top - ((Editor.ViewportHeight - view.DefaultLineHeight) / 2)));
    }

    private void OnCaretPositionChanged(object? sender, EventArgs e)
    {
        if (!ShowsCurrentDocument)
        {
            return;
        }

        _current!.CaretLine = Editor.TextArea.Caret.Line;
        _current.CaretColumn = Editor.TextArea.Caret.Column;
        _current.CaretOffset = Editor.CaretOffset;
    }

    private void OnScrollOffsetChanged(object? sender, EventArgs e)
    {
        if (!ShowsCurrentDocument)
        {
            return;
        }

        _current!.VerticalScrollOffset = Editor.VerticalOffset;
        _current.HorizontalScrollOffset = Editor.HorizontalOffset;

        ReportTopVisibleLine();
    }

    // Filtered on the line rather than on the offset: a wheel notch is several scroll events over a
    // few pixels, and the preview only has somewhere new to go when a different line has reached the
    // top. The field also carries across a document swap, where the same line number in another file
    // is a different place and has to be reported again — hence the reset in OnEditorDocumentChanged.
    private void ReportTopVisibleLine()
    {
        int line = TopVisibleLine;

        if (line == _reportedTopLine)
        {
            return;
        }

        _reportedTopLine = line;
        TopVisibleLineChanged?.Invoke(this, line);
    }

    // Both handlers fire during a document swap as well, while the control is showing the incoming
    // text but _current has not caught up. Recording those values would overwrite the outgoing
    // document's position with a caret that has just been reset to the top of another file.
    private bool ShowsCurrentDocument => (_current is not null) && ReferenceEquals(_current.Document, Editor.Document);

    private void OnCutClick(object? sender, RoutedEventArgs e) => Editor.Cut();

    private void OnCopyClick(object? sender, RoutedEventArgs e) => Editor.Copy();

    // Through the assists rather than straight to the editor, so an image on the clipboard becomes a
    // file and a link here too and not only under Ctrl+V. Not awaited: the menu handler is void, and
    // failures inside reach the Dispatcher's unhandled-exception handler.
    private void OnPasteClick(object? sender, RoutedEventArgs e) => _ = _assists.PasteAsync();

    private void OnSelectAllClick(object? sender, RoutedEventArgs e) => Editor.SelectAll();

    private void OnBoldClick(object? sender, RoutedEventArgs e) => _assists.Bold();

    private void OnItalicClick(object? sender, RoutedEventArgs e) => _assists.Italic();

    private void OnLinkClick(object? sender, RoutedEventArgs e) => _assists.Link();
}
