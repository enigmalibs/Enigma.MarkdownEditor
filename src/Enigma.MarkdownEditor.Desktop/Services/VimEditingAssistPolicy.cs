using System;
using Enigma.MarkdownEditor.Core.Vim;

namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// The markdown assists apply while the editor is being typed into, and not while it is being
/// commanded.
/// </summary>
/// <remarks>
/// The answer FEATURE-3C44 left this seam waiting for. With vim off, every keystroke is typing and
/// the assists are always on, exactly as before. With vim on, only insert mode is typing: in normal
/// mode <c>Enter</c> and <c>Tab</c> are commands, and an editor that quietly continued a bullet there
/// would be an editor that is not vim.
/// </remarks>
public sealed class VimEditingAssistPolicy : IEditingAssistPolicy
{
    private readonly IVimSession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="VimEditingAssistPolicy"/> class.
    /// </summary>
    /// <param name="session">The mode the editor is in, and whether it has modes at all.</param>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    public VimEditingAssistPolicy(IVimSession session)
        => _session = session ?? throw new ArgumentNullException(nameof(session));

    /// <inheritdoc />
    public bool IsActive => !_session.IsEnabled || (_session.Mode == VimMode.Insert);
}
