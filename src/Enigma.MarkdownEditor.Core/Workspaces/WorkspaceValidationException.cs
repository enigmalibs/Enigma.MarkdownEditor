using System;

namespace Enigma.MarkdownEditor.Core.Workspaces;

/// <summary>
/// Thrown when an operation would leave the workspace list in a state the rules forbid.
/// </summary>
/// <remarks>
/// The message is written for the user, not for a log: it is what the manage dialog puts under the box
/// that caused it. A user interface is expected to have asked <see cref="WorkspaceValidation"/> first,
/// so reaching this exception means either a race or a caller that skipped the check.
/// </remarks>
public sealed class WorkspaceValidationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceValidationException"/> class.
    /// </summary>
    /// <param name="message">What is wrong, in words the user can act on.</param>
    public WorkspaceValidationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceValidationException"/> class.
    /// </summary>
    /// <param name="message">What is wrong, in words the user can act on.</param>
    /// <param name="innerException">The failure underneath, when there is one.</param>
    public WorkspaceValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
