using System;
using System.Collections.Generic;
using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaEdit;
using Enigma.MarkdownEditor.Core.Vim;
using Enigma.MarkdownEditor.Desktop.Services;

namespace Enigma.MarkdownEditor.Desktop.Views;

/// <summary>
/// Wires the vim engine to the editor control: which keystrokes reach it, and what the control looks
/// like while it is in charge.
/// </summary>
/// <remarks>
/// <para>
/// The keys arrive on two events, and which one carries what is the whole design here. Commands come
/// from <see cref="InputElement.TextInputEvent"/> rather than from the key event, because that is the
/// only place the actual character is known: which physical key produces <c>$</c> depends on the
/// keyboard layout, and an engine fed key codes would be a vim that only works on a US keyboard. The
/// key event carries what types nothing — <c>Esc</c>, the arrows, and Enter and Backspace for the
/// command line's sake — and, in the command modes, swallows the keys AvaloniaEdit would otherwise
/// act on itself.
/// </para>
/// <para>
/// "The command modes" is every mode but insert, which is the only test this class ever makes about
/// which one the engine is in. A visual mode types nothing either, so what is true of normal mode
/// here is true of both of them: the text input is swallowed and fed to the engine, and Enter, Tab,
/// Home, End and the page keys are taken away from the editor and offered to it. So are the six
/// scroll keys vim spells with Control — <c>Ctrl+D</c> <c>Ctrl+U</c> <c>Ctrl+F</c> <c>Ctrl+B</c>
/// <c>Ctrl+E</c> <c>Ctrl+Y</c> — which the engine takes in the command modes and turns down in
/// insert mode, where <c>Ctrl+B</c> is still the bold shortcut.
/// </para>
/// <para>
/// The engine's options are seeded from the editor's own on every attach — the indentation size and
/// whether Tab writes spaces — so that <c>&gt;&gt;</c> indents exactly as the editor's Tab key does,
/// and the application needs no setting of its own for either.
/// </para>
/// <para>
/// Insert mode touches neither event. That is what "insert mode is ordinary typing" has to mean in a
/// real editor: an input method composes a character over several keystrokes and delivers it as text,
/// and a handler that took those keys apart would break every composed character in the language.
/// </para>
/// <para>
/// Both handlers tunnel, for the same reason <see cref="MarkdownAssists"/>' does: Enter, Tab and the
/// arrows already belong to AvaloniaEdit's own input handler, and a bubbling handler would only ever
/// see what it left.
/// </para>
/// </remarks>
public sealed class VimEditorBehavior
{
    // The keys AvaloniaEdit acts on itself, in its key handler rather than on text input, and that
    // the engine has no name for. Outside insert mode they have to be taken away from it, or Delete
    // would edit a line nobody is typing in. Enter, Backspace, Home, End, Tab and the two page keys
    // are not here: they are translated and offered to the engine like Esc and the arrows, which
    // moves with all but Tab and swallows that one. Everything else is left alone at this point: a
    // key that types a character has to reach the platform's text input to become one, and that is
    // where the engine picks it up.
    private static readonly HashSet<Key> EditorKeys =
    [
        Key.Delete,
        Key.Insert,
    ];

    private readonly TextEditor _editor;
    private readonly Func<IVimSession?> _session;
    private readonly Action<VimExCommand> _exCommand;
    private readonly VimEngine _engine;

    private bool _attached;

    /// <summary>
    /// Initializes a new instance of the <see cref="VimEditorBehavior"/> class.
    /// </summary>
    /// <param name="editor">The editor the pane shows every document in.</param>
    /// <param name="session">
    /// Reads the vim session as it is now. A function rather than the instance, because the pane's
    /// DataContext arrives after the control is built and is replaced rather than mutated.
    /// </param>
    /// <param name="exCommand">
    /// Where <c>:w</c>, <c>:q</c> and <c>:wq</c> go. Not the session's business: what it takes to save
    /// or close a document — the dialog about unsaved changes included — belongs to the pane's
    /// ViewModel, and this class knows only that the engine has asked for it.
    /// </param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public VimEditorBehavior(TextEditor editor, Func<IVimSession?> session, Action<VimExCommand> exCommand)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _exCommand = exCommand ?? throw new ArgumentNullException(nameof(exCommand));

        _engine = new VimEngine(new AvaloniaEditVimBuffer(
            _editor,
            message => _session()?.ReportStatus(message),
            line => _session()?.ReportCommandLine(line)));

        _engine.ModeChanged += OnModeChanged;
        _engine.ExCommandRequested += OnExCommandRequested;
    }

    /// <summary>
    /// Gets the mode the engine is in. Meaningless while vim is switched off.
    /// </summary>
    public VimMode Mode => _engine.Mode;

    /// <summary>
    /// Gets the engine's options, as the last <see cref="Refresh"/> seeded them from the editor.
    /// </summary>
    public VimOptions Options => _engine.Options;

    /// <summary>
    /// Attaches or detaches the engine to follow the setting, and reports where that leaves the mode.
    /// </summary>
    /// <remarks>
    /// Called when the pane's ViewModel arrives and whenever the session says something changed, which
    /// is what makes the settings toggle take effect on the editor that is already open rather than on
    /// the next one.
    /// </remarks>
    public void Refresh()
    {
        bool enabled = _session()?.IsEnabled == true;

        // Before the early return, so a Refresh that changes nothing about the attachment still
        // brings the options up to date with the editor's — cheap, and one less state to reason about.
        _engine.Options.ShiftWidth = _editor.Options.IndentationSize;
        _engine.Options.ExpandTab = _editor.Options.ConvertTabsToSpaces;

        if (enabled == _attached)
        {
            return;
        }

        _attached = enabled;

        if (enabled)
        {
            _editor.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
            _editor.AddHandler(InputElement.TextInputEvent, OnTextInput, RoutingStrategies.Tunnel);
        }
        else
        {
            _editor.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
            _editor.RemoveHandler(InputElement.TextInputEvent, OnTextInput);
        }

        // Reset either way: switching off has to close an insert session's undo group, and switching
        // on has to start from normal mode with a caret normal mode allows.
        _engine.Reset();
        ApplyMode();
    }

    /// <summary>
    /// Starts again on the document the control has just been given.
    /// </summary>
    /// <remarks>
    /// One control serves every tab, so a tab switch replaces the text under the engine. Coming back
    /// to normal mode is both the safe answer and the expected one — vim gives every buffer its own
    /// mode, and nobody expects to land in insert mode in a file they have just opened.
    /// </remarks>
    public void OnDocumentChanged()
    {
        if (!_attached)
        {
            return;
        }

        _engine.Reset();
        ApplyMode();
    }

    private static VimKey? Translate(KeyEventArgs e)
    {
        VimSpecialKey special = e.Key switch
        {
            Key.Escape => VimSpecialKey.Escape,
            Key.Left => VimSpecialKey.Left,
            Key.Right => VimSpecialKey.Right,
            Key.Up => VimSpecialKey.Up,
            Key.Down => VimSpecialKey.Down,

            // Named for the command line and for the motions they are outside it. In insert mode the
            // engine turns them all down and AvaloniaEdit splits the line, rubs a character out, jumps
            // to the line's ends or indents as it always did.
            Key.Enter => VimSpecialKey.Enter,
            Key.Back => VimSpecialKey.Backspace,
            Key.Home => VimSpecialKey.Home,
            Key.End => VimSpecialKey.End,
            Key.Tab => VimSpecialKey.Tab,
            Key.PageUp => VimSpecialKey.PageUp,
            Key.PageDown => VimSpecialKey.PageDown,
            _ => VimSpecialKey.None,
        };

        if (special != VimSpecialKey.None)
        {
            return VimKey.Named(special, Modifiers(e.KeyModifiers));
        }

        // Ctrl+R and the six scroll commands — Ctrl+D, Ctrl+U, Ctrl+F, Ctrl+B, Ctrl+E, Ctrl+Y — are
        // the commands in the contract that are a held key rather than a typed character, and a
        // modified key produces no text input at all, so they have to be recognised from the key
        // code. Safe for these, and only these: vim names each command by its letter, and every
        // layout puts the letter on the key marked with it. Everything else with Control or Alt on it
        // is built the same way and offered to the engine, which turns it down and lets the window's
        // own shortcuts have it.
        if (HasCommandModifier(e.KeyModifiers) && (e.Key >= Key.A) && (e.Key <= Key.Z))
        {
            return VimKey.Typed((char)('a' + (e.Key - Key.A)), Modifiers(e.KeyModifiers));
        }

        return null;
    }

    private static bool HasCommandModifier(KeyModifiers modifiers)
        => (modifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) != KeyModifiers.None;

    private static VimModifiers Modifiers(KeyModifiers modifiers)
    {
        VimModifiers translated = VimModifiers.None;

        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            translated |= VimModifiers.Shift;
        }

        if (modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta))
        {
            translated |= VimModifiers.Control;
        }

        if (modifiers.HasFlag(KeyModifiers.Alt))
        {
            translated |= VimModifiers.Alt;
        }

        return translated;
    }

    private void OnModeChanged(object? sender, VimMode mode) => ApplyMode();

    private void OnExCommandRequested(object? sender, VimExCommand command) => _exCommand(command);

    private void ApplyMode()
    {
        _session()?.ReportMode(_attached ? _engine.Mode : VimMode.Normal);

        // The nearest thing to a block caret that costs nothing: AvaloniaEdit draws the caret a whole
        // character wide in overstrike mode. The command modes type nothing, so the overwriting half
        // of that mode never comes into play.
        _editor.TextArea.OverstrikeMode = _attached && (_engine.Mode != VimMode.Insert);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_editor.IsReadOnly || (_editor.Document is null))
        {
            return;
        }

        if (Translate(e) is { } key)
        {
            e.Handled = _engine.HandleKey(key);

            return;
        }

        // A key that types nothing and means nothing to vim. Left alone in insert mode, where it is
        // the editor's; taken away from the editor in the command modes, where nothing may edit the
        // document behind the engine's back.
        e.Handled = (_engine.Mode != VimMode.Insert)
            && !HasCommandModifier(e.KeyModifiers)
            && EditorKeys.Contains(e.Key);
    }

    private void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (_editor.IsReadOnly || (_editor.Document is null) || (_engine.Mode == VimMode.Insert))
        {
            return;
        }

        // Handled whatever the text turns out to be: a command mode types nothing, so a character
        // with no command behind it must still not reach the document.
        e.Handled = true;

        string text = e.Text ?? string.Empty;

        for (int index = 0; index < text.Length; index++)
        {
            if (_engine.Mode == VimMode.Insert)
            {
                // One event can carry several characters — an input method delivers a composed word
                // in one go — and one of them has just switched to insert mode. The rest is typing,
                // and the editor will not see this event again, so it is typed here. Inside the
                // session's undo group, because that is what the command that opened it started.
                Insert(text[index..]);

                return;
            }

            _engine.HandleKey(VimKey.Typed(text[index]));
        }
    }

    private void Insert(string text)
    {
        if (_editor.Document is not { } document)
        {
            return;
        }

        int offset = _editor.CaretOffset;

        document.Insert(offset, text);
        _editor.CaretOffset = offset + text.Length;
    }
}
