namespace Enigma.MarkdownEditor.Core.Vim;

/// <summary>
/// The handful of vim options the engine reads while it works.
/// </summary>
/// <remarks>
/// <para>
/// Vim has several hundred; these are the ones a command in this engine actually consults. They are
/// a mutable object rather than a value on purpose: the editor seeds them from its own settings when
/// it attaches the engine, and a later <c>:set</c> edits the same instance, so there is one place
/// the answer to "how wide is a shift?" lives and both writers reach it.
/// </para>
/// <para>
/// The defaults are what the editor control itself defaults to rather than vim's own — four
/// columns and spaces, not eight and tabs — because a <c>&gt;&gt;</c> that indented differently from
/// the editor's Tab key would make the two halves of the same application disagree about the same
/// document. The three search options keep vim's defaults, because there is nothing in the editor for
/// them to agree with: a search is the engine's alone.
/// </para>
/// </remarks>
public sealed class VimOptions
{
    /// <summary>
    /// Gets or sets how many columns <c>&gt;&gt;</c> and <c>&lt;&lt;</c> move a line by — vim's
    /// <c>shiftwidth</c>. Also how wide a tab counts as when an indent is measured.
    /// </summary>
    public int ShiftWidth { get; set; } = 4;

    /// <summary>
    /// Gets or sets a value indicating whether an indent is written as spaces rather than as tabs —
    /// vim's <c>expandtab</c>.
    /// </summary>
    public bool ExpandTab { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether a search pattern matches whatever the case of the text
    /// — vim's <c>ignorecase</c>. Off, as vim's is.
    /// </summary>
    public bool IgnoreCase { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a pattern with a capital letter in it is matched
    /// exactly even while <see cref="IgnoreCase"/> is on — vim's <c>smartcase</c>.
    /// </summary>
    /// <remarks>
    /// Read only when <see cref="IgnoreCase"/> is on, and only for a pattern that was typed: <c>*</c>
    /// and <c>#</c> build theirs from the text, where a capital says nothing about what the user
    /// meant, and vim leaves this option out for them too.
    /// </remarks>
    public bool SmartCase { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a search that runs off the end of the buffer starts
    /// again from the other end — vim's <c>wrapscan</c>. On, as vim's is.
    /// </summary>
    /// <remarks>
    /// Off, a search that finds nothing ahead stops where it is and says which end it hit, which is
    /// what makes <c>n</c> a safe way to walk to the last match without being carried back to the
    /// first.
    /// </remarks>
    public bool WrapScan { get; set; } = true;
}
