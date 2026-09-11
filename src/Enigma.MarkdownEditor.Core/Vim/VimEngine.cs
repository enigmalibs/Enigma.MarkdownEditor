using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Enigma.MarkdownEditor.Core.Markdown;

namespace Enigma.MarkdownEditor.Core.Vim;

/// <summary>
/// Modal editing as a state machine: which mode the editor is in, and what each keystroke does to the
/// buffer while it is in that one.
/// </summary>
/// <remarks>
/// <para>
/// Nothing here knows about Avalonia, a key event or a text control. The keys arrive as
/// <see cref="VimKey"/> values and everything the engine does happens through <see cref="IVimBuffer"/>,
/// which is what lets the whole command surface be driven from a test with a string in it. That
/// matters more here than anywhere else in the application: a modal editor's bugs are combinations —
/// this motion, on that line, in that mode — and combinations are only affordable to cover when a
/// case costs one line.
/// </para>
/// <para>
/// The caret rule that shapes most of the code: in normal mode the caret sits <em>on</em> a character,
/// never past the last one, because that is what makes <c>x</c> at the end of a line delete something
/// and <c>$</c> a position rather than a limit. Insert mode drops the rule, because a caret that
/// cannot reach the end of a line cannot append to it.
/// </para>
/// <para>
/// The second rule, and the one PHASE02 is built on: every command that changes the buffer does it in
/// exactly one call to <see cref="IVimBuffer.Replace"/>, so that one command is one undo step without
/// a group having to say so. The single exception is a change — <c>cc</c>, <c>cw</c>, <c>C</c> —
/// where what is typed afterwards belongs to the same step, and which therefore opens the same undo
/// group an insert session does and leaves it open until <c>Esc</c>.
/// </para>
/// <para>
/// A visual mode is one extra piece of state: the anchor, the end of the selection that does not
/// move. Everything else about it is what the modes already do — the motions move the caret, the
/// selection is recomputed from the anchor and the caret afterwards, and an operator acts on that
/// range instead of on one a motion typed after it would have produced. The commands that act on a
/// selection without being operators — a put over it, <c>r</c> over every character of it, a join of
/// its lines — do the same: read the range, leave the mode, act. The one thing kept after the mode
/// is left is where the selection was, as lines and columns, so that <c>gv</c> can go back to it.
/// </para>
/// <para>
/// The command line is the third piece of state, and the only one that is not a mode: while it is
/// open every keystroke is a character of a line rather than a command, but the editor is still in
/// whatever mode it was in and says so. That is what vim looks like from the outside, and it keeps
/// the mode vocabulary to the four the contract names. The line is the engine's, not a text box of the
/// editor's, so <c>:wq</c> can be typed in a test. A search typed on it is a motion whose target
/// arrived late: the command that opened the line kept the operator and the count that were waiting,
/// and Enter runs the motion through <see cref="Execute"/> like any other, so <c>d/foo</c> deletes,
/// <c>v/foo</c> selects, and <c>.</c> remembers.
/// </para>
/// <para>
/// Three more pieces of state make the engine remember what it did. The registers
/// (<see cref="Registers"/>) hold what was taken out, by name. The last change — the command, the text
/// typed after it if it entered insert mode, and how far a selection reached if it was over one — is
/// what <c>.</c> runs again; it is caught at the end of every command that edits, and for an insert
/// command at the <c>Esc</c> that ends it, with the text read off the buffer's own change reports
/// because insert mode never shows the engine a key. And a recording, when <c>q</c> has opened one,
/// collects every key the engine takes and the text every insert session typed, in the notation
/// <see cref="VimKeyNotation"/> writes, into a register that <c>@</c> then feeds back through
/// <see cref="HandleKey"/> key by key. All three outlive <see cref="Reset"/>, as vim's own do.
/// </para>
/// <para>
/// The view is the one thing the engine reads without owning. Everything else here is about the
/// text, and the text is the buffer's; but <c>H</c> is "the top line on screen" and <c>Ctrl+D</c> is
/// "half a window", and neither can be worked out from the text. So the buffer reports where the view
/// stands — <see cref="IVimBuffer.FirstVisibleLine"/>, <see cref="IVimBuffer.VisibleLineCount"/> —
/// and takes an instruction to move it, <see cref="IVimBuffer.ScrollToLine"/>, and the arithmetic
/// between the two lives in <see cref="VimViewport"/>. Two rules go with it. A scroll command works
/// out where the caret should be from where it <em>asked</em> the view to go, not from where the view
/// went, so that the same keys mean the same thing over a view that cannot scroll as far as vim's
/// would. And a command that put the caret's line on an edge of the screen — a page, a line scroll,
/// the <c>z</c> family — is the one kind that is not followed by the usual "show the caret", because
/// an editor's idea of showing the caret keeps a margin round it, and the margin would undo the
/// command; every other command, the half-page scrolls included, ends with the caret brought on
/// screen.
/// </para>
/// <para>
/// The whole v1 contract is here — the modes, the motions, the operators, the counts, the unnamed
/// register, the visual pair, search and the ex commands — and the tier after it: the WORD motions,
/// the line, paragraph, sentence and bracket motions, the join, the shift and case operators, the
/// <c>Z</c> pair, the commands that act on a selection, the named registers, <c>.</c> and macros,
/// regular-expression search as a motion, <c>:s</c>, <c>:N</c> and <c>:set</c>, the screen motions
/// and the scroll commands.
/// </para>
/// </remarks>
public sealed class VimEngine
{
    private const int NoDesiredColumn = -1;
    private const int NoAnchor = -1;

    // Vim's own wording, down to the capitals: someone who knows vim reads these without reading
    // them, and someone who does not is told which way the search went round.
    private const string HitBottom = "search hit BOTTOM, continuing at TOP";
    private const string HitTop = "search hit TOP, continuing at BOTTOM";
    private const string HitBottomNoWrap = "search hit BOTTOM without match for: ";
    private const string HitTopNoWrap = "search hit TOP without match for: ";
    private const string NoPreviousSearch = "No previous search pattern";
    private const string MarkNotSet = "Mark not set";
    private const string NoRangeAllowed = "No range allowed";
    private const string NothingInRegister = "Nothing in the register";
    private const string NoPreviousMacro = "No previously used register";
    private const string MacroNestingTooDeep = "Macro nesting too deep";

    // How deep '@a' inside 'a' may go before it is called a mistake: vim's own limit, and far past
    // any recursion that is doing work rather than looping.
    private const int MaxMacroDepth = 100;

    private readonly IVimBuffer _buffer;
    private readonly KeySequenceParser _parser = new();

    // The column j and k aim for, kept across a run of them so that passing through a short line does
    // not cost the column the caret started in. NoDesiredColumn means "read it off the caret";
    // int.MaxValue is what '$' leaves behind, so that j and k walk down the ends of the lines.
    private int _desiredColumn = NoDesiredColumn;

    // The end of the visual selection that does not move, or NoAnchor when nothing is selected. The
    // moving end is the caret itself, which is why a visual mode needs one value here and not two.
    private int _visualAnchor = NoAnchor;

    // Where the last selection was, for 'gv' to go back to, or null when there has not been one in
    // this document. Lines and columns rather than offsets — vim's own marks are — so that a change
    // the operator made under it, a '>' that indented the lines, does not move it off them.
    private RememberedSelection? _lastVisual;

    // The command line as it is being typed, first character and all, or null when it is not open.
    // One field rather than a mode, because nothing else about the engine changes while it is up.
    private StringBuilder? _commandLine;

    // The command that opened the line now up — the operator, count and register a '/' or '?' was
    // typed behind — read when Enter closes it. Meaningful only while _commandLine is not null.
    private VimCommand _commandLineOpener;

    // What '/', '?', '*', '#' or ':s' last looked for, which way it went, and whether smartcase may
    // read it: what 'n' and 'N' repeat. The pattern is kept rather than the matches, so that 'n' after
    // an edit searches the text as it is now — and it outlives a document swap for the same reason the
    // register does: vim's search pattern is not the buffer's.
    private LastSearch? _lastSearch;

    // What 'f', 'F', 't' or 'T' last looked for, which is what ';' and ',' repeat. Kept even when the
    // find missed — 'fz' on a line with no 'z' then ';' on the next line finds the 'z' there — and,
    // like the search pattern, it outlives a document swap.
    private VimMotion _lastFind = VimMotion.None;
    private char _lastFindCharacter;

    private bool _insertGroupOpen;

    // What the insert session now open has typed, read off the buffer's change reports.
    private readonly VimInsertRecorder _typed = new();

    // The register a '"' named on the command that is running, or '\0' for the unnamed one. State
    // rather than a parameter because it is read three calls down, by Fill and the puts — and because
    // a delete inside a visual put has to fill the unnamed register whatever the put was named with.
    private char _activeRegister;

    // What '.' repeats, or null before the first change.
    private RecordedChange? _lastChange;

    // The command that opened the insert session now running, and the selection it was typed over,
    // waiting for the Esc that closes the record and makes it the last change.
    private VimCommand? _insertCommand;
    private VisualExtent? _insertVisual;

    // Set while '.' is replaying: nothing the replay does is a change of its own to remember.
    private bool _replaying;

    // The register a 'q' is recording into, as it was typed — a capital appends — or null when none,
    // and the keys collected so far.
    private char? _recording;
    private readonly StringBuilder _macro = new();

    // How many '@' replays are inside one another, whether the innermost has hit something that stops
    // them all, and which register was replayed last, for '@@'.
    private int _macroDepth;
    private bool _macroFailed;
    private char _lastMacro;

    /// <summary>
    /// Initializes a new instance of the <see cref="VimEngine"/> class over a buffer.
    /// </summary>
    /// <param name="buffer">The text this engine reads and edits.</param>
    /// <param name="options">
    /// The options the engine reads while it works, or <see langword="null"/> for the defaults. The
    /// instance is kept, not copied: whoever hands it over goes on owning it and may change it later.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is <see langword="null"/>.</exception>
    public VimEngine(IVimBuffer buffer, VimOptions? options = null)
    {
        _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        Options = options ?? new VimOptions();

        _buffer.Changed += OnBufferChanged;
    }

    /// <summary>
    /// Raised after the mode has changed, with the mode it changed to.
    /// </summary>
    public event EventHandler<VimMode>? ModeChanged;

    /// <summary>
    /// Raised when an ex command has been typed and recognised.
    /// </summary>
    /// <remarks>
    /// An event rather than a call on <see cref="IVimBuffer"/>, because saving and closing a file are
    /// not things a buffer does. This is the engine's one way out to the application, and the
    /// application's answer — a dialog about unsaved changes, a save that fails — is its own business
    /// and never comes back here.
    /// </remarks>
    public event EventHandler<VimExCommand>? ExCommandRequested;

    /// <summary>
    /// Gets the mode the engine is in.
    /// </summary>
    public VimMode Mode { get; private set; } = VimMode.Normal;

    /// <summary>
    /// Gets the options the engine reads while it works — how wide a shift is, and whether it is
    /// written as spaces.
    /// </summary>
    /// <remarks>
    /// The object is mutable and shared on purpose: the editor seeds it from its own settings when it
    /// attaches the engine, so <c>&gt;&gt;</c> indents exactly as the editor's Tab key does.
    /// </remarks>
    public VimOptions Options { get; }

    /// <summary>
    /// Gets what the last delete, change or yank took out, or <see langword="null"/> if nothing has.
    /// </summary>
    /// <remarks>
    /// The unnamed register — <see cref="VimRegisters.Unnamed"/>, kept here by its old name because
    /// it is the one nearly every test asks about.
    /// </remarks>
    public VimRegister? Register => Registers.Unnamed;

    /// <summary>
    /// Gets the registers: the unnamed one, the named ones, the yank register and whatever a
    /// recording has put where.
    /// </summary>
    /// <remarks>
    /// Read-only from here. The engine fills them; the editor and the tests look.
    /// </remarks>
    public VimRegisters Registers { get; } = new();

    /// <summary>
    /// Gets the register a <c>q</c> is recording into, as it was typed, or <see langword="null"/>
    /// when nothing is being recorded.
    /// </summary>
    public char? RecordingRegister => _recording;

    /// <summary>
    /// Gets a value indicating whether a command is half typed — <c>3d</c> waiting for its motion,
    /// <c>g</c>, <c>Z</c> or <c>z</c> for its second key, <c>r</c> for the character to write,
    /// <c>"</c>, <c>q</c> or <c>@</c> for the register, or a command line still being written.
    /// </summary>
    public bool HasPendingSequence => _parser.HasPendingSequence || (_commandLine is not null);

    /// <summary>
    /// Gets the command line as it stands, including the <c>:</c>, <c>/</c> or <c>?</c> that opened
    /// it, or <see langword="null"/> when none is open.
    /// </summary>
    public string? CommandLine => _commandLine?.ToString();

    /// <summary>
    /// Offers the engine a keystroke.
    /// </summary>
    /// <param name="key">What was pressed.</param>
    /// <returns>
    /// <see langword="true"/> when the engine took the key, which means the editor must not also act
    /// on it; <see langword="false"/> to let it through to the editor and to the window's shortcuts.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The two answers are the whole contract with the view. Insert mode says <see langword="false"/>
    /// to everything but <c>Esc</c>, which is what "insert mode is ordinary typing" means in practice
    /// — including for input methods, which compose their text through the editor without the engine
    /// ever seeing the keys. Normal mode says <see langword="true"/> to every bare key, even one it
    /// has no meaning for, because a key that fell through would type a character into a document that
    /// is not being typed into; keys held with Control or Alt are the exception, and belong to the
    /// window — all but the seven the contract claims, <c>Ctrl+R</c> and the six scroll commands
    /// <c>Ctrl+D</c> <c>Ctrl+U</c> <c>Ctrl+F</c> <c>Ctrl+B</c> <c>Ctrl+E</c> <c>Ctrl+Y</c>, which are
    /// taken in the command modes only.
    /// </para>
    /// <para>
    /// While a recording is open every key the engine takes goes into it, after it has done its work:
    /// the text an insert session typed is written into the recording by the <c>Esc</c> that ends the
    /// session, and has to come before the <c>&lt;Esc&gt;</c> itself. The <c>qa</c> that opened the
    /// recording and the <c>q</c> that closes it are not part of it, and neither is anything a replay
    /// runs — that is already in the register it came from.
    /// </para>
    /// </remarks>
    public bool HandleKey(VimKey key)
    {
        bool wasRecording = _recording is not null;
        bool taken = Dispatch(key);

        if (taken && wasRecording && (_recording is not null) && (_macroDepth == 0))
        {
            _macro.Append(VimKeyNotation.Format(key));
        }

        return taken;
    }

    private bool Dispatch(VimKey key)
    {
        if (Mode == VimMode.Insert)
        {
            if (key.Special == VimSpecialKey.Escape)
            {
                LeaveInsert();

                return true;
            }

            return false;
        }

        // Before the parser and before Esc: while a command line is open every key belongs to it,
        // including the ones that would otherwise cancel a half-typed command or start a new one.
        if (_commandLine is not null)
        {
            return HandleCommandLineKey(key);
        }

        // A key held with Control or Alt belongs to the window, with seven exceptions: redo, read
        // here, and the six scroll commands, which go through the grammar like any other key so that
        // a count in front of one reaches it — '5<C-d>' scrolls five lines.
        if (key.HasCommandModifier && !KeySequenceParser.IsControlCommand(key))
        {
            if (!IsRedo(key))
            {
                return false;
            }

            _parser.Reset();

            // Redo is not among the things a selection can be asked to do — 'u' over one is the
            // lowercase operator, as it is in vim, so neither half of undo is available while
            // selecting — and the key is taken and named rather than acted on.
            if (IsVisual)
            {
                NotACommand("<C-r>");
            }
            else
            {
                Execute(new VimCommand(VimAction.Redo));
            }

            return true;
        }

        if (key.Special == VimSpecialKey.Escape)
        {
            _parser.Reset();
            ClearStatus();

            // Also drops a selection made with the mouse while in normal mode. Esc is the key vim
            // uses to get out of every other kind of state, and a highlight nothing is acting on any
            // more is one of them.
            LeaveVisual();

            return true;
        }

        // Swallowed without a word: a function key or a Page Down has no vim meaning, and saying so
        // for every one of them would turn the status bar into a complaint about keys nobody pressed
        // on purpose. Tab is here too — there is no jump list for it to walk. The same goes for the
        // control characters some platforms report as text. What is half typed goes with it, so that
        // an 'r' waiting for its character is not still waiting after a stray key that means nothing.
        // Enter and Backspace are not here: outside the command line they are motions, and the
        // parser reads them.
        if ((key.Special is VimSpecialKey.Other or VimSpecialKey.Tab)
            || (key.IsCharacter && char.IsControl(key.Character)))
        {
            _parser.Reset();

            return true;
        }

        // The 'q' that stops a recording, taken before the parser sees it: only the engine knows a
        // recording is open. A 'q' typed into a sequence already under way — '3q', 'dq' — is that
        // sequence's to read.
        if ((_recording is not null) && !_parser.HasPendingSequence && key.IsCharacter && (key.Character == 'q'))
        {
            StopRecording();

            return true;
        }

        VimParseResult result = _parser.Feed(key, Mode);

        switch (result.Status)
        {
            case VimParseStatus.Pending:
                break;

            case VimParseStatus.Invalid:
                NotACommand(result.Sequence);
                break;

            default:
                Execute(result.Command);
                break;
        }

        return true;
    }

    /// <summary>
    /// Puts the engine back where it starts: normal mode, nothing pending, no message, and a caret
    /// that obeys normal mode's rule again.
    /// </summary>
    /// <remarks>
    /// What the editor calls when the ground moves under the engine — vim switched off or on, or a
    /// different document swapped into the control. It closes an open insert session's undo group
    /// rather than leaving it open on a buffer nobody is editing any more. The registers, the search
    /// pattern, the last find, the last change and an open recording survive: vim's own outlive the
    /// buffer they were filled from, which is what makes yanking in one file and putting in another
    /// work, and <c>n</c>, <c>;</c> and <c>.</c> after a tab switch do the same thing they did before
    /// it. An insert session cut short this way still becomes the last change, with what was typed up
    /// to then.
    /// </remarks>
    public void Reset()
    {
        if (Mode == VimMode.Insert)
        {
            CloseInsertRecord();
        }

        EndInsertGroup();
        _parser.Reset();
        CloseCommandLine();
        _desiredColumn = NoDesiredColumn;
        ClearStatus();

        LeaveVisual();
        SetMode(VimMode.Normal);
        SetCaret(_buffer.CaretOffset);

        // After LeaveVisual, which would have just remembered it. A selection is a place in a
        // document, and the document may be a different one now — vim's marks are the buffer's too.
        _lastVisual = null;
    }

    private bool IsVisual => Mode is VimMode.Visual or VimMode.VisualLine;

    private VimLine CurrentLine => _buffer.GetLineByOffset(_buffer.CaretOffset);

    private static bool IsRedo(VimKey key)
        => key.IsCharacter
            && ((key.Character == 'r') || (key.Character == 'R'))
            && ((key.Modifiers & VimModifiers.Alt) == VimModifiers.None);

    private static string Repeat(string text, int count, string separator)
    {
        if (count <= 1)
        {
            return text;
        }

        StringBuilder builder = new(text);

        for (int index = 1; index < count; index++)
        {
            builder.Append(separator).Append(text);
        }

        return builder.ToString();
    }

    private void Execute(VimCommand command)
    {
        ClearStatus();

        // Every command starts from a caret normal mode would allow. Nothing the engine does can
        // leave one past the last character of a line, but a mouse click in the empty space beyond
        // the end of a line can, and a command that ran from there would be acting on a position the
        // buffer does not have.
        SetCaret(_buffer.CaretOffset);

        _activeRegister = command.Register;

        int count = command.EffectiveCount;
        bool change = IsChange(command);

        // Measured before the command runs and leaves the mode: how far the selection reached is what
        // '.' has to reproduce from wherever the caret is by then.
        VisualExtent? extent = (change && IsVisual) ? CurrentExtent() : null;

        // Resolved before the switch because it is the one motion that writes state as it is read:
        // 'f' and its three siblings are what ';' repeats, and both the move and the operator paths
        // need the answer.
        VimFindRequest? find = ResolveFind(command.Motion, command.Argument);

        switch (command.Action)
        {
            case VimAction.Move:

                // A move carrying a text object is 'viw' and its family: the parser produces one only
                // in a visual mode, where naming the text is the whole command.
                if (command.TextObject != VimTextObject.None)
                {
                    SelectTextObject(command.TextObject, count);
                }
                else
                {
                    ApplyMotion(command, find);
                }

                break;

            case VimAction.Operate:

                // The one command whose meaning depends on the mode rather than on the keys: with a
                // selection on screen the operator has already been told what to act on, and the
                // motion the parser never read is the selection itself.
                if (IsVisual)
                {
                    ApplyVisualOperator(command);
                }
                else
                {
                    ApplyOperator(command, find);
                }

                break;

            case VimAction.InsertBeforeCaret:
                EnterInsertAt(_buffer.CaretOffset);
                break;

            case VimAction.InsertAtLineStart:
                EnterInsertAt(CurrentLine.FirstNonBlankOffset);
                break;

            case VimAction.InsertAfterCaret:
                EnterInsertAt(Math.Min(_buffer.CaretOffset + 1, CurrentLine.EndOffset));
                break;

            case VimAction.InsertAtLineEnd:
                EnterInsertAt(CurrentLine.EndOffset);
                break;

            case VimAction.OpenLineBelow:
                OpenLine(below: true);
                break;

            case VimAction.OpenLineAbove:
                OpenLine(below: false);
                break;

            case VimAction.VisualCharacterwise:
                EnterVisual(VimMode.Visual);
                break;

            case VimAction.VisualLinewise:
                EnterVisual(VimMode.VisualLine);
                break;

            case VimAction.DeleteCharacter:
                DeleteCharacters(count);
                break;

            case VimAction.PutAfter:
                Put(count, after: true);
                break;

            case VimAction.PutBefore:
                Put(count, after: false);
                break;

            case VimAction.ReplaceCharacter:

                // The second command whose meaning the mode decides: over a selection the character
                // goes over every selected one rather than over the caret's.
                if (IsVisual)
                {
                    FillSelection(command.Argument);
                }
                else
                {
                    ReplaceCharacters(command.Argument, count);
                }

                break;

            case VimAction.ReplaceSelection:
                ReplaceSelection(count, keepRegister: command.Argument == 'P');
                break;

            case VimAction.ToggleCase:
                ToggleCase(count);
                break;

            case VimAction.Join:
            case VimAction.JoinRaw:

                // And the third: over a selection the lines to join are the selected ones, not a count.
                if (IsVisual)
                {
                    JoinSelection(insertSpace: command.Action == VimAction.Join);
                }
                else
                {
                    Join(count, insertSpace: command.Action == VimAction.Join);
                }

                break;

            case VimAction.SwapVisualEnds:
                SwapVisualEnds();
                break;

            case VimAction.ReselectVisual:
                ReselectVisual();
                break;

            case VimAction.WriteQuit:
                ExCommandRequested?.Invoke(this, VimExCommand.WriteQuit);
                break;

            case VimAction.QuitDiscard:
                ExCommandRequested?.Invoke(this, VimExCommand.QuitDiscard);
                break;

            case VimAction.BeginCommandLine:
                OpenCommandLine(command);
                break;

            case VimAction.Undo:
                Rewind(count, forward: false);
                break;

            case VimAction.Redo:
                Rewind(count, forward: true);
                break;

            case VimAction.Repeat:
                RepeatLastChange(command);
                break;

            case VimAction.RecordMacro:
                StartRecording(command.Argument);
                break;

            case VimAction.PlayMacro:
                PlayMacro(command.Argument, count);
                break;

            case VimAction.PlayLastMacro:
                PlayMacro('@', count);
                break;

            case VimAction.ScrollHalfPageDown:
                ScrollHalfPage(command, down: true);
                break;

            case VimAction.ScrollHalfPageUp:
                ScrollHalfPage(command, down: false);
                break;

            case VimAction.ScrollPageDown:
                ScrollPage(count, down: true);
                break;

            case VimAction.ScrollPageUp:
                ScrollPage(count, down: false);
                break;

            case VimAction.ScrollLineDown:
                ScrollLines(count, down: true);
                break;

            case VimAction.ScrollLineUp:
                ScrollLines(count, down: false);
                break;

            case VimAction.ScrollCaretTop:
                ScrollCaret(command, VimScrollAlignment.Top, firstNonBlank: false);
                break;

            case VimAction.ScrollCaretCenter:
                ScrollCaret(command, VimScrollAlignment.Center, firstNonBlank: false);
                break;

            case VimAction.ScrollCaretBottom:
                ScrollCaret(command, VimScrollAlignment.Bottom, firstNonBlank: false);
                break;

            case VimAction.ScrollCaretTopToFirstNonBlank:
                ScrollCaret(command, VimScrollAlignment.Top, firstNonBlank: true);
                break;

            case VimAction.ScrollCaretCenterToFirstNonBlank:
                ScrollCaret(command, VimScrollAlignment.Center, firstNonBlank: true);
                break;

            case VimAction.ScrollCaretBottomToFirstNonBlank:
                ScrollCaret(command, VimScrollAlignment.Bottom, firstNonBlank: true);
                break;

            default:
                break;
        }

        // A change is remembered when it is over. One that entered insert mode is not over yet: what
        // is typed until Esc belongs to it, and the record closes then. A replay remembers nothing —
        // it is the last change, being done again.
        if (change)
        {
            if (Mode == VimMode.Insert)
            {
                _insertCommand = command;
                _insertVisual = extent;
            }
            else if (!_replaying)
            {
                _lastChange = new RecordedChange(command, null, extent);
            }
        }

        // Here rather than inside each motion: everything a visual mode does ends with the caret
        // somewhere, and the selection is a function of where that is.
        if (IsVisual)
        {
            ShowSelection();
        }

        // Every command but the ones that placed the view themselves: those put the caret's line on
        // an edge of the screen on purpose, and an editor asked to "show the caret" afterwards would
        // pull it back off the edge by whatever margin it likes to keep — which undoes 'zt', and
        // would leave a held Ctrl+E scrolling one line down and one line back for ever.
        if (!PlacesView(command.Action))
        {
            _buffer.ScrollToCaret();
        }
    }

    /// <summary>
    /// Whether a command puts a chosen line on a chosen edge of the screen, and so must not be
    /// followed by the editor's own idea of where the caret should be.
    /// </summary>
    /// <remarks>
    /// The two half-page scrolls are not here: they move the caret through the middle of the screen
    /// rather than onto an edge, so the editor's margin never fights them, and the safety net is
    /// worth keeping for a wrapped line at the bottom that the engine's count of lines did not see.
    /// </remarks>
    private static bool PlacesView(VimAction action)
        => action is VimAction.ScrollPageDown
            or VimAction.ScrollPageUp
            or VimAction.ScrollLineDown
            or VimAction.ScrollLineUp
            or VimAction.ScrollCaretTop
            or VimAction.ScrollCaretCenter
            or VimAction.ScrollCaretBottom
            or VimAction.ScrollCaretTopToFirstNonBlank
            or VimAction.ScrollCaretCenterToFirstNonBlank
            or VimAction.ScrollCaretBottomToFirstNonBlank;

    /// <summary>
    /// Whether a command edits the buffer on purpose, which is what makes it the one <c>.</c> repeats.
    /// </summary>
    /// <remarks>
    /// Every operator but yank, the single-key edits, the puts, the joins, the inserts, and their
    /// visual forms — every one of them is <c>.</c>'s business whether or not it found anything to
    /// do, as in vim, where <c>x</c> on an empty line is still the last change. A motion, a yank,
    /// undo and redo, an ex command, a recording and a replay never are.
    /// </remarks>
    private static bool IsChange(VimCommand command)
        => command.Action switch
        {
            VimAction.Operate => command.Operator != VimOperator.Yank,
            VimAction.InsertBeforeCaret
                or VimAction.InsertAtLineStart
                or VimAction.InsertAfterCaret
                or VimAction.InsertAtLineEnd
                or VimAction.OpenLineBelow
                or VimAction.OpenLineAbove
                or VimAction.DeleteCharacter
                or VimAction.PutAfter
                or VimAction.PutBefore
                or VimAction.ReplaceCharacter
                or VimAction.ReplaceSelection
                or VimAction.ToggleCase
                or VimAction.Join
                or VimAction.JoinRaw => true,
            _ => false,
        };

    /// <summary>
    /// Works out which find a motion stands for, and remembers a freshly typed one.
    /// </summary>
    /// <param name="motion">The motion the parser produced.</param>
    /// <param name="argument">The character it carries, for the four that carry one.</param>
    /// <returns>
    /// The find to run, or <see langword="null"/> when the motion is not one — including a <c>;</c>
    /// or <c>,</c> typed before any find, which is a silent no-op rather than a message.
    /// </returns>
    /// <remarks>
    /// The one place the memory is written, and it is written on a miss too: a find that found
    /// nothing is still the find <c>;</c> repeats. <c>,</c> reads the memory back the other way
    /// round and never writes it, so <c>fa</c> then <c>,</c> then <c>;</c> still goes forwards.
    /// </remarks>
    private VimFindRequest? ResolveFind(VimMotion motion, char argument)
    {
        switch (motion)
        {
            case VimMotion.FindForward:
            case VimMotion.FindBackward:
            case VimMotion.TillForward:
            case VimMotion.TillBackward:
                _lastFind = motion;
                _lastFindCharacter = argument;

                return new VimFindRequest(motion, argument, Repeat: false);

            case VimMotion.RepeatFind when _lastFind != VimMotion.None:
                return new VimFindRequest(_lastFind, _lastFindCharacter, Repeat: true);

            case VimMotion.RepeatFindReverse when _lastFind != VimMotion.None:
                return new VimFindRequest(Reversed(_lastFind), _lastFindCharacter, Repeat: true);

            default:
                return null;
        }
    }

    private static VimMotion Reversed(VimMotion find)
        => find switch
        {
            VimMotion.FindForward => VimMotion.FindBackward,
            VimMotion.FindBackward => VimMotion.FindForward,
            VimMotion.TillForward => VimMotion.TillBackward,
            VimMotion.TillBackward => VimMotion.TillForward,
            _ => VimMotion.None,
        };

    private void ApplyMotion(VimCommand command, VimFindRequest? find)
    {
        VimLine line = CurrentLine;
        int caret = _buffer.CaretOffset;
        int count = command.EffectiveCount;
        bool hasCount = command.HasCount;

        switch (command.Motion)
        {
            case VimMotion.Left:
                SetCaret(Math.Max(line.Offset, caret - count));
                break;

            case VimMotion.Right:
                SetCaret(Math.Min(line.LastCaretOffset, caret + count));
                break;

            case VimMotion.Up:
                MoveToLine(line.Number - count);
                return;

            case VimMotion.Down:
                MoveToLine(line.Number + count);
                return;

            case VimMotion.LineStart:
                SetCaret(line.Offset);
                break;

            case VimMotion.LineEnd:

                // The one motion that leaves a column behind on purpose: after it, j and k walk down
                // the ends of the lines rather than down the column it happened to land in. A count
                // reaches the end of a line below, which is what makes '3D' delete to there.
                SetCaret(_buffer.GetLine(Math.Min((line.Number + count) - 1, _buffer.LineCount)).LastCaretOffset);
                _desiredColumn = int.MaxValue;
                return;

            case VimMotion.WordForward:
                SetCaret(Walk(caret, count, VimWords.NextStart));
                break;

            case VimMotion.WordBackward:
                SetCaret(Walk(caret, count, VimWords.PreviousStart));
                break;

            case VimMotion.WordEnd:
                SetCaret(Walk(caret, count, VimWords.End));
                break;

            case VimMotion.WordForwardBig:
                SetCaret(Walk(caret, count, VimWords.NextStartBig));
                break;

            case VimMotion.WordBackwardBig:
                SetCaret(Walk(caret, count, VimWords.PreviousStartBig));
                break;

            case VimMotion.WordEndBig:
                SetCaret(Walk(caret, count, VimWords.EndBig));
                break;

            case VimMotion.FirstNonBlank:
                SetCaret(line.FirstNonBlankOffset);
                break;

            case VimMotion.Column:
                SetCaret(VimRanges.ColumnOffset(line, count));
                break;

            case VimMotion.RightWrapping:
                SetCaret(Walk(caret, count, VimWhichWrap.Right));
                break;

            case VimMotion.LeftWrapping:
                SetCaret(Walk(caret, count, VimWhichWrap.Left));
                break;

            case VimMotion.LineDownFirstNonBlank:
                SetCaret(_buffer.GetLine(Math.Min(line.Number + count, _buffer.LineCount)).FirstNonBlankOffset);
                break;

            case VimMotion.LineUpFirstNonBlank:
                SetCaret(_buffer.GetLine(Math.Max(line.Number - count, 1)).FirstNonBlankOffset);
                break;

            case VimMotion.CurrentLineFirstNonBlank:
                SetCaret(_buffer.GetLine(Math.Min((line.Number + count) - 1, _buffer.LineCount)).FirstNonBlankOffset);
                break;

            case VimMotion.MatchingBracket:

                // Two motions under one key. With a count it is a place in the file by percentage;
                // without one it is the matching bracket, and a caret with no bracket to match stays
                // where it is, without a word.
                if (hasCount)
                {
                    SetCaret(_buffer.GetLine(VimRanges.PercentLine(_buffer, count)).FirstNonBlankOffset);
                }
                else if (VimBrackets.Match(_buffer, caret) is { } match)
                {
                    SetCaret(match);
                }

                break;

            case VimMotion.ParagraphForward:
            case VimMotion.ParagraphBackward:

                // The end of the buffer comes back as the offset past the last character, and normal
                // mode's clamp pulls it onto the character, which is where vim leaves it too.
                if (VimParagraphs.Paragraph(_buffer, caret, command.Motion == VimMotion.ParagraphForward, count) is { } paragraph)
                {
                    SetCaret(paragraph);
                }

                break;

            case VimMotion.SentenceForward:
            case VimMotion.SentenceBackward:
                if (VimParagraphs.Sentence(_buffer, caret, command.Motion == VimMotion.SentenceForward, count) is { } sentence)
                {
                    SetCaret(sentence);
                }

                break;

            case VimMotion.FindForward:
            case VimMotion.FindBackward:
            case VimMotion.TillForward:
            case VimMotion.TillBackward:
            case VimMotion.RepeatFind:
            case VimMotion.RepeatFindReverse:

                // A find that matches nothing leaves everything alone, without a word: the caret,
                // the buffer and the status bar. Silence is what this engine already answers a
                // motion that cannot go anywhere with, and a find that misses is one of those. It
                // does stop a macro, though, which is how 'fz' inside one ends the run where vim's
                // would.
                if (find is { } request)
                {
                    if (VimFind.Target(_buffer, caret, request, count) is { } target)
                    {
                        SetCaret(target);
                    }
                    else
                    {
                        _macroFailed = true;
                    }
                }

                break;

            case VimMotion.SearchForward:
            case VimMotion.SearchBackward:
            case VimMotion.SearchNext:
            case VimMotion.SearchPrevious:
            case VimMotion.SearchWordForward:
            case VimMotion.SearchWordBackward:

                // A search that finds nothing has already said why, and left the caret alone.
                if (SearchTarget(command) is { } landing)
                {
                    SetCaret(landing);
                }

                break;

            case VimMotion.FirstLine:
                SetCaret(_buffer.GetLine(hasCount ? count : 1).FirstNonBlankOffset);
                break;

            case VimMotion.LastLine:
                SetCaret(_buffer.GetLine(hasCount ? count : _buffer.LineCount).FirstNonBlankOffset);
                break;

            // The three that read the view rather than the text: which line is at the top, in the
            // middle and at the bottom of the screen is the buffer's to say.
            case VimMotion.ScreenTop:
                SetCaret(_buffer.GetLine(VimViewport.TopLine(_buffer, count)).FirstNonBlankOffset);
                break;

            case VimMotion.ScreenMiddle:
                SetCaret(_buffer.GetLine(VimViewport.MiddleLine(_buffer)).FirstNonBlankOffset);
                break;

            case VimMotion.ScreenBottom:
                SetCaret(_buffer.GetLine(VimViewport.BottomLine(_buffer, count)).FirstNonBlankOffset);
                break;

            default:

                // VimMotion.None and VimMotion.WholeLine: neither is anywhere for a caret to go, and
                // the parser never produces either as a move.
                break;
        }

        _desiredColumn = NoDesiredColumn;
    }

    /// <summary>
    /// Runs an operator over what a motion or a text object covers.
    /// </summary>
    /// <param name="command">The command, whose noun is either a motion or a text object.</param>
    /// <param name="find">The find its motion stands for, when it is one of the six.</param>
    /// <remarks>
    /// The two nouns answer the same question in two ways — which text — so only the resolver
    /// differs and everything after it is shared: the empty-range rule, and the operators.
    /// </remarks>
    private void ApplyOperator(VimCommand command, VimFindRequest? find)
    {
        VimOperator @operator = command.Operator;
        int caret = _buffer.CaretOffset;
        int count = command.EffectiveCount;

        VimTextRange? resolved;

        if (IsSearch(command.Motion))
        {
            // Resolved here rather than in VimRanges, because where a search lands depends on state
            // only the engine has — the options, the last pattern — and on a compile that may fail
            // and has to say so.
            resolved = SearchTarget(command) is { } landing ? VimRanges.Search(_buffer, caret, landing) : null;
        }
        else
        {
            resolved = command.TextObject == VimTextObject.None
                ? VimRanges.Resolve(_buffer, caret, @operator, command.Motion, count, command.HasCount, find)
                : VimTextObjects.Resolve(_buffer, caret, command.TextObject, count);
        }

        if (resolved is not { } range)
        {
            // Behind an operator too, a find that misses is what stops a macro.
            _macroFailed |= find is not null;

            return;
        }

        // A characterwise range that covers nothing is a command with nothing to do — 'd0' at the
        // start of a line. A linewise one that covers nothing is an empty line, which very much has
        // something to do; a change enters insert mode either way; and a shift acts on the line the
        // range is on rather than on the range, so '>0' at the start of a line still shifts it.
        if (range.IsEmpty && !range.IsLinewise && !IsChangeOrShift(@operator))
        {
            return;
        }

        _desiredColumn = NoDesiredColumn;

        // A shift behind a motion is always by one width; the count went into the motion.
        Operate(@operator, range, shiftSteps: 1);
    }

    private static bool IsChangeOrShift(VimOperator @operator)
        => @operator is VimOperator.Change or VimOperator.ShiftRight or VimOperator.ShiftLeft;

    private static bool IsSearch(VimMotion motion)
        => motion is VimMotion.SearchForward
            or VimMotion.SearchBackward
            or VimMotion.SearchNext
            or VimMotion.SearchPrevious
            or VimMotion.SearchWordForward
            or VimMotion.SearchWordBackward;

    /// <summary>
    /// Runs an operator over a range that has already been resolved, from either grammar.
    /// </summary>
    /// <param name="operator">Which operator.</param>
    /// <param name="range">What it acts on.</param>
    /// <param name="shiftSteps">How many widths a shift moves the lines by: one behind a motion, the count over a selection.</param>
    private void Operate(VimOperator @operator, VimTextRange range, int shiftSteps)
    {
        switch (@operator)
        {
            case VimOperator.Yank:
                Yank(range);
                break;

            case VimOperator.Delete:
                Delete(range);
                break;

            case VimOperator.Change:
                Change(range);
                break;

            case VimOperator.ShiftRight:
                Shift(range, shiftSteps);
                break;

            case VimOperator.ShiftLeft:
                Shift(range, -shiftSteps);
                break;

            default:
                ChangeCase(range, @operator);
                break;
        }
    }

    /// <summary>
    /// Starts selecting, switches which way, or stops — <c>v</c> and <c>V</c>.
    /// </summary>
    /// <param name="mode">The visual mode the key names.</param>
    private void EnterVisual(VimMode mode)
    {
        if (Mode == mode)
        {
            // The key that starts a selection is the key that stops it: 'v' in charwise visual and
            // 'V' in linewise both leave, which is what makes them a toggle rather than a trap.
            LeaveVisual();

            return;
        }

        // Switching between the two keeps the anchor, so that 'vjV' selects both the lines the
        // charwise selection had reached into rather than starting again from the caret.
        if (!IsVisual)
        {
            _visualAnchor = _buffer.CaretOffset;
        }

        SetMode(mode);
    }

    /// <summary>
    /// Puts the engine back in normal mode with nothing selected.
    /// </summary>
    /// <remarks>
    /// Whichever way out it is — an operator, the visual key again, Esc — the selection is remembered
    /// on the way, so that <c>gv</c> has something to go back to. Remembered before the operator runs,
    /// which is what makes <c>gv</c> after a yank cover exactly what was yanked.
    /// </remarks>
    private void LeaveVisual()
    {
        if (IsVisual)
        {
            _lastVisual = CurrentSelection();
        }

        _visualAnchor = NoAnchor;

        _buffer.ClearSelection();
        SetMode(VimMode.Normal);
    }

    /// <summary>
    /// Hands the editor the selection the anchor and the caret describe between them.
    /// </summary>
    private void ShowSelection()
    {
        VimTextRange range = VisualRange();

        if (!range.IsLinewise)
        {
            _buffer.SetSelection(range.Start, range.End);

            return;
        }

        // The line break goes into the highlight, though not into the range an operator works on, so
        // that a linewise selection reads as whole lines rather than as text that happens to reach
        // both edges of them. The last line of the buffer has none to add.
        VimLine last = _buffer.GetLineByOffset(range.End);

        _buffer.SetSelection(range.Start, last.EndOffset + last.DelimiterLength);
    }

    private VimTextRange VisualRange() => VisualRange(Mode == VimMode.VisualLine);

    private VimTextRange VisualRange(bool linewise)
        => VimRanges.Selection(_buffer, _visualAnchor, _buffer.CaretOffset, linewise);

    /// <summary>
    /// Selects what a text object covers — <c>viw</c>, <c>va(</c> — and stays in the visual mode.
    /// </summary>
    /// <param name="textObject">The object that was typed.</param>
    /// <param name="count">How many times, never less than one.</param>
    /// <remarks>
    /// Both ends of the selection are moved, which is what makes an object in a visual mode useful:
    /// <c>vjjiw</c> is not "extend by a word", it is "select the word I am on". In linewise visual
    /// the two ends still land where the object does, and the selection is whole lines over them
    /// because that is what <see cref="VisualRange"/> already makes of any two offsets in <c>V</c>.
    /// An object that resolves to nothing leaves the selection exactly as it was, without a message.
    /// </remarks>
    private void SelectTextObject(VimTextObject textObject, int count)
    {
        if (VimTextObjects.Resolve(_buffer, _buffer.CaretOffset, textObject, count) is not { } range)
        {
            return;
        }

        _visualAnchor = range.Start;

        // On the last character the object covers rather than one past it: in a visual mode the caret
        // is inside the selection, and the range the operator gets is taken from where it sits. An
        // object that covers nothing puts both ends in the same place, which is what a bare 'v' does.
        SetCaret(Math.Max(range.Start, range.End - 1));

        _desiredColumn = NoDesiredColumn;
    }

    /// <summary>
    /// Runs an operator over the selection, and leaves visual mode.
    /// </summary>
    /// <param name="command">
    /// The command, whose operator is the whole of it and whose count a shift reads. A motion of
    /// <see cref="VimMotion.WholeLine"/> in place of <see cref="VimMotion.None"/> makes the selection
    /// linewise whatever the mode — the capitals <c>X</c> <c>D</c> <c>Y</c> <c>C</c> <c>S</c>.
    /// </param>
    private void ApplyVisualOperator(VimCommand command)
    {
        VimOperator @operator = command.Operator;
        VimTextRange range = VisualRange((Mode == VimMode.VisualLine) || (command.Motion == VimMotion.WholeLine));

        // Before the operator runs, not after: a change enters insert mode, and a highlight still on
        // screen over text that is being typed into is not what any of the three commands mean.
        LeaveVisual();

        // Every operator leaves the caret at the start of what was selected — vim's rule, and the
        // reason it is set here rather than left to the operator: the caret is at whichever end the
        // motions took it to, which for a selection made backwards is the start and for one made
        // forwards is the far end. Linewise, "the start" is the first non-blank of the first line,
        // which is where every other linewise command in this engine leaves it.
        SetCaret(range.IsLinewise ? _buffer.GetLineByOffset(range.Start).FirstNonBlankOffset : range.Start);

        _desiredColumn = NoDesiredColumn;

        // The one selection that covers nothing: 'v' on the last line of a buffer when it is empty,
        // where there is neither a character nor a line break to take. A change still enters insert
        // mode there, exactly as 'c0' at the start of a line does, and a shift still has a line.
        if (range.IsEmpty && !range.IsLinewise && !IsChangeOrShift(@operator))
        {
            return;
        }

        // Over a selection the count is the shift's own: '3>' moves the lines three widths.
        Operate(@operator, range, shiftSteps: command.EffectiveCount);
    }

    private void Yank(VimTextRange range)
    {
        Fill(range, yank: true);

        if (range.IsLinewise)
        {
            VimLine first = _buffer.GetLineByOffset(range.Start);

            // The caret only moves when the yank reached above it. 'yy' and 'yj' leave it exactly
            // where it was, which is what makes yanking a line a read-only act in every sense.
            if (first.Number < CurrentLine.Number)
            {
                SetCaret(first.FirstNonBlankOffset);
            }

            return;
        }

        SetCaret(Math.Min(_buffer.CaretOffset, range.Start));
    }

    private void Delete(VimTextRange range)
    {
        Fill(range, yank: false);

        if (!range.IsLinewise)
        {
            _buffer.Replace(range.Start, range.Length, string.Empty);
            SetCaret(range.Start);

            return;
        }

        int firstLine = _buffer.GetLineByOffset(range.Start).Number;
        (int start, int length) = LinewiseSpan(range);

        _buffer.Replace(start, length, string.Empty);

        // The line that took the deleted one's place, or the one above when the last line went.
        SetCaret(_buffer.GetLine(Math.Min(firstLine, _buffer.LineCount)).FirstNonBlankOffset);
    }

    private void Change(VimTextRange range)
    {
        // What comes out and what is typed in its place are one undo step. The group is the insert
        // session's own, opened here rather than by the command that enters insert mode, so that 'u'
        // after 'cc' restores the line and everything typed on it together.
        BeginInsertGroup();

        // A characterwise range that covers nothing leaves the register alone — 'c0' at the start of
        // a line changes nothing and takes nothing. An empty linewise one is an empty line, which is
        // a thing to have taken.
        if (range.IsLinewise || !range.IsEmpty)
        {
            Fill(range, yank: false);
        }

        if (!range.IsEmpty)
        {
            // Linewise, the text goes but the line stays: 'cc' empties a line rather than removing
            // it, which is the difference between it and 'dd' followed by 'O'.
            _buffer.Replace(range.Start, range.Length, string.Empty);
        }

        EnterInsertAt(range.Start);
    }

    /// <summary>
    /// Puts what a range covers into the register the command named — the unnamed one when it named
    /// none — with the rules <see cref="VimRegisters.Fill"/> spells out.
    /// </summary>
    private void Fill(VimTextRange range, bool yank)
        => Registers.Fill(_activeRegister, new VimRegister(_buffer.GetText(range.Start, range.Length), range.Kind), yank, _buffer.NewLine);

    /// <summary>
    /// Runs <c>&gt;</c> or <c>&lt;</c> over the lines a range touches.
    /// </summary>
    /// <param name="range">The range, whose lines are what matter; a shift is linewise whatever the motion was.</param>
    /// <param name="steps">How many widths, negative for a shift left.</param>
    /// <remarks>
    /// The register is left alone, as vim leaves it: nothing is taken out. The caret goes to the
    /// first non-blank of the first line, which is where <c>&gt;&gt;</c> leaves it in vim and the one
    /// place on the line that is still the same text after the indent has moved under it.
    /// </remarks>
    private void Shift(VimTextRange range, int steps)
    {
        int firstLine = _buffer.GetLineByOffset(range.Start).Number;
        int lastLine = _buffer.GetLineByOffset(range.End).Number;

        (int start, int length, string text) = VimShift.Shift(_buffer, firstLine, lastLine, steps, Options);

        // A shift that changes nothing — '<<' on lines with no indent — writes nothing, so it costs
        // no undo step and does not mark the document changed.
        if (text != _buffer.GetText(start, length))
        {
            _buffer.Replace(start, length, text);
        }

        SetCaret(_buffer.GetLine(firstLine).FirstNonBlankOffset);
    }

    /// <summary>
    /// Runs <c>gu</c>, <c>gU</c> or <c>g~</c> over a range.
    /// </summary>
    /// <remarks>
    /// The register is left alone, as it is for a shift. The caret goes to the start of the range,
    /// which for a linewise one is the first column of its first line — vim's own landing, and a
    /// different one from the first non-blank a delete ends on.
    /// </remarks>
    private void ChangeCase(VimTextRange range, VimOperator @operator)
    {
        if (!range.IsEmpty)
        {
            _buffer.Replace(range.Start, range.Length, VimCase.Convert(_buffer.GetText(range.Start, range.Length), @operator));
        }

        SetCaret(range.Start);
    }

    /// <summary>
    /// Runs <c>J</c> or <c>gJ</c>: the caret's line and the count-1 below it become one line.
    /// </summary>
    /// <param name="count">How many lines, read as two when fewer were asked for.</param>
    /// <param name="insertSpace">
    /// Whether this is <c>J</c>, which drops the joined line's leading blanks and puts one space at
    /// the seam, or <c>gJ</c>, which puts the lines together exactly as they are.
    /// </param>
    /// <remarks>
    /// <para>
    /// Vim's rules for the space, all of them: none when the joined line starts with <c>)</c>, none
    /// when the line so far already ends in a blank, and none when either side of the seam is empty
    /// — an empty line joined to text adds nothing but the join. Two spaces after a full stop is
    /// <c>joinspaces</c>, which is off.
    /// </para>
    /// <para>
    /// The caret lands on the seam of the last join: on the space it inserted, or where the joined
    /// text begins when it inserted none. On the last line of the buffer there is nothing to join
    /// and nothing happens, without a word; a count that reaches past the end joins what is there.
    /// One <see cref="IVimBuffer.Replace"/> for the whole run of lines, so <c>5J</c> is one undo.
    /// </para>
    /// </remarks>
    private void Join(int count, bool insertSpace)
    {
        VimLine first = CurrentLine;
        int lastNumber = Math.Min((first.Number + Math.Max(count, 2)) - 1, _buffer.LineCount);

        if (lastNumber == first.Number)
        {
            return;
        }

        StringBuilder joined = new(first.Text);
        int seam = 0;

        for (int number = first.Number + 1; number <= lastNumber; number++)
        {
            string next = _buffer.GetLine(number).Text;

            if (!insertSpace)
            {
                seam = joined.Length;
                joined.Append(next);

                continue;
            }

            string trimmed = next.TrimStart(' ', '\t');
            bool space = (trimmed.Length > 0)
                && (trimmed[0] != ')')
                && (joined.Length > 0)
                && (joined[^1] is not (' ' or '\t'));

            seam = joined.Length;

            if (space)
            {
                joined.Append(' ');
            }

            joined.Append(trimmed);
        }

        _buffer.Replace(first.Offset, _buffer.GetLine(lastNumber).EndOffset - first.Offset, joined.ToString());
        SetCaret(first.Offset + seam);
        _desiredColumn = NoDesiredColumn;
    }

    /// <summary>
    /// Runs <c>J</c> or <c>gJ</c> over the selection: the selected lines become one, two at the least.
    /// </summary>
    /// <param name="insertSpace">Whether this is <c>J</c> or <c>gJ</c>, as for <see cref="Join"/>.</param>
    /// <remarks>
    /// The same join as the normal-mode one, over the lines the selection touches rather than a
    /// count — a selection inside one line still joins that line to the next, which is vim's minimum.
    /// The caret lands on the seam of the last join, where <c>J</c> always leaves it, rather than at
    /// the start of the selection: the seam is what a join is about. On the last line there is
    /// nothing to join, and the caret goes to the start of what was selected, without a word.
    /// </remarks>
    private void JoinSelection(bool insertSpace)
    {
        VimTextRange range = VisualRange();
        int first = _buffer.GetLineByOffset(range.Start).Number;
        int last = _buffer.GetLineByOffset(range.End).Number;

        LeaveVisual();
        SetCaret(range.IsLinewise ? _buffer.GetLine(first).FirstNonBlankOffset : range.Start);
        _desiredColumn = NoDesiredColumn;

        Join(Math.Max((last - first) + 1, 2), insertSpace);
    }

    /// <summary>
    /// Runs <c>r</c> over the selection: every selected character becomes <paramref name="replacement"/>,
    /// the line breaks excepted.
    /// </summary>
    /// <remarks>
    /// Linewise, that is every character of every selected line, blanks included; an empty line has
    /// none and stays empty. One write for the whole selection, and the caret goes to the start of
    /// what was selected — for lines, the first column of the first, which is vim's landing and,
    /// after every character on it has become the same one, also its first non-blank.
    /// </remarks>
    private void FillSelection(char replacement)
    {
        VimTextRange range = VisualRange();

        LeaveVisual();

        if (!range.IsEmpty)
        {
            string text = _buffer.GetText(range.Start, range.Length);
            string filled = Overwrite(text, replacement);

            if (filled != text)
            {
                _buffer.Replace(range.Start, range.Length, filled);
            }
        }

        SetCaret(range.Start);
        _desiredColumn = NoDesiredColumn;
    }

    /// <summary>
    /// Runs <c>p</c> or <c>P</c> over the selection: what is selected goes, and the register goes in
    /// its place.
    /// </summary>
    /// <param name="count">How many copies of the register.</param>
    /// <param name="keepRegister">
    /// <see langword="true"/> for <c>P</c>, which leaves the register as it was; <c>p</c> leaves what
    /// it replaced there instead, so that <c>vp</c> twice over two words swaps them.
    /// </param>
    /// <remarks>
    /// <para>
    /// The register's kind and the selection's decide the shape, as vim's do. Characters into a
    /// characterwise selection go inline where the selection was. Lines into a characterwise
    /// selection split the line there and go in between the halves, on lines of their own — an empty
    /// half stays as an empty line, which is vim's answer too. Anything into a linewise selection goes
    /// in as lines where the lines were, so a word put over a line becomes a line.
    /// </para>
    /// <para>
    /// Two writes — the delete and the put — inside one undo group, so that <c>u</c> takes the whole
    /// replacement back at once. The caret lands where a put leaves it: on the last character of
    /// text put inline, on the first non-blank of the first line put as a line. With nothing in the
    /// register nothing goes either: the selection stays, and the status bar says why.
    /// </para>
    /// </remarks>
    private void ReplaceSelection(int count, bool keepRegister)
    {
        if (Registers.Get(_activeRegister) is not { } register)
        {
            _buffer.ShowStatus(NothingInRegister);

            return;
        }

        VimTextRange range = VisualRange();
        VimRegister? unnamed = Registers.Unnamed;

        LeaveVisual();
        _buffer.BeginUndoGroup();

        // What the selection held goes into the unnamed register whichever register the put was
        // named with — '"avp' puts a and leaves the old word where 'p' always leaves it — so the
        // delete inside runs without the name.
        char named = _activeRegister;
        _activeRegister = '\0';

        if (range.IsLinewise)
        {
            PutOverLines(range, register, count);
        }
        else
        {
            PutOverCharacters(range, register, count);
        }

        _activeRegister = named;
        _buffer.EndUndoGroup();
        _desiredColumn = NoDesiredColumn;

        if (keepRegister)
        {
            Registers.SetUnnamed(unnamed);
        }
    }

    private void PutOverCharacters(VimTextRange range, VimRegister register, int count)
    {
        // The one selection that covers nothing — 'v' on the empty last line — has nothing to take
        // out, and taking nothing out would still cost a write and fill the register with nothing.
        if (!range.IsEmpty)
        {
            Delete(range);
        }

        // Where the selection began, not where the delete left the caret: deleting the end of a line
        // pulls the caret back onto the character before, and the text belongs after that one.
        int at = range.Start;
        string newLine = _buffer.NewLine;

        if (register.Kind == VimRegisterKind.Linewise)
        {
            _buffer.Replace(at, 0, newLine + Repeat(register.Text, count, newLine) + newLine);
            SetCaret(_buffer.GetLineByOffset(at + newLine.Length).FirstNonBlankOffset);

            return;
        }

        string text = Repeat(register.Text, count, string.Empty);

        _buffer.Replace(at, 0, text);
        SetCaret(Math.Max(at, (at + text.Length) - 1));
    }

    private void PutOverLines(VimTextRange range, VimRegister register, int count)
    {
        VimLine last = _buffer.GetLineByOffset(range.End);
        bool tail = last.DelimiterLength == 0;
        bool wholeBuffer = tail && (_buffer.GetLineByOffset(range.Start).Number == 1);

        Delete(range);

        // Lines that were the whole buffer leave the one empty line a buffer always has, and the put
        // takes its place rather than going above it — otherwise 'Vp' over a one-line file would
        // leave an empty line under the text. Vim removes that line too.
        if (wholeBuffer)
        {
            _buffer.Replace(0, 0, Repeat(register.Text, count, _buffer.NewLine));
            SetCaret(_buffer.GetLine(1).FirstNonBlankOffset);

            return;
        }

        // The delete left the caret on the line that took the selection's place — or, when the
        // selection reached the end of the buffer, on the line above where it was — so the lines go
        // above the caret's line in the first case and below it in the second. Whatever the register
        // holds goes in as lines here; that is what putting over whole lines means.
        PutLines(register.Text, count, after: tail);
    }

    /// <summary>
    /// Runs <c>o</c> in a visual mode: the caret goes to the anchor and the anchor to where the caret
    /// was, so that the motions move the other end of the selection.
    /// </summary>
    private void SwapVisualEnds()
    {
        int anchor = _visualAnchor;

        _visualAnchor = _buffer.CaretOffset;
        SetCaret(anchor);
        _desiredColumn = NoDesiredColumn;
    }

    /// <summary>
    /// Runs <c>gv</c>: in normal mode, selects again what was last selected, in the mode it was
    /// selected in; in a visual mode, swaps the selection on screen with the one before it, so that a
    /// second <c>gv</c> comes back.
    /// </summary>
    /// <remarks>
    /// An end whose line or column has since gone is pulled onto the nearest that is still there,
    /// which is as much as "the same selection" can mean over text that has changed. With nothing to
    /// go back to, nothing happens, without a word.
    /// </remarks>
    private void ReselectVisual()
    {
        if (_lastVisual is not { } previous)
        {
            return;
        }

        if (IsVisual)
        {
            _lastVisual = CurrentSelection();
        }

        _visualAnchor = OffsetOf(previous.AnchorLine, previous.AnchorColumn);
        SetCaret(OffsetOf(previous.CaretLine, previous.CaretColumn));
        SetMode(previous.Mode);
        _desiredColumn = NoDesiredColumn;
    }

    private RememberedSelection CurrentSelection()
    {
        VimLine anchorLine = _buffer.GetLineByOffset(_visualAnchor);
        VimLine caretLine = CurrentLine;

        return new RememberedSelection(
            anchorLine.Number,
            _visualAnchor - anchorLine.Offset,
            caretLine.Number,
            _buffer.CaretOffset - caretLine.Offset,
            Mode);
    }

    /// <summary>
    /// The offset of a line and column, pulled onto the nearest line and character the buffer still has.
    /// </summary>
    private int OffsetOf(int lineNumber, int column)
    {
        VimLine line = _buffer.GetLine(Math.Clamp(lineNumber, 1, _buffer.LineCount));

        return Math.Min(line.Offset + column, line.LastCaretOffset);
    }

    private static string Overwrite(string text, char replacement)
    {
        char[] characters = text.ToCharArray();

        for (int index = 0; index < characters.Length; index++)
        {
            if (characters[index] is not ('\r' or '\n'))
            {
                characters[index] = replacement;
            }
        }

        return new string(characters);
    }

    /// <summary>
    /// Widens a linewise range to the text a deletion has to remove: the lines and one terminator.
    /// </summary>
    /// <remarks>
    /// Which terminator is the whole question. Normally it is the one after the last line, so the
    /// following line moves up. For the last line of the buffer there is none, so it is the one
    /// <em>before</em> the first — otherwise <c>dd</c> on the last line would leave an empty line
    /// where the text was instead of removing it.
    /// </remarks>
    private (int Start, int Length) LinewiseSpan(VimTextRange range)
    {
        VimLine first = _buffer.GetLineByOffset(range.Start);
        VimLine last = _buffer.GetLineByOffset(range.End);

        if (last.DelimiterLength > 0)
        {
            return (first.Offset, (last.EndOffset + last.DelimiterLength) - first.Offset);
        }

        if (first.Number > 1)
        {
            int previousEnd = _buffer.GetLine(first.Number - 1).EndOffset;

            return (previousEnd, last.EndOffset - previousEnd);
        }

        // The whole buffer. What is left is one empty line, because a buffer always has one.
        return (first.Offset, last.EndOffset - first.Offset);
    }

    private void Put(int count, bool after)
    {
        if (Registers.Get(_activeRegister) is not { } register)
        {
            _buffer.ShowStatus(NothingInRegister);

            return;
        }

        if (register.Kind == VimRegisterKind.Linewise)
        {
            PutLines(register.Text, count, after);

            return;
        }

        string text = Repeat(register.Text, count, string.Empty);

        if (text.Length == 0)
        {
            return;
        }

        // After the caret, not after the line: on an empty line there is nowhere past the caret to
        // go, and the end of the line is where the text belongs.
        int at = after ? Math.Min(_buffer.CaretOffset + 1, CurrentLine.EndOffset) : _buffer.CaretOffset;

        _buffer.Replace(at, 0, text);
        SetCaret((at + text.Length) - 1);
        _desiredColumn = NoDesiredColumn;
    }

    private void PutLines(string text, int count, bool after)
    {
        string newLine = _buffer.NewLine;
        string chunk = Repeat(text, count, newLine);
        VimLine line = CurrentLine;

        int landed;

        if (!after)
        {
            landed = line.Offset;
            _buffer.Replace(landed, 0, chunk + newLine);
        }
        else if (line.DelimiterLength > 0)
        {
            landed = line.EndOffset + line.DelimiterLength;
            _buffer.Replace(landed, 0, chunk + newLine);
        }
        else
        {
            // The last line of the buffer has no terminator to put the text after, so the put brings
            // one of its own rather than one at the end that would leave a trailing empty line.
            _buffer.Replace(line.EndOffset, 0, newLine + chunk);
            landed = line.EndOffset + newLine.Length;
        }

        SetCaret(_buffer.GetLineByOffset(landed).FirstNonBlankOffset);
        _desiredColumn = NoDesiredColumn;
    }

    private void MoveToLine(int lineNumber)
    {
        if (_desiredColumn == NoDesiredColumn)
        {
            _desiredColumn = _buffer.CaretOffset - CurrentLine.Offset;
        }

        // Clamped rather than refused: a count that overshoots the document lands on the last line,
        // and the column is kept either way, so holding j against the bottom and coming back up
        // returns to the column the run started in.
        VimLine target = _buffer.GetLine(Math.Clamp(lineNumber, 1, _buffer.LineCount));

        SetCaret(target.IsEmpty
            ? target.Offset
            : target.Offset + Math.Min(_desiredColumn, target.Length - 1));
    }

    /// <summary>
    /// Runs <c>Ctrl+D</c> or <c>Ctrl+U</c>: the view and the caret both move half a screen, or the
    /// count's lines.
    /// </summary>
    /// <param name="command">The command, whose count — when typed — is how many lines to scroll.</param>
    /// <param name="down">Which way.</param>
    /// <remarks>
    /// The view stops where the buffer's last line reaches the bottom of the screen, as vim's does,
    /// and the caret carries on alone to the end; on the last line already — or the first, going up
    /// — nothing happens, without a word where vim beeps. The caret keeps its column through the
    /// move, exactly as it does through <c>j</c> and <c>k</c>, which is what makes <c>Ctrl+D</c> then
    /// <c>Ctrl+U</c> come back to where it started.
    /// </remarks>
    private void ScrollHalfPage(VimCommand command, bool down)
    {
        VimLine line = CurrentLine;

        if (down ? (line.Number >= _buffer.LineCount) : (line.Number <= 1))
        {
            return;
        }

        int lines = command.HasCount ? command.Count : VimViewport.HalfPage(_buffer);
        int first = _buffer.FirstVisibleLine;
        int target = down
            ? Math.Min(first + lines, Math.Max(VimViewport.LastTopLineWithoutGap(_buffer), first))
            : Math.Max(first - lines, 1);

        if (target != first)
        {
            _buffer.ScrollToLine(target, VimScrollAlignment.Top);
        }

        MoveToLine(down ? line.Number + lines : line.Number - lines);
    }

    /// <summary>
    /// Runs <c>Ctrl+F</c> or <c>Ctrl+B</c>: the view moves <paramref name="count"/> pages, a page
    /// being the screen less two lines of overlap, and the caret goes to the first non-blank of the new
    /// top line going down or of the new bottom line going up.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Going down, the view may end with the buffer's last line alone at the top, as vim's does; with
    /// it there already there is nowhere to go and nothing happens. Going up, the view stops at the
    /// first line. Both are silent, where vim beeps.
    /// </para>
    /// <para>
    /// The view is aligned on the edge the caret lands on — the new top line at the top going down,
    /// the new bottom line at the bottom going up — so that the caret's line is exactly on screen
    /// whatever the editor's line heights turn out to be, and the other edge is what the count of
    /// lines makes it.
    /// </para>
    /// </remarks>
    private void ScrollPage(int count, bool down)
    {
        int first = _buffer.FirstVisibleLine;
        int lines = VimViewport.Page(_buffer) * count;

        if (down ? (first >= _buffer.LineCount) : (first <= 1))
        {
            return;
        }

        if (down)
        {
            int top = Math.Min(first + lines, _buffer.LineCount);

            _buffer.ScrollToLine(top, VimScrollAlignment.Top);
            SetCaret(_buffer.GetLine(top).FirstNonBlankOffset);
        }
        else
        {
            int top = Math.Max(first - lines, 1);
            int bottom = Math.Min((top + _buffer.VisibleLineCount) - 1, _buffer.LineCount);

            _buffer.ScrollToLine(bottom, VimScrollAlignment.Bottom);
            SetCaret(_buffer.GetLine(bottom).FirstNonBlankOffset);
        }

        _desiredColumn = NoDesiredColumn;
    }

    /// <summary>
    /// Runs <c>Ctrl+E</c> or <c>Ctrl+Y</c>: the view moves <paramref name="count"/> lines and the
    /// caret stays — unless the move would leave it off screen, in which case it is pulled onto the
    /// nearest line still shown, keeping its column.
    /// </summary>
    /// <remarks>
    /// Where the caret ends up is worked out from where the view was asked to go rather than read back
    /// from the editor, so that the answer is the same over a view that could not go that far. A
    /// caret pulled onto the bottom line has the view aligned on that line rather than on the new top,
    /// for the same reason a page up does: the line the caret is on is then exactly on screen, and
    /// the top is what the count of lines makes it.
    /// </remarks>
    private void ScrollLines(int count, bool down)
    {
        int first = _buffer.FirstVisibleLine;
        int target = down ? Math.Min(first + count, _buffer.LineCount) : Math.Max(first - count, 1);

        if (target == first)
        {
            return;
        }

        int last = Math.Min((target + _buffer.VisibleLineCount) - 1, _buffer.LineCount);
        int line = CurrentLine.Number;

        if (line > last)
        {
            _buffer.ScrollToLine(last, VimScrollAlignment.Bottom);
            MoveToLine(last);

            return;
        }

        _buffer.ScrollToLine(target, VimScrollAlignment.Top);

        if (line < target)
        {
            MoveToLine(target);
        }
    }

    /// <summary>
    /// Runs one of the six <c>z</c> commands: a line goes to the top, the middle or the bottom of the
    /// screen — the caret's, or the one the count names.
    /// </summary>
    /// <param name="command">The command, whose count — when typed — is the line to place.</param>
    /// <param name="alignment">Where on the screen the line goes.</param>
    /// <param name="firstNonBlank">
    /// Whether this is the <c>z</c> and <c>Enter</c> family, which moves the caret to the line's first
    /// non-blank, or the <c>zt</c> family, which leaves the caret where it is — in the same column,
    /// when a count has moved it to another line.
    /// </param>
    private void ScrollCaret(VimCommand command, VimScrollAlignment alignment, bool firstNonBlank)
    {
        VimLine line = command.HasCount ? _buffer.GetLine(Math.Clamp(command.Count, 1, _buffer.LineCount)) : CurrentLine;

        _buffer.ScrollToLine(line.Number, alignment);

        if (firstNonBlank)
        {
            SetCaret(line.FirstNonBlankOffset);
            _desiredColumn = NoDesiredColumn;
        }
        else if (command.HasCount)
        {
            MoveToLine(line.Number);
        }
    }

    private void EnterInsertAt(int offset)
    {
        // Not SetCaret: insert mode is where the caret is allowed past the last character, which is
        // the whole difference between appending at the end of a line and inserting on it.
        _buffer.CaretOffset = Math.Clamp(offset, 0, _buffer.TextLength);

        BeginInsertGroup();
        _typed.Begin(_buffer.CaretOffset);
        SetMode(VimMode.Insert);
    }

    private void OpenLine(bool below)
    {
        VimLine line = CurrentLine;

        // The markdown item this line is, carried onto the new one — the same rule Enter follows in
        // insert mode, and the reason it lives in ListContinuation rather than here. Empty for a line
        // that is not an item, which leaves 'o' and 'O' doing what they always did.
        string prefix = below
            ? ListContinuation.OpenBelow(line.Text)
            : ListContinuation.OpenAbove(line.Text);

        // Opened inside the insert session's undo group, so the new line and everything typed on it
        // come back off in one undo rather than leaving an empty line behind.
        BeginInsertGroup();

        if (below)
        {
            _buffer.Replace(line.EndOffset, 0, _buffer.NewLine + prefix);
            _buffer.CaretOffset = line.EndOffset + _buffer.NewLine.Length + prefix.Length;
        }
        else
        {
            _buffer.Replace(line.Offset, 0, prefix + _buffer.NewLine);
            _buffer.CaretOffset = line.Offset + prefix.Length;
        }

        // After the line has been opened, so that the opening is the command's and not the typing's —
        // the marker included: it is written by the command, so '.' and a macro re-derive it rather
        // than replaying it as text the user typed.
        _typed.Begin(_buffer.CaretOffset);
        SetMode(VimMode.Insert);
    }

    private void DeleteCharacters(int count)
    {
        VimLine line = CurrentLine;
        int caret = _buffer.CaretOffset;
        int length = Math.Min(count, line.EndOffset - caret);

        if (length <= 0)
        {
            // Nothing under the caret. Vim does nothing here, and in particular does not pull the
            // line below onto this one.
            return;
        }

        Registers.Fill(_activeRegister, VimRegister.Characters(_buffer.GetText(caret, length)), yank: false, _buffer.NewLine);
        _buffer.Replace(caret, length, string.Empty);

        // Re-clamped rather than left where it was: deleting the last character of a line takes the
        // position the caret is standing on away with it.
        SetCaret(caret);
        _desiredColumn = NoDesiredColumn;
    }

    private void ReplaceCharacters(char replacement, int count)
    {
        int caret = _buffer.CaretOffset;

        // Vim refuses rather than doing as much as it can: '3rx' with two characters left on the line
        // leaves the line alone, so that a mistyped count cannot half-destroy a word.
        if ((CurrentLine.EndOffset - caret) < count)
        {
            return;
        }

        _buffer.Replace(caret, count, new string(replacement, count));
        SetCaret((caret + count) - 1);
        _desiredColumn = NoDesiredColumn;
    }

    private void ToggleCase(int count)
    {
        int caret = _buffer.CaretOffset;
        int length = Math.Min(count, CurrentLine.EndOffset - caret);

        if (length <= 0)
        {
            return;
        }

        _buffer.Replace(caret, length, VimCase.Convert(_buffer.GetText(caret, length), VimOperator.ToggleCase));

        // Past what it swapped, clamped by normal mode's rule — so '~' held down walks along the line
        // and then stops on its last character.
        SetCaret(caret + length);
        _desiredColumn = NoDesiredColumn;
    }

    /// <summary>
    /// Opens the command line — <c>:</c>, <c>/</c> or <c>?</c>.
    /// </summary>
    /// <param name="opener">
    /// The command that opened it. Its argument is the key, which is also the line's first character;
    /// its operator, count and register are what a search typed on the line will run with.
    /// </param>
    private void OpenCommandLine(VimCommand opener)
    {
        _commandLineOpener = opener;
        _commandLine = new StringBuilder().Append(opener.Argument);
        _buffer.ShowCommandLine(_commandLine.ToString());
    }

    private void CloseCommandLine()
    {
        if (_commandLine is null)
        {
            return;
        }

        _commandLine = null;
        _buffer.ShowCommandLine(null);
    }

    /// <summary>
    /// Reads one keystroke into the open command line.
    /// </summary>
    /// <param name="key">What was pressed.</param>
    /// <returns>The same answer <see cref="HandleKey"/> gives: whether the engine took the key.</returns>
    /// <remarks>
    /// Typing, Backspace, Enter and Esc, and nothing else. The arrows do not move along the line and
    /// there is no history to page through — both are vim's, both are more command line than this one
    /// needs, and both would have to be typed blind into a strip that shows no cursor of its own.
    /// </remarks>
    private bool HandleCommandLineKey(VimKey key)
    {
        StringBuilder line = _commandLine!;

        // Ctrl and Alt still belong to the window, exactly as they do in normal mode. Ctrl+S while a
        // command line is open saves the file, which is what the key is for; the line stays up.
        if (key.HasCommandModifier)
        {
            return false;
        }

        switch (key.Special)
        {
            case VimSpecialKey.Escape:
                CloseCommandLine();

                return true;

            case VimSpecialKey.Enter:
                string typed = line.ToString();
                VimCommand opener = _commandLineOpener;
                CloseCommandLine();
                RunCommandLine(typed, opener);

                return true;

            case VimSpecialKey.Backspace:

                // Rubbing out the ':' itself closes the line, which is vim's answer and the only one
                // that leaves no way to be stuck in a command line with nothing in it.
                line.Remove(line.Length - 1, 1);

                if (line.Length == 0)
                {
                    CloseCommandLine();
                }
                else
                {
                    _buffer.ShowCommandLine(line.ToString());
                }

                return true;

            case VimSpecialKey.None when !char.IsControl(key.Character):
                line.Append(key.Character);
                _buffer.ShowCommandLine(line.ToString());

                return true;

            default:

                // An arrow, a function key, or a control character some platform reported as text.
                // Swallowed rather than let through: the document is not what is being typed into.
                return true;
        }
    }

    /// <summary>
    /// Runs a finished command line.
    /// </summary>
    /// <param name="line">The whole line, including the <c>:</c>, <c>/</c> or <c>?</c> it opened with.</param>
    /// <param name="opener">
    /// The command that opened it, for the operator, count and register a search carries with it.
    /// </param>
    /// <remarks>
    /// A search becomes the motion it always was and goes through <see cref="Execute"/> like any other
    /// command — a move on its own, an operator's noun when one was waiting, and in a visual mode the
    /// far end of the selection — so that everything a motion gets for free, from the highlight to
    /// <c>.</c> remembering <c>d/foo</c>, holds for it too. An ex line is run here.
    /// </remarks>
    private void RunCommandLine(string line, VimCommand opener)
    {
        char prefix = line[0];

        if (prefix is '/' or '?')
        {
            string pattern = line[1..];

            Execute(new VimCommand(
                opener.Operator == VimOperator.None ? VimAction.Move : VimAction.Operate,
                prefix == '/' ? VimMotion.SearchForward : VimMotion.SearchBackward,
                opener.Count,
                opener.Operator,
                Register: opener.Register,
                Pattern: pattern.Length > 0 ? pattern : null));

            return;
        }

        string command = line[1..].Trim();

        // ':' and Enter. Vim runs the line the caret is on; here it is a typo, and doing nothing is
        // the answer that cannot surprise anyone.
        if (command.Length == 0)
        {
            return;
        }

        RunEx(command);
    }

    /// <summary>
    /// Runs an ex line: a jump, a substitute, a <c>:set</c>, or one of the four requests that leave
    /// the engine.
    /// </summary>
    /// <remarks>
    /// The parser says what was typed and this says what it means, so that "Not an editor command" is
    /// one answer from one place — for a name the engine does not know, and for a shape it does not
    /// read. The file requests are the only ones that leave: everything else here is about the text,
    /// and the text is the engine's.
    /// </remarks>
    private void RunEx(string command)
    {
        if (ExCommandParser.ParseLine(command) is not { } line)
        {
            Fail($"Not an editor command: {command}");

            return;
        }

        switch (line.Name)
        {
            case "" when line.Range is { } range:
                GoToLine(range);
                break;

            case "s":
            case "substitute":
                Substitute(line, command);
                break;

            case "se":
            case "set":
                Set(line);
                break;

            default:
                if (ExCommandParser.FileCommand(line) is { } request)
                {
                    ExCommandRequested?.Invoke(this, request);
                }
                else
                {
                    Fail($"Not an editor command: {command}");
                }

                break;
        }
    }

    /// <summary>
    /// Runs a range typed on its own — <c>:42</c>, <c>:$</c>, <c>:'&gt;</c> — a jump to the last line
    /// it names, at its first non-blank.
    /// </summary>
    private void GoToLine(VimExRange range)
    {
        if (ResolveRange(range) is not { } lines)
        {
            return;
        }

        SetCaret(_buffer.GetLine(lines.Last).FirstNonBlankOffset);
        _desiredColumn = NoDesiredColumn;
        _buffer.ScrollToCaret();
    }

    /// <summary>
    /// Runs <c>:s</c>.
    /// </summary>
    /// <param name="line">The parsed line.</param>
    /// <param name="command">The line as typed, for the message when it is not a substitute after all.</param>
    /// <remarks>
    /// The pattern becomes the last search pattern, as it does in vim — <c>n</c> after
    /// <c>:s/foo/bar/</c> finds the next <c>foo</c> — and an empty pattern reads it, so <c>/foo</c>
    /// then <c>:s//bar/</c> substitutes what was just searched for. It is remembered once it has
    /// compiled and before it is run, so a substitute that finds nothing still leaves its pattern
    /// behind. One write for the whole range and one undo step; the caret to the first non-blank of
    /// the last line a substitution touched, which is vim's landing.
    /// </remarks>
    private void Substitute(VimExLine line, string command)
    {
        if (!VimSubstitute.TryParse(line.Argument, out VimSubstituteRequest request, out string? trailing))
        {
            Fail(trailing ?? $"Not an editor command: {command}");

            return;
        }

        string? pattern = request.Pattern.Length > 0 ? request.Pattern : _lastSearch?.Pattern;

        if (pattern is null)
        {
            Fail(NoPreviousSearch);

            return;
        }

        (int First, int Last) lines;

        if (line.Range is { } range)
        {
            if (ResolveRange(range) is not { } resolved)
            {
                return;
            }

            lines = resolved;
        }
        else
        {
            int current = CurrentLine.Number;
            lines = (current, current);
        }

        if (!VimPattern.TryCompile(pattern, Options, smartCase: true, request.IgnoreCase, out Regex? regex, out string? error))
        {
            Fail($"Invalid pattern: {error}");

            return;
        }

        // The direction is left as it was: ':s' has none of its own, and 'n' afterwards goes on the
        // way the last '/' or '?' went.
        _lastSearch = new LastSearch(pattern, _lastSearch?.Forward ?? true, SmartCase: true);

        VimSubstitution? result;

        try
        {
            result = VimSubstitute.Apply(_buffer, regex, request.Replacement, request.Global, lines.First, lines.Last);
        }
        catch (RegexMatchTimeoutException)
        {
            Fail($"Invalid pattern: {VimPattern.TimedOut}");

            return;
        }

        if (result is not { } substitution)
        {
            Fail($"Pattern not found: {pattern}");

            return;
        }

        _buffer.Replace(substitution.Offset, substitution.Length, substitution.Text);
        SetCaret(_buffer.GetLineByOffset(substitution.Offset + substitution.Caret).FirstNonBlankOffset);
        _desiredColumn = NoDesiredColumn;
        _buffer.ShowStatus(substitution.Message);
        _buffer.ScrollToCaret();
    }

    /// <summary>
    /// Runs <c>:set</c>.
    /// </summary>
    private void Set(VimExLine line)
    {
        // Vim's answer to ':3set': an option is not a thing lines have.
        if (line.Range is not null)
        {
            Fail(NoRangeAllowed);

            return;
        }

        if (!VimSet.TryApply(line.Argument, Options, out string? message))
        {
            Fail(message);

            return;
        }

        if (message is not null)
        {
            _buffer.ShowStatus(message);
        }
    }

    /// <summary>
    /// Turns a range into line numbers the buffer has: <c>.</c> and <c>$</c> read now, the marks read
    /// off the last selection, the offsets added, both ends clamped, and the two put in order.
    /// </summary>
    /// <returns>
    /// The lines, or <see langword="null"/> — with the reason shown — when a mark names a selection
    /// that has not been made.
    /// </returns>
    private (int First, int Last)? ResolveRange(VimExRange range)
    {
        if ((ResolveAddress(range.Start) is not { } one) || (ResolveAddress(range.End) is not { } other))
        {
            return null;
        }

        int first = Math.Clamp(one, 1, _buffer.LineCount);
        int last = Math.Clamp(other, 1, _buffer.LineCount);

        // Vim asks whether to swap a backwards range. There is no way to ask here, and the lines
        // meant are the same either way.
        return (Math.Min(first, last), Math.Max(first, last));
    }

    private int? ResolveAddress(VimExAddress address)
    {
        int line;

        switch (address.Kind)
        {
            case VimExAddressKind.Line:
                line = address.Line;
                break;

            case VimExAddressKind.Current:
                line = CurrentLine.Number;
                break;

            case VimExAddressKind.Last:
                line = _buffer.LineCount;
                break;

            default:

                // '< and '>: the last selection's first and last lines, whichever end was the anchor.
                if (_lastVisual is not { } selection)
                {
                    Fail(MarkNotSet);

                    return null;
                }

                line = address.Kind == VimExAddressKind.VisualStart
                    ? Math.Min(selection.AnchorLine, selection.CaretLine)
                    : Math.Max(selection.AnchorLine, selection.CaretLine);
                break;
        }

        return line + address.Offset;
    }

    /// <summary>
    /// Works out where a search motion lands, and remembers what it looked for.
    /// </summary>
    /// <param name="command">The command, whose motion is one of the six searches.</param>
    /// <returns>
    /// The offset of the match, or <see langword="null"/> when there is none — with the reason already
    /// on the status bar, and the macro stopped.
    /// </returns>
    /// <remarks>
    /// The one place the memory is written. <c>/</c>, <c>?</c>, <c>*</c> and <c>#</c> write it — the
    /// pattern, and the way they went; <c>n</c> reads it as it is and <c>N</c> reads it the other way
    /// round without turning it, so that <c>N</c> then <c>n</c> still goes the original way, exactly
    /// as <c>,</c> and <c>;</c> do for a find. A <c>/</c> with nothing typed after it reads the pattern
    /// back and writes only the direction, which is vim's <c>/</c> and Enter. <c>*</c> and <c>#</c>
    /// start from the word's first character rather than the caret's, so that the word under the caret
    /// is the one skipped, and they mark the pattern as one <c>smartcase</c> must leave alone, because
    /// a capital in a word taken off the page was not typed. A pattern that does not compile is not
    /// remembered: there is nothing for <c>n</c> to do with it.
    /// </remarks>
    private int? SearchTarget(VimCommand command)
    {
        int from = _buffer.CaretOffset;
        LastSearch search;
        bool remember;

        switch (command.Motion)
        {
            case VimMotion.SearchForward:
            case VimMotion.SearchBackward:
                bool forward = command.Motion == VimMotion.SearchForward;

                if (command.Pattern is { } typed)
                {
                    search = new LastSearch(typed, forward, SmartCase: true);
                }
                else if (_lastSearch is { } previous)
                {
                    search = previous with { Forward = forward };
                }
                else
                {
                    Fail(NoPreviousSearch);

                    return null;
                }

                remember = true;
                break;

            case VimMotion.SearchNext:
            case VimMotion.SearchPrevious:
                if (_lastSearch is not { } last)
                {
                    Fail(NoPreviousSearch);

                    return null;
                }

                search = command.Motion == VimMotion.SearchNext ? last : last with { Forward = !last.Forward };
                remember = false;
                break;

            case VimMotion.SearchWordForward:
            case VimMotion.SearchWordBackward:
                if (VimSearch.WordUnderCaret(_buffer, from) is not { } word)
                {
                    // Nothing on the line to look for. Silent, as a motion that cannot go anywhere is
                    // — and, like a find that misses, the end of any macro it was part of.
                    _macroFailed = true;

                    return null;
                }

                search = new LastSearch(word.Pattern, command.Motion == VimMotion.SearchWordForward, SmartCase: false);
                from = word.Start;
                remember = true;
                break;

            default:
                return null;
        }

        if (!VimPattern.TryCompile(search.Pattern, Options, search.SmartCase, ignoreCase: null, out Regex? regex, out string? error))
        {
            Fail($"Invalid pattern: {error}");

            return null;
        }

        if (remember)
        {
            _lastSearch = search;
        }

        return Search(regex, search.Pattern, from, search.Forward, command.EffectiveCount);
    }

    /// <summary>
    /// Walks <paramref name="count"/> matches along from <paramref name="from"/>, or says why it
    /// could not.
    /// </summary>
    /// <remarks>
    /// A count walks match by match rather than jumping, because each step has to start from where the
    /// last one landed for the wrap to be right. Nothing is moved here: a <c>3n</c> that runs out of
    /// matches leaves the caret where it was rather than two thirds of the way there, and the caller
    /// puts the caret — or the operator's range — where the answer says. With <c>wrapscan</c> off, the
    /// step that would have gone round the end is the step that fails, and the message says which end
    /// it hit; with it on, the message says the search went round.
    /// </remarks>
    private int? Search(Regex regex, string pattern, int from, bool forward, int count)
    {
        int at = from;
        bool wrapped = false;

        try
        {
            for (int index = 0; index < count; index++)
            {
                if (VimSearch.Find(_buffer, regex, at, forward) is not { } hit)
                {
                    Fail($"Pattern not found: {pattern}");

                    return null;
                }

                if (hit.Wrapped && !Options.WrapScan)
                {
                    Fail((forward ? HitBottomNoWrap : HitTopNoWrap) + pattern);

                    return null;
                }

                at = hit.Offset;
                wrapped |= hit.Wrapped;
            }
        }
        catch (RegexMatchTimeoutException)
        {
            Fail($"Invalid pattern: {VimPattern.TimedOut}");

            return null;
        }

        if (wrapped)
        {
            _buffer.ShowStatus(forward ? HitBottom : HitTop);
        }

        return at;
    }

    private void Rewind(int count, bool forward)
    {
        for (int index = 0; index < count; index++)
        {
            if (forward)
            {
                _buffer.Redo();
            }
            else
            {
                _buffer.Undo();
            }
        }

        _desiredColumn = NoDesiredColumn;

        // The undo stack puts the caret back where the change was, which is a position insert mode
        // allowed and normal mode may not.
        SetCaret(_buffer.CaretOffset);
    }

    private void LeaveInsert()
    {
        // Before the group closes: the extra copies a count asked for are part of the same step.
        CloseInsertRecord();
        EndInsertGroup();

        int caret = _buffer.CaretOffset;
        VimLine line = _buffer.GetLineByOffset(caret);

        // Vim's own step back: Esc leaves the caret on the character that was just typed rather than
        // past it, so that the next command acts on what you were looking at.
        SetCaret(caret > line.Offset ? caret - 1 : caret);

        _desiredColumn = NoDesiredColumn;
        _parser.Reset();

        SetMode(VimMode.Normal);
    }

    private void BeginInsertGroup()
    {
        if (_insertGroupOpen)
        {
            return;
        }

        _buffer.BeginUndoGroup();
        _insertGroupOpen = true;
    }

    private void EndInsertGroup()
    {
        if (!_insertGroupOpen)
        {
            return;
        }

        _insertGroupOpen = false;
        _buffer.EndUndoGroup();
    }

    private void NotACommand(string sequence) => Fail($"Not a command: {sequence}");

    /// <summary>
    /// Says why a command did nothing, and stops any macro it was part of.
    /// </summary>
    private void Fail(string message)
    {
        _buffer.ShowStatus(message);
        _macroFailed = true;
    }

    /// <summary>
    /// Takes the message off the status bar — or, while a recording is open, puts
    /// <c>recording @a</c> there, which is what the slot shows whenever nothing else needs it.
    /// </summary>
    private void ClearStatus()
        => _buffer.ShowStatus(_recording is { } register ? $"recording @{register}" : null);

    private void OnBufferChanged(object? sender, VimTextChange change) => _typed.Observe(change);

    /// <summary>
    /// Closes the record of what the insert session typed: it becomes the last change, goes into an
    /// open recording, and is typed again for every extra count the insert command carried.
    /// </summary>
    /// <remarks>
    /// The copies come last and after the record has closed, so that they are not recorded as typing
    /// themselves — <c>3ix</c> is remembered as <c>3i</c> and <c>x</c>, not as <c>xxx</c>. Only the six
    /// insert commands multiply their text; a count on a change went into its motion.
    /// </remarks>
    private void CloseInsertRecord()
    {
        string text = _typed.Text;
        _typed.Close();

        if (_insertCommand is not { } command)
        {
            return;
        }

        _insertCommand = null;

        if (!_replaying)
        {
            _lastChange = new RecordedChange(command, text, _insertVisual);

            if ((_recording is not null) && (_macroDepth == 0))
            {
                _macro.Append(VimKeyNotation.Format(TypedKeys(text)));
            }
        }

        _insertVisual = null;

        if (command.EffectiveCount > 1)
        {
            RepeatTyped(command, text, command.EffectiveCount - 1);
        }
    }

    /// <summary>
    /// Types the text an insert session typed again, <paramref name="copies"/> more times, the way
    /// vim replays the command for each count: an open line is opened again below or above, an insert
    /// carries straight on from where the caret is.
    /// </summary>
    private void RepeatTyped(VimCommand command, string text, int copies)
    {
        string newLine = _buffer.NewLine;

        switch (command.Action)
        {
            // The marker is re-derived per copy rather than taken from the first line: after the
            // first copy the current line is the one just written, so '3o- ' walks down a list
            // writing the next marker each time, exactly as three separate 'o's would.
            case VimAction.OpenLineBelow:
                for (int copy = 0; copy < copies; copy++)
                {
                    VimLine line = CurrentLine;
                    string prefix = ListContinuation.OpenBelow(line.Text);

                    _buffer.Replace(line.EndOffset, 0, newLine + prefix + text);
                    _buffer.CaretOffset = line.EndOffset + newLine.Length + prefix.Length + text.Length;
                }

                break;

            case VimAction.OpenLineAbove:
                for (int copy = 0; copy < copies; copy++)
                {
                    VimLine line = CurrentLine;
                    string prefix = ListContinuation.OpenAbove(line.Text);

                    _buffer.Replace(line.Offset, 0, prefix + text + newLine);
                    _buffer.CaretOffset = line.Offset + prefix.Length + text.Length;
                }

                break;

            case VimAction.InsertBeforeCaret:
            case VimAction.InsertAtLineStart:
            case VimAction.InsertAfterCaret:
            case VimAction.InsertAtLineEnd:
                if (text.Length > 0)
                {
                    TypeText(Repeat(text, copies, string.Empty));
                }

                break;

            default:
                break;
        }
    }

    /// <summary>
    /// Writes text at the caret as insert mode would, and leaves the caret after it.
    /// </summary>
    private void TypeText(string text)
    {
        if (text.Length == 0)
        {
            return;
        }

        int caret = _buffer.CaretOffset;

        _buffer.Replace(caret, 0, text);
        _buffer.CaretOffset = caret + text.Length;
    }

    /// <summary>
    /// The keys that would have typed a piece of text: a line break is Enter, a tab is Tab, and
    /// everything else is itself.
    /// </summary>
    private static IReadOnlyList<VimKey> TypedKeys(string text)
    {
        List<VimKey> keys = [];

        foreach (char character in text)
        {
            switch (character)
            {
                case '\r':
                    break;

                case '\n':
                    keys.Add(VimKey.Named(VimSpecialKey.Enter));
                    break;

                case '\t':
                    keys.Add(VimKey.Named(VimSpecialKey.Tab));
                    break;

                default:
                    keys.Add(VimKey.Typed(character));
                    break;
            }
        }

        return keys;
    }

    /// <summary>
    /// Runs the last change again — <c>.</c>.
    /// </summary>
    /// <param name="dot">The <c>.</c> itself, for the count and the register it may carry.</param>
    /// <remarks>
    /// The same command goes back through <see cref="Execute"/>, with the new count in place of the
    /// old one when there is one — and kept, so that a bare <c>.</c> after <c>3.</c> is <c>3.</c>
    /// again, which is vim's rule. A command that entered insert mode gets its text typed back in and
    /// the session closed, inside the session's own undo group, so the whole replay is one <c>u</c>.
    /// One over a selection has the selection made again first, the same size, from the caret. With
    /// no change to repeat nothing happens, without a word.
    /// </remarks>
    private void RepeatLastChange(VimCommand dot)
    {
        if (_lastChange is not { } last)
        {
            return;
        }

        VimCommand command = last.Command;

        if (dot.HasCount)
        {
            command = command with { Count = dot.Count };
        }

        if (dot.Register != '\0')
        {
            command = command with { Register = dot.Register };
        }

        _lastChange = last with { Command = command };
        _replaying = true;

        try
        {
            if (last.Visual is { } extent)
            {
                Reselect(extent);
            }

            Execute(command);

            if (Mode == VimMode.Insert)
            {
                TypeText(last.Text ?? string.Empty);
                LeaveInsert();
            }
        }
        finally
        {
            _replaying = false;
        }
    }

    /// <summary>
    /// How far the selection reaches, as <c>.</c> will need it: how many lines, and on one line how
    /// many characters, over several the column the last line reaches to.
    /// </summary>
    private VisualExtent CurrentExtent()
    {
        VimTextRange range = VisualRange();
        VimLine first = _buffer.GetLineByOffset(range.Start);
        VimLine last = _buffer.GetLineByOffset(range.IsLinewise ? range.End : Math.Max(range.Start, range.End - 1));
        int lines = (last.Number - first.Number) + 1;

        return new VisualExtent(Mode, lines, lines == 1 ? range.Length : range.End - last.Offset);
    }

    /// <summary>
    /// Selects again, from the caret, as much as a remembered selection covered — vim's rule for
    /// <c>.</c> after a visual command.
    /// </summary>
    private void Reselect(VisualExtent extent)
    {
        int anchor = _buffer.CaretOffset;
        VimLine line = CurrentLine;
        int caret;

        if (extent.Lines > 1)
        {
            VimLine last = _buffer.GetLine(Math.Min((line.Number + extent.Lines) - 1, _buffer.LineCount));

            caret = extent.Mode == VimMode.VisualLine
                ? last.Offset
                : Math.Min(last.Offset + Math.Max(extent.Column - 1, 0), last.LastCaretOffset);
        }
        else
        {
            caret = extent.Mode == VimMode.VisualLine
                ? anchor
                : Math.Min(anchor + Math.Max(extent.Column - 1, 0), line.LastCaretOffset);
        }

        _visualAnchor = anchor;
        SetCaret(caret);
        SetMode(extent.Mode);
    }

    /// <summary>
    /// Opens a recording — <c>qa</c> — or, when one is already open, closes it, which is what vim
    /// does with a <c>q</c> that reached the parser through a count.
    /// </summary>
    private void StartRecording(char register)
    {
        if (_recording is not null)
        {
            StopRecording();

            return;
        }

        _recording = register;
        _macro.Clear();
        ClearStatus();
    }

    private void StopRecording()
    {
        if (_recording is not { } register)
        {
            return;
        }

        string keys = _macro.ToString();

        _recording = null;
        _macro.Clear();

        Registers.Record(register, keys);
        ClearStatus();
    }

    /// <summary>
    /// Runs a register as keys — <c>@a</c>, <c>@@</c> — <paramref name="count"/> times.
    /// </summary>
    /// <param name="name">The register, or <c>@</c> for the one last run.</param>
    /// <param name="count">How many times.</param>
    /// <remarks>
    /// <para>
    /// The keys go back through <see cref="HandleKey"/> one at a time, and a key the engine turns down
    /// — insert mode's — is typed into the buffer the way the editor would have typed it. That is the
    /// test harness's own loop, and it is here so that a macro is exactly what typing the keys would
    /// have been.
    /// </para>
    /// <para>
    /// The run stops at the first thing that fails: a sequence that spells nothing, a find or a
    /// search that finds nothing, an ex command that is not one, a register with nothing in it — and
    /// a nesting deeper than <see cref="MaxMacroDepth"/>, which is a macro running itself with no way
    /// out. What ran before that stays; vim's rule too, and the reason a recursive macro is a way of
    /// saying "until this stops working".
    /// </para>
    /// </remarks>
    private void PlayMacro(char name, int count)
    {
        // A fresh run from the keyboard forgets how the last one ended; a nested one must not, or an
        // inner failure would not reach the outer loop.
        if (_macroDepth == 0)
        {
            _macroFailed = false;
        }

        if (name == '@')
        {
            if (_lastMacro == '\0')
            {
                Fail(NoPreviousMacro);

                return;
            }

            name = _lastMacro;
        }

        if (_macroDepth >= MaxMacroDepth)
        {
            Fail(MacroNestingTooDeep);

            return;
        }

        if (Registers.Get(name) is not { } register)
        {
            // Nothing to run. Silent, as vim's beep is, and still a failure for any macro this one is
            // inside.
            _macroFailed = true;

            return;
        }

        _lastMacro = char.ToLowerInvariant(name);

        IReadOnlyList<VimKey> keys = RegisterKeys(register);

        _macroDepth++;

        try
        {
            for (int run = 0; (run < count) && !_macroFailed; run++)
            {
                foreach (VimKey key in keys)
                {
                    if (!HandleKey(key))
                    {
                        TypeIntoBuffer(key);
                    }

                    if (_macroFailed)
                    {
                        break;
                    }
                }
            }
        }
        finally
        {
            _macroDepth--;
        }
    }

    /// <summary>
    /// Reads a register as keys.
    /// </summary>
    /// <remarks>
    /// A recording is already in <see cref="VimKeyNotation"/>. Any other register is text, and is
    /// read the way vim reads one it is asked to run: a line break is an Enter, and a linewise
    /// register ends every line with one, so that a yanked line runs as a command.
    /// </remarks>
    private static IReadOnlyList<VimKey> RegisterKeys(VimRegister register)
    {
        string text = register.Text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\n", "<CR>", StringComparison.Ordinal)
            .Replace("\t", "<Tab>", StringComparison.Ordinal);

        if (register.Kind == VimRegisterKind.Linewise)
        {
            text += "<CR>";
        }

        return VimKeyNotation.Parse(text);
    }

    /// <summary>
    /// Does to the buffer what the editor does with a key the engine has turned down — insert mode's
    /// typing — so that a macro replays through insert mode exactly as typing would have.
    /// </summary>
    private void TypeIntoBuffer(VimKey key)
    {
        switch (key.Special)
        {
            case VimSpecialKey.Enter:
                TypeText(_buffer.NewLine);
                break;

            case VimSpecialKey.Backspace:
                if (_buffer.CaretOffset > 0)
                {
                    _buffer.Replace(_buffer.CaretOffset - 1, 1, string.Empty);
                    _buffer.CaretOffset--;
                }

                break;

            case VimSpecialKey.Tab:
                TypeText("\t");
                break;

            case VimSpecialKey.Home:
                _buffer.CaretOffset = CurrentLine.Offset;
                break;

            case VimSpecialKey.End:
                _buffer.CaretOffset = CurrentLine.EndOffset;
                break;

            case VimSpecialKey.None when !char.IsControl(key.Character) && !key.HasCommandModifier:
                TypeText(key.Character.ToString());
                break;

            default:
                break;
        }
    }

    private void SetMode(VimMode mode)
    {
        if (Mode == mode)
        {
            return;
        }

        Mode = mode;
        ModeChanged?.Invoke(this, mode);
    }

    private void SetCaret(int offset)
    {
        int clamped = Math.Clamp(offset, 0, _buffer.TextLength);
        VimLine line = _buffer.GetLineByOffset(clamped);

        _buffer.CaretOffset = Math.Clamp(clamped, line.Offset, line.LastCaretOffset);
    }

    private int Walk(int caret, int count, Func<IVimBuffer, int, int> step)
    {
        for (int index = 0; index < count; index++)
        {
            caret = step(_buffer, caret);
        }

        return caret;
    }

    /// <summary>
    /// Where a selection was: both ends as a line and a column, and the mode it was made in.
    /// </summary>
    private readonly record struct RememberedSelection(
        int AnchorLine,
        int AnchorColumn,
        int CaretLine,
        int CaretColumn,
        VimMode Mode);

    /// <summary>
    /// How far a selection reached, as a size rather than a place, so that it can be made again
    /// from wherever the caret is: the mode, how many lines, and — characterwise — how many
    /// characters on one line or the column the last line reaches to over several.
    /// </summary>
    private readonly record struct VisualExtent(VimMode Mode, int Lines, int Column);

    /// <summary>
    /// What <c>.</c> repeats: the command, the text typed after it when it entered insert mode, and
    /// the selection it was over when it was one of the visual commands.
    /// </summary>
    private readonly record struct RecordedChange(VimCommand Command, string? Text, VisualExtent? Visual);

    /// <summary>
    /// What <c>n</c> repeats: the pattern, which way the search that set it went, and whether
    /// <c>smartcase</c> may read the pattern — it may not when <c>*</c> or <c>#</c> took it off the
    /// page.
    /// </summary>
    private readonly record struct LastSearch(string Pattern, bool Forward, bool SmartCase);
}
