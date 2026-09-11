using System.Text;

namespace Enigma.MarkdownEditor.Core.Vim;

/// <summary>
/// Follows the buffer's changes while an insert session is open and keeps the text they add up to.
/// </summary>
/// <remarks>
/// <para>
/// The engine never sees what is typed in insert mode — a key there is the editor's — so the only way
/// to know what an insert session inserted is to watch the buffer. What <c>.</c> replays and what a
/// macro holds for the session is this record.
/// </para>
/// <para>
/// The rule is the one that makes typing a straight line: an insertion where the record ends is
/// appended, a one-character removal just before where it ends takes the last character back off,
/// and anything else — a click elsewhere then typing, a paste over a selection, an undo — closes the
/// record, keeping what was typed up to then. Vim starts a new insert on an arrow key; this is the
/// same idea with one fewer state, and the plan records the difference.
/// </para>
/// </remarks>
internal sealed class VimInsertRecorder
{
    private readonly StringBuilder _text = new();

    // Where the text recorded so far ends, which is where the next typed character has to land.
    private int _end;

    /// <summary>
    /// Gets a value indicating whether the record is still following changes.
    /// </summary>
    public bool IsOpen { get; private set; }

    /// <summary>
    /// Gets the text typed so far.
    /// </summary>
    public string Text => _text.ToString();

    /// <summary>
    /// Starts a record at the caret.
    /// </summary>
    /// <param name="caret">Where insert mode began, after the command that opened it has done its own writing.</param>
    public void Begin(int caret)
    {
        _text.Clear();
        _end = caret;
        IsOpen = true;
    }

    /// <summary>
    /// Stops following changes, keeping the text so far.
    /// </summary>
    public void Close() => IsOpen = false;

    /// <summary>
    /// Reads one change.
    /// </summary>
    /// <param name="change">What the buffer reported.</param>
    public void Observe(VimTextChange change)
    {
        if (!IsOpen)
        {
            return;
        }

        if ((change.RemovedLength == 0) && (change.Offset == _end))
        {
            _text.Append(change.InsertedText);
            _end += change.InsertedText.Length;

            return;
        }

        if ((change.RemovedLength == 1)
            && (change.InsertedText.Length == 0)
            && (change.Offset == _end - 1)
            && (_text.Length > 0))
        {
            _text.Length--;
            _end--;

            return;
        }

        IsOpen = false;
    }
}
