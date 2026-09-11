using System;

namespace Enigma.MarkdownEditor.Core.Files;

/// <summary>
/// Thrown when a file operation the tree asked for cannot be carried out.
/// </summary>
/// <remarks>
/// The message is written for the user, not for a log: it is what the tree puts in its info bar, or
/// under the text box of the dialog that started the operation. A user interface is expected to have
/// asked <see cref="FileNameValidation"/> or <see cref="FileMovePlanner"/> first, so reaching this
/// exception usually means the file system moved under the check — which is exactly the case that
/// cannot be validated away.
/// </remarks>
public sealed class FileOperationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FileOperationException"/> class.
    /// </summary>
    /// <param name="message">What went wrong, in words the user can act on.</param>
    public FileOperationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileOperationException"/> class.
    /// </summary>
    /// <param name="message">What went wrong, in words the user can act on.</param>
    /// <param name="innerException">The failure underneath, when there is one.</param>
    public FileOperationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
