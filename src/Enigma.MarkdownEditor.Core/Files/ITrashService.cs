using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Enigma.MarkdownEditor.Core.Files;

/// <summary>
/// Hands a file or directory to the operating system's trash, so that deleting from the tree stays
/// recoverable from the place the user already knows to look.
/// </summary>
/// <remarks>
/// <para>
/// The return value carries one very specific promise, and the whole delete flow is built on it:
/// <see langword="false"/> means the entry is <i>still on disk, exactly as it was</i>. That is what
/// lets the caller answer a declined trash by offering a confirmed permanent delete instead, without
/// ever having to ask the file system what happened.
/// </para>
/// <para>
/// Anything that fails in a way that might have half-happened therefore throws rather than returning
/// <see langword="false"/>. <see cref="IOException"/> and <see cref="UnauthorizedAccessException"/>
/// are the expected escapes; the caller reports them to the user as the failure they are.
/// </para>
/// </remarks>
public interface ITrashService
{
    /// <summary>
    /// Gets a value indicating whether this platform can move an entry to the trash at all.
    /// </summary>
    /// <remarks>
    /// Answered per platform rather than per entry: it is the right thing to consult when deciding
    /// what to call the menu item, not a promise that any particular delete will succeed.
    /// </remarks>
    bool IsAvailable { get; }

    /// <summary>
    /// Moves a file or directory to the operating system's trash.
    /// </summary>
    /// <param name="path">The file or directory to trash.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// <see langword="true"/> when the entry is in the trash — including when it was already gone,
    /// since the caller's goal is that it is no longer there. <see langword="false"/> when this
    /// platform or this setup cannot trash it <i>and the entry is untouched</i>, which is the
    /// caller's cue to offer a confirmed permanent delete instead.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="IOException">The trash refused the entry in a way that may have half-happened.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
    /// <exception cref="UnauthorizedAccessException">This user is not allowed to move the entry.</exception>
    Task<bool> MoveToTrashAsync(string path, CancellationToken cancellationToken = default);
}
