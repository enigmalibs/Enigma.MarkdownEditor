using System.Collections.Generic;
using Enigma.Configuration;

namespace Enigma.MarkdownEditor.Core.Session;

/// <summary>
/// The half of <see cref="SessionState"/>'s validation that DataAnnotations cannot express.
/// </summary>
/// <remarks>
/// <para>
/// Only the rules that make the file incoherent <em>with itself</em> are here. That the world has
/// moved on since the file was written — a note deleted, a monitor unplugged — is not a broken file
/// and is not validated: <see cref="SessionRestore.Resolve"/> and <see cref="WindowPlacements.Clamp"/>
/// absorb it, because throwing a whole session away over one missing note would be the wrong trade
/// every time.
/// </para>
/// <para>
/// So a window whose remembered size is nonsense is clamped rather than rejected, while an active-tab
/// index pointing past the end of the tab list is rejected: nothing this editor writes can produce
/// one, and there is no honest way to guess what it meant.
/// </para>
/// </remarks>
public sealed class SessionStateValidator : ConfigValidator<SessionState>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SessionStateValidator"/> class.
    /// </summary>
    public SessionStateValidator()
    {
        RuleFor(state => state.OpenDocuments)
            .Must(HasNoBlankPaths, "must not list a document without a path.");

        RuleFor(state => state.ActiveDocumentIndex)
            .Must(AddressesAnOpenDocument, "must be -1, or the position of one of the open documents.");
    }

    private static bool HasNoBlankPaths(List<string> paths)
        => !paths.Exists(string.IsNullOrWhiteSpace);

    private static bool AddressesAnOpenDocument(SessionState state, int index)
        => (index == -1) || ((index >= 0) && (index < state.OpenDocuments.Count));
}
