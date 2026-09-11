using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Editing;
using Enigma.MarkdownEditor.Core.Settings;
using Enigma.MarkdownEditor.Core.Vim;
using Enigma.MarkdownEditor.Core.Workspaces;
using Enigma.MarkdownEditor.Desktop.Services;
using Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;
using Enigma.MarkdownEditor.Desktop.Views;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Views;

/// <summary>
/// A real editor pane, over a throw-away folder of real files, in a window narrow enough that a
/// handful of tabs does not fit — and the few readings a test takes off its tab strip.
/// </summary>
/// <remarks>
/// <para>
/// The whole pane rather than a stand-in strip: the styles under test are declared on
/// <c>EditorPaneView</c> itself and are the thing being asserted, so a copy of the tab template
/// inside a test would only assert a copy of the styles.
/// </para>
/// <para>
/// A shown window, and not incidentally: <c>:pointerover</c> is set by real input against a real top
/// level, a dynamic resource resolves through the tree the visual is in, and a control that was never
/// laid out has no bounds to compare. Every mutating helper ends in a layout pass for the same
/// reason — styles are applied as part of one, so nothing here can be read before one has run.
/// </para>
/// </remarks>
internal sealed class TabStrip : IDisposable
{
    /// <summary>
    /// More tabs than the harness's window has room for, so the strip overflows.
    /// </summary>
    public static readonly string[] Many =
    [
        "alpha.md", "bravo.md", "charlie.md", "delta.md",
        "echo.md", "foxtrot.md", "golf.md", "hotel.md",
    ];

    private const double WindowWidth = 320;
    private const double WindowHeight = 300;

    private readonly EditorHarness _harness;
    private readonly Window _window;
    private readonly EditorPaneView _pane;

    private TabStrip(EditorHarness harness, Window window, EditorPaneView pane)
    {
        _harness = harness;
        _window = window;
        _pane = pane;
    }

    /// <summary>
    /// The tab strip itself.
    /// </summary>
    public ListBox Tabs => _pane.GetVisualDescendants().OfType<ListBox>().First();

    /// <summary>
    /// The pane's one text editor, shared by every tab.
    /// </summary>
    public TextEditor Editor => _pane.GetVisualDescendants().OfType<TextEditor>().First();

    /// <summary>
    /// The editor's line-number column.
    /// </summary>
    /// <remarks>
    /// Built in code by <c>ShowLineNumbers</c> and put into <c>TextArea.LeftMargins</c>, so it is
    /// reached through the collection it lives in rather than by name.
    /// </remarks>
    public LineNumberMargin LineNumbers => Editor.TextArea.LeftMargins.OfType<LineNumberMargin>().Single();

    /// <summary>
    /// The border the tabs are drawn in — shown or hidden with the tabs themselves, and not with the
    /// editor.
    /// </summary>
    public Border Strip => Tabs.GetVisualAncestors().OfType<Border>().First();

    /// <summary>
    /// The empty state shown in the editor's place when there is nothing to edit.
    /// </summary>
    public TextBlock Placeholder
        => _pane.GetVisualDescendants().OfType<TextBlock>().First(block => block.Classes.Contains("placeholder"));

    /// <summary>
    /// The title of the tab the editor is showing, or <see langword="null"/> when it is showing none.
    /// </summary>
    public string? ActiveTitle => _harness.Documents.ActiveDocument?.Title;

    /// <summary>
    /// The mode the vim engine is in, as the status bar would report it.
    /// </summary>
    /// <remarks>
    /// Read off the session rather than off the behaviour: the engine belongs to the pane and is not
    /// public, and the session is what the rest of the window is told.
    /// </remarks>
    public VimMode Mode => _harness.Vim.Mode;

    /// <summary>
    /// The questions the editor asks about unsaved work, and the answers a test decides in advance.
    /// </summary>
    public FakeDocumentPrompts Prompts => _harness.Prompts;

    /// <summary>
    /// How many tabs are open.
    /// </summary>
    public int Count => _harness.Documents.Documents.Count;

    /// <summary>
    /// The settings the pane draws itself from, to be changed the way the settings window changes
    /// them — followed by <see cref="ApplySettings"/>, which is what closing that window amounts to.
    /// </summary>
    public AppSettings Settings => _harness.Settings.Value;

    /// <summary>
    /// The fonts the imaginary machine this pane runs on has (see <c>TestFonts</c>).
    /// </summary>
    public IFontCatalog Fonts => _harness.Fonts;

    /// <summary>
    /// The strip's horizontal scrollbar — drawn only while there are more tabs than room, and always
    /// in the band the strip reserves under them.
    /// </summary>
    public ScrollBar HorizontalScrollBar
        => Tabs.GetVisualDescendants().OfType<ScrollBar>().First(bar => bar.Orientation == Orientation.Horizontal);

    /// <summary>
    /// The patch of the strip the scrollbar occupies, in the strip's own coordinates.
    /// </summary>
    /// <remarks>
    /// The strip rather than the bar's own parent, because the one thing worth asserting about it is
    /// where it sits relative to the tabs — and <see cref="TabBand"/> is measured against the same
    /// origin so the two rectangles can simply be intersected.
    /// </remarks>
    public Rect ScrollBarBand => BandOf(HorizontalScrollBar);

    /// <summary>
    /// How tall the whole strip is, band included.
    /// </summary>
    public double Height => Strip.Bounds.Height;

    /// <summary>
    /// Whether there are more tabs than the strip has room for.
    /// </summary>
    public bool Overflows => Scroll.Extent.Width > Scroll.Viewport.Width;

    /// <summary>
    /// How far the strip is scrolled sideways.
    /// </summary>
    public double Offset => Scroll.Offset.X;

    private IScrollable Scroll
        => Tabs.Scroll ?? throw new InvalidOperationException("The tab strip has no scroll information.");

    private ScrollViewer Viewer => Tabs.GetVisualDescendants().OfType<ScrollViewer>().First();

    /// <summary>
    /// Whether the last key pressed was taken before it reached AvaloniaEdit's own handler — by vim,
    /// or by the markdown assists — or <see langword="null"/> before any key has been pressed.
    /// </summary>
    /// <remarks>
    /// Read by a tunnelling handler on the text area, one level below the editor the pane's own two
    /// handlers tunnel from, so it runs after both and sees the key exactly as AvaloniaEdit would:
    /// what vim and the assists left of it.
    /// </remarks>
    public bool? LastKeyHandled { get; private set; }

    /// <summary>
    /// Sets whether the markdown assists take their keys, standing in for what the real policy does:
    /// active in insert mode and with vim off, inactive in the command modes, where the keys the
    /// assists want are vim's.
    /// </summary>
    /// <remarks>
    /// The harness's policy is a switch a test sets, not the real one, so a case about a key both
    /// sides want — <c>Ctrl+B</c> — has to set it the way the mode would.
    /// </remarks>
    public bool AssistsActive
    {
        set => _harness.Assists.IsActive = value;
    }

    /// <summary>
    /// Opens a pane on one file per name and shows it.
    /// </summary>
    public static Task<TabStrip> ShowAsync(params string[] names)
        => ShowAsync(names.Select(name => (name, "# " + name + "\n")));

    /// <summary>
    /// Opens a pane on one file with exactly <paramref name="content"/> in it and shows it — for a
    /// case that needs more lines than the window has room for.
    /// </summary>
    public static Task<TabStrip> ShowFileAsync(string name, string content) => ShowAsync([(name, content)]);

    private static async Task<TabStrip> ShowAsync(IEnumerable<(string Name, string Content)> files)
    {
        EditorHarness harness = new();
        EditorPaneView pane = new() { DataContext = harness.Documents };
        Window window = new() { Width = WindowWidth, Height = WindowHeight, Content = pane };

        // Before the documents: opening one posts the caret restore for after a layout pass, and a
        // window that was never shown never runs one.
        window.Show();

        foreach ((string name, string content) in files)
        {
            await harness.Documents.OpenAsync(harness.Write(name, content));
        }

        TabStrip strip = new(harness, window, pane);
        strip.Settle();

        // On the text area rather than the editor: a tunnelling event visits the editor's handlers
        // before the text area's, so this one runs after the pane's two and reads what they decided.
        strip.Editor.TextArea.AddHandler(
            InputElement.KeyDownEvent,
            (_, e) => strip.LastKeyHandled = e.Handled,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);

        return strip;
    }

    /// <summary>
    /// The brush behind <paramref name="key"/> in <paramref name="variant"/>, as a string, or
    /// <see langword="null"/> when the key resolves to no brush at all.
    /// </summary>
    public static string? Brush(string key, ThemeVariant variant)
        => Application.Current!.TryFindResource(key, variant, out object? value) && (value is IBrush brush)
            ? brush.ToString()
            : null;

    /// <summary>
    /// Republishes the appearance settings over the shown pane, which is what closing the settings
    /// window does.
    /// </summary>
    public void ApplySettings()
    {
        _harness.Documents.OnSettingsChanged();
        Settle();
    }

    /// <summary>
    /// Types into the document behind a tab, which is what makes it modified.
    /// </summary>
    public void Edit(int index)
    {
        _harness.Documents.Documents[index].Document.Insert(0, "edited ");
        Settle();
    }

    /// <summary>
    /// Puts the keyboard focus where a click in the editor would put it.
    /// </summary>
    /// <returns>Whether the text area took it.</returns>
    public bool FocusEditor()
    {
        bool focused = Editor.TextArea.Focus();
        Settle();

        return focused;
    }

    /// <summary>
    /// Types <paramref name="text"/> at the keyboard, character for character.
    /// </summary>
    /// <remarks>
    /// Through the platform's text input, which is where vim reads its commands from and where
    /// AvaloniaEdit takes the characters it inserts — so what this drives is the whole of the real
    /// path, engine included, and not <see cref="Edit"/>'s write straight into the document.
    /// </remarks>
    public void Type(string text)
    {
        // One event per character, the way a keyboard delivers them: a single event carrying several
        // is an input method composing a word, which is a different path through the engine.
        foreach (char character in text)
        {
            _window.KeyTextInput(character.ToString());
        }

        Settle();
    }

    /// <summary>
    /// Presses and releases a key that types nothing — Home, End, Tab, an arrow — at the keyboard.
    /// </summary>
    /// <remarks>
    /// Through the key event rather than the text input, because that is the event such a key
    /// arrives on for real: it is where the vim behaviour translates it, and where AvaloniaEdit's own
    /// handler would otherwise act on it.
    /// </remarks>
    public void Press(PhysicalKey key) => Press(key, RawInputModifiers.None);

    /// <summary>
    /// Presses and releases a key with modifiers held — <c>Ctrl+D</c>, <c>Ctrl+B</c> — at the keyboard.
    /// </summary>
    public void Press(PhysicalKey key, RawInputModifiers modifiers)
    {
        _window.KeyPressQwerty(key, modifiers);
        _window.KeyReleaseQwerty(key, modifiers);
        Settle();
    }

    /// <summary>
    /// Saves the active tab through the very command the window's <c>Ctrl+S</c> binding names - this
    /// harness's window is not the one that binding is declared on.
    /// </summary>
    public async Task SaveAsync()
    {
        await _harness.Documents.SaveCommand.ExecuteAsync(null);
        Settle();
    }

    /// <summary>
    /// Makes a tab the active one, the way clicking it would.
    /// </summary>
    public void Select(int index)
    {
        _harness.Documents.ActiveDocument = _harness.Documents.Documents[index];
        Settle();
    }

    /// <summary>
    /// Closes a tab by clicking its close button, which is how a user closes one — and, unlike
    /// calling the command, goes through the selection the strip writes back when the tab it had
    /// selected leaves the collection.
    /// </summary>
    public async Task CloseAsync(int index)
    {
        Click(Close(index));

        // The button starts the command and does not wait for it; a clean tab never awaits anything,
        // but one with an answer to collect from the prompts does.
        if (_harness.Documents.CloseCommand.ExecutionTask is { } execution)
        {
            await execution;
        }

        Settle();
    }

    /// <summary>
    /// Leaves the tabs open and the editor showing none of them — the state the close bug used to
    /// leave behind, reached directly so a test can assert what the pane does in it.
    /// </summary>
    public void Deactivate()
    {
        _harness.Documents.ActiveDocument = null;
        Settle();
    }

    /// <summary>
    /// Presses and releases the left button over the editor's patch of the window, whether or not an
    /// editor is showing there.
    /// </summary>
    public void ClickPane()
    {
        Point point = new(WindowWidth / 2, WindowHeight - 10);

        _window.MouseDown(point, MouseButton.Left);
        _window.MouseUp(point, MouseButton.Left);
        Settle();
    }

    /// <summary>
    /// Repaints the window in <paramref name="variant"/>.
    /// </summary>
    public void UseVariant(ThemeVariant variant)
    {
        _window.RequestedThemeVariant = variant;
        Settle();
    }

    /// <summary>
    /// Puts the mouse pointer in the middle of a tab.
    /// </summary>
    public void PointerOver(int index)
    {
        _window.MouseMove(PointIn(TabAt(index)));
        Settle();
    }

    /// <summary>
    /// Takes the pointer off the strip entirely, onto the editor below it.
    /// </summary>
    public void PointerAway()
    {
        _window.MouseMove(new Point(WindowWidth / 2, WindowHeight - 10));
        Settle();
    }

    /// <summary>
    /// Turns the wheel over the strip. Positive is away from the user.
    /// </summary>
    public void Wheel(double notches)
    {
        _window.MouseWheel(PointIn(Tabs), new Vector(0, notches));
        Settle();
    }

    /// <summary>
    /// Puts the mouse pointer in the middle of the scrollbar.
    /// </summary>
    public void PointerOverScrollBar()
    {
        _window.MouseMove(PointIn(HorizontalScrollBar));
        Settle();
    }

    /// <summary>
    /// Drags the scrollbar's thumb <paramref name="distance"/> pixels sideways, negative towards the
    /// first tab, and lets go.
    /// </summary>
    /// <param name="whileDragging">
    /// Read at the far end of the move with the button still down. The dragged state cannot be
    /// observed any other way: it is gone by the time the drag returns.
    /// </param>
    public void DragScrollBar(double distance, Action? whileDragging = null)
    {
        Thumb thumb = HorizontalScrollBar.GetVisualDescendants().OfType<Thumb>().Single();
        Point from = PointIn(thumb);
        Point to = from.WithX(from.X + distance);

        _window.MouseDown(from, MouseButton.Left);
        Settle();

        _window.MouseMove(to);
        Settle();

        whileDragging?.Invoke();

        _window.MouseUp(to, MouseButton.Left);
        Settle();
    }

    /// <summary>
    /// The tab's title.
    /// </summary>
    public TextBlock Title(int index)
        => TabAt(index).GetVisualDescendants().OfType<TextBlock>().First(block => block.Classes.Contains("tabTitle"));

    /// <summary>
    /// The tab's folder-colour bar — painted when the note sits under a coloured folder, and an
    /// unpainted 4px of nothing when it does not.
    /// </summary>
    /// <remarks>
    /// The bar rather than the ViewModel's brush: what is worth asserting is that the binding reached
    /// the control the strip actually draws, and that the tab is the same width either way.
    /// </remarks>
    public Border ColorBar(int index)
        => TabAt(index).GetVisualDescendants().OfType<Border>().First(border => border.Classes.Contains("tabColour"));

    /// <summary>
    /// Colours a folder through the workspace service the pane reads, which is what the tree's
    /// context menu does.
    /// </summary>
    public async Task ColourAsync(string directory, FolderColor colour)
    {
        await _harness.Workspaces.SetFolderColorAsync(
            _harness.Workspaces.ActiveWorkspace!.Id,
            directory,
            colour,
            CancellationToken.None);

        Settle();
    }

    /// <summary>
    /// The folder the harness's notes are written into, so a test can colour it.
    /// </summary>
    public string Root => _harness.Root;

    /// <summary>
    /// The tab's dirty dot.
    /// </summary>
    public Ellipse Dot(int index) => TabAt(index).GetVisualDescendants().OfType<Ellipse>().Single();

    /// <summary>
    /// The tab's close button.
    /// </summary>
    public Button Close(int index) => TabAt(index).GetVisualDescendants().OfType<Button>().Single();

    /// <summary>
    /// How wide the tab is.
    /// </summary>
    public double Width(int index) => TabAt(index).Bounds.Width;

    /// <summary>
    /// The tab's laid-out rectangle, in the strip's own coordinates — where it can be compared with
    /// <see cref="ScrollBarBand"/>.
    /// </summary>
    public Rect TabBand(int index) => BandOf(TabAt(index));

    /// <summary>
    /// Whether the whole tab is inside the strip's viewport.
    /// </summary>
    public bool IsInView(int index)
    {
        Control container = TabAt(index);
        Point left = container.TranslatePoint(default, Viewer) ?? default;

        // A pixel of slack at either end: the offset the strip settles on is a rounded layout value
        // rather than the container's own edge.
        return (left.X >= -1) && ((left.X + container.Bounds.Width) <= (Viewer.Viewport.Width + 1));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _window.Close();
        _harness.Dispose();
    }

    // The tab's own container, which is where the :pointerover the styles read is set, and the
    // thing whose width has to stay the same through every one of these states.
    private Control TabAt(int index)
        => Tabs.ContainerFromIndex(index) ?? throw new InvalidOperationException("Tab " + index + " has no container.");

    // A control's rectangle in the strip's coordinates. Bounds alone are relative to whatever the
    // control happens to hang off — the bar off the ScrollViewer's grid, a tab off the items panel —
    // so the two are only comparable once both are expressed against the strip.
    private Rect BandOf(Visual control)
    {
        Point origin = control.TranslatePoint(default, Strip)
            ?? throw new InvalidOperationException("The control is not under the strip.");

        return new Rect(origin, control.Bounds.Size);
    }

    // A press and a release in the middle of a control, which is what a click amounts to.
    private void Click(Visual control)
    {
        Point point = PointIn(control);

        _window.MouseDown(point, MouseButton.Left);
        _window.MouseUp(point, MouseButton.Left);
    }

    // The middle of a control, in the window's own coordinates, which is what the headless input
    // takes.
    private Point PointIn(Visual control)
        => control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), _window)
            ?? throw new InvalidOperationException("The control is not in the window.");

    // The posted work first — opening a document puts the caret back from a background callback —
    // then the layout pass that applies the styles, then whatever that pass posted in turn.
    private void Settle()
    {
        Dispatcher.UIThread.RunJobs();
        _window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }
}
