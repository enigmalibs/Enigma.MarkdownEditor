using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Enigma.MarkdownEditor.Core.Markdown;
using Markdig.Syntax;

namespace Enigma.MarkdownEditor.Desktop.Controls;

/// <summary>
/// A rendered view of a markdown document: set <see cref="Markdown"/> and it rebuilds its contents.
/// </summary>
/// <remarks>
/// <para>
/// The control renders and nothing else. It does not read files, watch documents or decide when the
/// text has settled — <see cref="Markdown"/> is pushed at it, and every assignment is a render. The
/// debouncing a live preview needs belongs to whoever is watching the editor, because only that side
/// knows what a keystroke is; here it would be a timer inside a control with no way to say so.
/// </para>
/// <para>
/// The tree is a <see cref="StackPanel"/> of block visuals in a <see cref="ScrollViewer"/>, rebuilt
/// wholesale. At a note's scale that is cheaper than reconciling, and it keeps the renderer a
/// function of its input; if a document ever grows large enough for the rebuild to show, the fallback
/// is to virtualize the block panel rather than to make the walk incremental.
/// </para>
/// <para>
/// Where the view sits is the one piece of state a render carries across: the source line at the top
/// of the pane is read off the old visuals, and the new ones are scrolled back to it. A line survives
/// an edit three paragraphs above it where a pixel offset does not — which is what stops the preview
/// jumping to the top of the note every quarter second while someone types. A different document
/// resets it, which is what <see cref="SourcePath"/> is for.
/// </para>
/// </remarks>
public class MarkdownPreview : UserControl
{
    /// <summary>
    /// Defines the <see cref="Markdown"/> property.
    /// </summary>
    public static readonly StyledProperty<string?> MarkdownProperty =
        AvaloniaProperty.Register<MarkdownPreview, string?>(nameof(Markdown));

    /// <summary>
    /// Defines the <see cref="BasePath"/> property.
    /// </summary>
    public static readonly StyledProperty<string?> BasePathProperty =
        AvaloniaProperty.Register<MarkdownPreview, string?>(nameof(BasePath));

    /// <summary>
    /// Defines the <see cref="SourcePath"/> property.
    /// </summary>
    public static readonly StyledProperty<string?> SourcePathProperty =
        AvaloniaProperty.Register<MarkdownPreview, string?>(nameof(SourcePath));

    /// <summary>
    /// Defines the <see cref="Typography"/> property.
    /// </summary>
    public static readonly StyledProperty<MarkdownPreviewTypography> TypographyProperty =
        AvaloniaProperty.Register<MarkdownPreview, MarkdownPreviewTypography>(
            nameof(Typography),
            MarkdownPreviewTypography.Default);

    private readonly StackPanel _blocks;
    private readonly ScrollViewer _scroll;

    private MarkdownLineMap _map = MarkdownLineMap.Empty;

    // The line a render still owes a scroll to, once there is a laid-out tree to measure it against.
    private int? _pendingLine;

    // Whether a layout pass has run since the last render. A block's offset within the panel is only
    // known once one has: straight after a render every child sits at zero, and the panel itself
    // still reports the height it had before the rebuild — so its own size proves nothing.
    private bool _arranged;

    // Raised while this control is moving its own view. Setting an offset lays the tree out again,
    // and laying it out is what a pending scroll is waiting for — so without this a scroll could ask
    // for another one from inside itself.
    private bool _scrolling;

    // Set when the document itself changed, so the next render starts at the top rather than at the
    // line the previous document happened to be showing.
    private bool _resetScroll = true;

    // Whether the last thing the reader did was select the whole document. It is what tells the two
    // copy cases apart: after a select-all EVERY block reports a selection, so a copy that read the
    // focused block's selection would put one paragraph on the clipboard and call it the note.
    private bool _allSelected;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarkdownPreview"/> class.
    /// </summary>
    public MarkdownPreview()
    {
        _blocks = new StackPanel { Spacing = 8 };

        _scroll = new ScrollViewer
        {
            Content = _blocks,
            Padding = new Thickness(16, 12),

            // The pane is narrow and the blocks wrap to it, so a horizontal bar would only ever mean
            // something inside failed to wrap — a code block or a wide image, both of which are
            // better clipped to the pane than allowed to widen the whole document.
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };

        Content = _scroll;

        // Never detached: the panel lives as long as this control, and this is how a scroll asked for
        // between a render and the layout pass that follows it eventually gets answered.
        _blocks.LayoutUpdated += OnBlocksLayoutUpdated;

        // So a click in the pane gives this control the focus, which is what puts the two gestures
        // below within reach. Without it the pane is a place the keyboard cannot address at all.
        Focusable = true;

        // Tunnelling, not bubbling, and for the same reason the window handles Ctrl+Tab that way: a
        // SelectableTextBlock handles both of these itself, so by the time a bubbling handler saw
        // Ctrl+A the focused paragraph would already have selected only itself.
        AddHandler(KeyDownEvent, OnPaneKeyDown, RoutingStrategies.Tunnel);

        ApplyTypography();
        MarkdownPreviewTheme.BindBrush(this, ForegroundProperty, MarkdownPreviewTheme.ForegroundKey);
    }

    /// <summary>
    /// Raised for every failure the render survived: a block that could not be built, a parse that
    /// threw, an image that could not be decoded.
    /// </summary>
    /// <remarks>
    /// An event rather than an injected logger, because this control is built by the XAML that hosts
    /// it and never by the container. The pane's ViewModel does the logging — the same seam the editor
    /// pane reports a highlighting failure through.
    /// </remarks>
    public event EventHandler<Exception>? RenderFailed;

    /// <summary>
    /// Raised when the reader clicks a link, with the URL exactly as the document wrote it.
    /// </summary>
    /// <remarks>
    /// The control decides nothing about it. Whether a link means the browser, a tab or the operating
    /// system depends on the folder the document lives in and on what this application opens — neither
    /// of which a renderer should know — so it reports the click and the pane's ViewModel acts on it.
    /// </remarks>
    public event EventHandler<string>? LinkActivated;

    /// <summary>
    /// Raised when the reader ticks or unticks a task box, with the source offset of the character
    /// between the brackets and the state they asked for.
    /// </summary>
    /// <remarks>
    /// The same seam as <see cref="LinkActivated"/>, and for the same reason: the control knows where
    /// the character was in the markdown it was handed, and nothing at all about the buffer that
    /// markdown came from — which document it belongs to, whether it has been edited since, or how a
    /// change is written to it. It reports the tick; the pane's ViewModel decides.
    /// </remarks>
    public event EventHandler<MarkdownTaskToggledEventArgs>? TaskToggled;

    /// <summary>
    /// Raised when the reader asks for a copy, with which of the two kinds it is already resolved:
    /// the text they had selected, or the whole rendered note.
    /// </summary>
    /// <remarks>
    /// The same seam as <see cref="LinkActivated"/> and <see cref="TaskToggled"/>, and for the same
    /// reason: this control can say what is selected in it and nothing at all about the clipboard,
    /// which is a service and therefore the pane's ViewModel's to reach. What it reports is that a
    /// copy was asked for; what that costs is somebody else's.
    /// </remarks>
    public event EventHandler<MarkdownCopyRequest>? CopyRequested;

    /// <summary>
    /// The markdown to show. Every assignment re-renders; <see langword="null"/> or empty clears.
    /// </summary>
    public string? Markdown
    {
        get => GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    /// <summary>
    /// The folder relative image links are resolved against — the folder the document lives in.
    /// </summary>
    public string? BasePath
    {
        get => GetValue(BasePathProperty);
        set => SetValue(BasePathProperty, value);
    }

    /// <summary>
    /// Which document the markdown came from — its full path, or <see langword="null"/> for none.
    /// </summary>
    /// <remarks>
    /// Identity, not content: it is never rendered and never triggers a render. It answers the one
    /// question the control cannot answer from <see cref="Markdown"/> alone — whether the next render
    /// is the same note being retyped, whose scroll position is worth keeping, or a different note,
    /// which has to start at the top. Two notes in one folder make <see cref="BasePath"/> unable to
    /// tell them apart, which is why this is its own property rather than derived from that one.
    /// </remarks>
    public string? SourcePath
    {
        get => GetValue(SourcePathProperty);
        set => SetValue(SourcePathProperty, value);
    }

    /// <summary>
    /// The type the document is set in: the two families, the body size, and the line height. Every
    /// assignment re-renders.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A render rather than a repaint, unlike a colour: a size decides how much room a block takes,
    /// which decides where every block after it sits, so there is nothing to change in place. That is
    /// also why this is a property of the control rather than something the palette carries — colour
    /// is bound and free, type is assigned and costs a walk.
    /// </para>
    /// <para>
    /// The families are names the machine actually has, or empty for "the font this pane inherits".
    /// Resolving "the best monospace family here" is <see cref="Services.IFontCatalog"/>'s job, and a
    /// renderer is the wrong place to probe installed fonts — so what arrives here is the answer.
    /// </para>
    /// </remarks>
    public MarkdownPreviewTypography Typography
    {
        get => GetValue(TypographyProperty);
        set => SetValue(TypographyProperty, value);
    }

    /// <summary>
    /// The block visuals currently on screen, in document order.
    /// </summary>
    /// <remarks>
    /// Public because it is what the renderer produced, and the only honest way to assert a render
    /// without a screenshot. <see cref="LineMap"/> indexes this list.
    /// </remarks>
    public IReadOnlyList<Control> Blocks => _blocks.Children;

    /// <summary>
    /// The line-to-block index of the render currently on screen, over <see cref="Blocks"/>.
    /// </summary>
    /// <remarks>
    /// Rebuilt with every render and <see cref="MarkdownLineMap.Empty"/> when there is nothing to
    /// show. It is what makes <see cref="ScrollToSourceLine"/> answerable, and it is exposed because a
    /// test asserting the sync should be able to read the index rather than infer it from offsets.
    /// </remarks>
    public MarkdownLineMap LineMap => _map;

    /// <summary>
    /// Whether there is anything in the pane to copy.
    /// </summary>
    /// <remarks>
    /// Read off the render rather than off <see cref="Markdown"/>: a document of nothing but a
    /// thematic rule has text in it and nothing a copy could carry, and a document that failed to
    /// parse has an error block the reader is not being offered.
    /// </remarks>
    public bool HasContent => _blocks.Children.Count > 0;

    /// <summary>
    /// Selects every piece of text in the pane, as <c>Ctrl+A</c> does.
    /// </summary>
    /// <remarks>
    /// Avalonia keeps a selection per text block, so a document-wide selection is every block being
    /// told to select itself — which is also why <see cref="RequestCopy"/> cannot simply read the
    /// selection back afterwards. The pane takes the focus as well, so that the <c>Ctrl+C</c> after a
    /// select-all from the context menu arrives here rather than wherever the focus happened to be.
    /// </remarks>
    public void SelectAll()
    {
        foreach (SelectableTextBlock block in SelectableBlocks())
        {
            block.SelectAll();
        }

        _allSelected = true;

        Focus();
    }

    /// <summary>
    /// Clears whatever is selected in the pane.
    /// </summary>
    public void ClearSelection()
    {
        foreach (SelectableTextBlock block in SelectableBlocks())
        {
            block.ClearSelection();
        }

        _allSelected = false;
    }

    /// <summary>
    /// Asks for a copy of whatever the reader currently has: the selection when there is one, and the
    /// whole rendered note otherwise.
    /// </summary>
    /// <remarks>
    /// The one entry point for all three surfaces — the <c>Ctrl+C</c> below, the pane's context menu
    /// and the header button — so the rule cannot come out differently on one of them. It resolves
    /// which kind of copy this is and raises <see cref="CopyRequested"/>; nothing happens when there
    /// is nothing on screen to copy.
    /// </remarks>
    public void RequestCopy()
    {
        if (!HasContent)
        {
            return;
        }

        OnCopyRequested(BuildCopyRequest());
    }

    /// <summary>
    /// Scrolls the pane to whatever was rendered from one source line.
    /// </summary>
    /// <param name="line">The source line, one-based, as the editor numbers it.</param>
    /// <remarks>
    /// <para>
    /// The editor's side of the sync, and the whole of it: the block that owns the line comes from
    /// <see cref="LineMap"/> and the position inside the block from interpolating across its height,
    /// so a long fence or a long list tracks the editor through it rather than sticking at its top.
    /// </para>
    /// <para>
    /// Silently does nothing when there is no render to scroll — no blocks, or an empty index. When
    /// there is one but it has not been laid out yet, the request is held and applied on the next
    /// layout pass rather than being answered against offsets that are all still zero.
    /// </para>
    /// </remarks>
    public void ScrollToSourceLine(int line)
    {
        if (_scrolling
            || !_map.TryFind(line, out MarkdownLinePosition position)
            || (position.Index >= _blocks.Children.Count))
        {
            return;
        }

        if (!_arranged)
        {
            _pendingLine = line;

            return;
        }

        Control visual = _blocks.Children[position.Index];

        ScrollTo(visual.Bounds.Y + (position.Fraction * visual.Bounds.Height));
    }

    /// <summary>
    /// Builds the block visuals for a parsed document.
    /// </summary>
    /// <param name="document">The document to walk.</param>
    /// <returns>The block visuals, in document order, each with its source line.</returns>
    /// <remarks>
    /// The one seam of this control. A subclass can render block kinds this version does not, and the
    /// isolation around the call is the caller's either way: whatever this throws becomes the inline
    /// error block rather than an unhandled exception. That is also how a test proves it.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    protected virtual IReadOnlyList<MarkdownBlockVisual> BuildBlocks(MarkdownDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return new MarkdownVisualBuilder(BasePath, Type, OnFailure, OnLinkActivated, OnTaskToggled)
            .Build(document);
    }

    /// <inheritdoc />
    /// <remarks>
    /// A press is the end of a document-wide selection whatever else it turns out to be — the reader
    /// is either starting a new selection or clicking something — and the blocks drop their own
    /// highlights on it without being asked. Only the flag has to be put back, so that the
    /// <c>Ctrl+C</c> after it copies what is actually selected.
    /// </remarks>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        _allSelected = false;

        base.OnPointerPressed(e);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);

        base.OnPropertyChanged(change);

        if (change.Property == SourcePathProperty)
        {
            // Not a render: the path is not content. Its effect is on where the view sits — and taken
            // now rather than left to the next render, because two documents can hold the same text,
            // and an assignment of the same markdown is not a change and so renders nothing at all.
            _resetScroll = true;
            _pendingLine = null;

            ScrollTo(0);

            return;
        }

        if (change.Property == TypographyProperty)
        {
            // The pane's own font first: prose inherits it, so the blocks about to be built have to
            // find it already set rather than be rebuilt again once it changes.
            ApplyTypography();
            Render();

            return;
        }

        if ((change.Property == MarkdownProperty) || (change.Property == BasePathProperty))
        {
            Render();
        }
    }

    // Never null, whatever was assigned: the record is what every size in a render is derived from,
    // and a null one would be an unhandled exception on the UI thread rather than a wrong-looking pane.
    private MarkdownPreviewTypography Type => Typography ?? MarkdownPreviewTypography.Default;

    private void ApplyTypography()
    {
        MarkdownPreviewTypography typography = Type;

        // Set on the control, not on each block: prose carries no family of its own, so this is what
        // it reads — and it is the one assignment that reaches a visual the builder does not make.
        // An empty family means "inherit", which is what leaving the property unset already does.
        if (typography.FontFamily is { Length: > 0 } family)
        {
            FontFamily = new FontFamily(family);
        }
        else
        {
            ClearValue(FontFamilyProperty);
        }

        FontSize = typography.BodyFontSize;
    }

    private void Render()
    {
        // Read before the tree is thrown away: after the Clear below there is nothing left to
        // measure the current position against.
        int? restoreTo = _resetScroll ? null : TopSourceLine();

        _resetScroll = false;
        _pendingLine = null;
        _arranged = false;

        // The blocks that held the selection are about to be thrown away, so there is nothing left
        // selected. A live selection does not survive a render, by design: the pane is rebuilt a
        // quarter of a second after the last keystroke, and holding that back to keep a highlight
        // would quietly stop the preview being live.
        _allSelected = false;

        _blocks.Children.Clear();
        _map = MarkdownLineMap.Empty;

        string? markdown = Markdown;

        if (string.IsNullOrEmpty(markdown))
        {
            ScrollTo(0);

            return;
        }

        try
        {
            MarkdownDocument document = MarkdownPipelineFactory.Parse(markdown);
            List<int> startLines = [];

            foreach (MarkdownBlockVisual block in BuildBlocks(document))
            {
                _blocks.Children.Add(block.Visual);
                startLines.Add(block.StartLine);
            }

            BuildLineMap(startLines, markdown);
        }
        catch (Exception failure)
        {
            // The builder isolates each block, so reaching here means the parse itself, or the walk
            // over the top-level blocks, failed — there is no partial document to keep. Caught
            // broadly because the alternative is an unhandled exception on the UI thread from text
            // the user merely typed.
            OnFailure(failure);

            _blocks.Children.Clear();
            _blocks.Children.Add(MarkdownVisualBuilder.ErrorBlock(failure, Type));

            return;
        }

        if (restoreTo is { } line)
        {
            // Held rather than applied: the blocks are in the tree but have not been arranged, so
            // there is nothing to measure the line against until the next layout pass.
            _pendingLine = line;
        }
        else
        {
            ScrollTo(0);
        }
    }

    private void BuildLineMap(List<int> startLines, string markdown)
    {
        try
        {
            _map = MarkdownLineMap.For(startLines, markdown.AsSpan().Count('\n') + 1);
        }
        catch (ArgumentException failure)
        {
            // Isolated from the render for the same reason each block is: the index only drives the
            // scroll sync, and a document the reader can read without it beats an error block where
            // the note should be. Reported rather than swallowed — with blocks walked in document
            // order this cannot happen, so if it ever does it is a bug worth a log entry.
            OnFailure(failure);

            _map = MarkdownLineMap.Empty;
        }
    }

    // Which source line is at the top of the pane right now, or null when there is nothing to say:
    // no index, nothing rendered, or the view sitting at the very top, where the answer would only
    // pin the next render to the first line it was going to start at anyway.
    private int? TopSourceLine()
    {
        double offset = _scroll.Offset.Y;

        if (_map.IsEmpty || (_blocks.Children.Count == 0) || (offset <= 0))
        {
            return null;
        }

        // A linear walk, unlike the line lookup: this runs once per render rather than once per
        // scrolled pixel, and the blocks' offsets are only in ascending order because a StackPanel
        // stacks them — which is a property of this control, not of the index.
        int index = 0;

        for (int candidate = 0; candidate < _blocks.Children.Count; candidate++)
        {
            if (_blocks.Children[candidate].Bounds.Y > offset)
            {
                break;
            }

            index = candidate;
        }

        Control visual = _blocks.Children[index];
        double fraction = (visual.Bounds.Height > 0)
            ? Math.Clamp((offset - visual.Bounds.Y) / visual.Bounds.Height, 0, 1)
            : 0;

        return _map.LineAt(new MarkdownLinePosition(Math.Min(index, _map.Count - 1), fraction));
    }

    private void OnBlocksLayoutUpdated(object? sender, EventArgs e)
    {
        _arranged = true;

        if (_pendingLine is { } line)
        {
            _pendingLine = null;
            ScrollToSourceLine(line);
        }
    }

    private void ScrollTo(double offset)
    {
        double furthest = Math.Max(0, _scroll.Extent.Height - _scroll.Viewport.Height);

        _scrolling = true;

        try
        {
            _scroll.Offset = new Vector(_scroll.Offset.X, Math.Clamp(offset, 0, furthest));
        }
        finally
        {
            _scrolling = false;
        }
    }

    private void OnFailure(Exception failure)
    {
        try
        {
            RenderFailed?.Invoke(this, failure);
        }
        catch (Exception reporting)
        {
            // A handler that throws while being told about a failure would replace the error block
            // with an unhandled exception — the exact outcome this whole path exists to prevent.
            System.Diagnostics.Trace.TraceError($"Reporting a preview failure threw: {reporting}");
        }
    }

    // Every text visual in the pane, at any depth: a code block keeps its text inside a scroll
    // viewer, a table cell inside a border inside a grid, and a list item's marker beside its
    // content. The logical tree is what the render tests read the same visuals back out of.
    private IEnumerable<SelectableTextBlock> SelectableBlocks()
        => _blocks.GetLogicalDescendants().OfType<SelectableTextBlock>();

    private MarkdownCopyRequest BuildCopyRequest()
    {
        if (_allSelected)
        {
            return MarkdownCopyRequest.WholeDocument;
        }

        foreach (SelectableTextBlock block in SelectableBlocks())
        {
            // The first block holding one, because without a select-all only one can: a drag is
            // confined to the block it started in, and a new one clears the last. Read off the two
            // offsets rather than through the block's own HasSelection, which is not public — the
            // same test MarkdownLinkRegions uses to tell a drag from a click.
            if ((block.SelectionStart != block.SelectionEnd)
                && (block.SelectedText is { Length: > 0 } selected))
            {
                // A code chip's first and last character are padding this renderer put there, not
                // something the note said, and this is an editor whose readers paste commands into
                // shells (see MarkdownCodePads). Stripped by index off the block's own record, so an
                // authored &nbsp; in the same sentence survives the copy untouched.
                string text = MarkdownCodePads.Strip(
                    block,
                    Math.Min(block.SelectionStart, block.SelectionEnd),
                    selected);

                // A selection of nothing but padding is a selection of nothing, and what a copy with
                // nothing selected means here is the note.
                if (text.Length > 0)
                {
                    return MarkdownCopyRequest.ForSelection(text);
                }
            }
        }

        // Nothing selected: a copy still means something, and what it means is the note.
        return MarkdownCopyRequest.WholeDocument;
    }

    private void OnPaneKeyDown(object? sender, KeyEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            return;
        }

        switch (e.Key)
        {
            case Key.A:
                SelectAll();
                e.Handled = true;
                break;

            // Only once the whole document is selected. Left alone otherwise, so a selection inside
            // one paragraph is still copied by the block's own handler — the path that already works
            // today, and the one this must not take over.
            case Key.C when _allSelected:
                RequestCopy();
                e.Handled = true;
                break;

            default:
                break;
        }
    }

    private void OnCopyRequested(MarkdownCopyRequest request)
    {
        try
        {
            CopyRequested?.Invoke(this, request);
        }
        catch (Exception failure)
        {
            // Raised from a key or pointer handler, so an exception out of here is an unhandled one
            // on the UI thread. A copy that could not be made is worth a log entry, not the window.
            OnFailure(failure);
        }
    }

    private void OnLinkActivated(string url)
    {
        try
        {
            LinkActivated?.Invoke(this, url);
        }
        catch (Exception failure)
        {
            // Raised from a pointer handler, so an exception out of here is an unhandled one on the UI
            // thread. A link that cannot be followed is worth a log entry, not the window.
            OnFailure(failure);
        }
    }

    private bool OnTaskToggled(int sourceOffset, bool isChecked)
    {
        try
        {
            MarkdownTaskToggledEventArgs toggle = new(sourceOffset, isChecked);
            TaskToggled?.Invoke(this, toggle);

            return toggle.Applied;
        }
        catch (Exception failure)
        {
            // Same reason as above, and one more: a handler that threw halfway may or may not have
            // written the character, so the box is put back and the next render — 250ms behind at
            // worst — shows whatever the document actually holds.
            OnFailure(failure);

            return false;
        }
    }
}
