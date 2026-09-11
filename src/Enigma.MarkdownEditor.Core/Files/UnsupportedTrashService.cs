using System;
using System.Threading;
using System.Threading.Tasks;

namespace Enigma.MarkdownEditor.Core.Files;

/// <summary>
/// The trash on a platform this application has no trash implementation for.
/// </summary>
/// <remarks>
/// <para>
/// A null object rather than a throwing stub, because "there is no trash here" is an ordinary answer
/// and not a defect: it is what macOS gets until someone writes the <c>NSFileManager</c> call, and
/// what any future platform gets on its first day. The caller already has to handle a declined trash
/// — that is the confirmed permanent delete it offers whenever this method answers
/// <see langword="false"/> — so nothing extra is needed to make this platform work, only slower to
/// undo.
/// </para>
/// <para>
/// The argument is still checked. A caller passing an empty path has a bug, and the bug should not
/// hide behind the one implementation that never looks at the value.
/// </para>
/// </remarks>
public sealed class UnsupportedTrashService : ITrashService
{
    /// <inheritdoc />
    public bool IsAvailable => false;

    /// <inheritdoc />
    public Task<bool> MoveToTrashAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        // Nothing was touched, which is exactly what false promises.
        return Task.FromResult(false);
    }
}
