using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Enigma.MarkdownEditor.Core.Files;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;

/// <summary>
/// A trash the test decides the behaviour of: one that takes what it is given, one that declines, and
/// one that is not there at all.
/// </summary>
/// <remarks>
/// It really does remove the entry when it accepts it, because the tree re-reads the folder afterwards
/// and a fake that only recorded the call would leave the row exactly where it was. Where the entry
/// goes is not this double's business — the real recycle bin and the XDG trash are covered in
/// <c>TrashServiceTests</c>.
/// </remarks>
internal sealed class FakeTrashService : ITrashService
{
    private readonly List<string> _trashed = [];

    /// <summary>
    /// Whether this platform has a trash at all.
    /// </summary>
    public bool IsAvailable { get; init; } = true;

    /// <summary>
    /// Whether it takes what it is given. <see langword="false"/> stands for the trash that exists but
    /// declined this entry — a cross-device move, typically — which sends the caller down the
    /// confirmed permanent-delete path just as an absent trash does.
    /// </summary>
    public bool Accepts { get; init; } = true;

    /// <summary>
    /// Set to make every call fail, standing in for a trash that broke rather than declined.
    /// </summary>
    public Exception? Failure { get; init; }

    /// <summary>
    /// File or folder names this trash declines although it takes everything else — one entry of a
    /// selection on another device, typically.
    /// </summary>
    public IReadOnlyCollection<string> Declines { get; init; } = [];

    /// <summary>
    /// File or folder names this trash breaks on although it takes everything else: a failure in the
    /// middle of a selection.
    /// </summary>
    public IReadOnlyCollection<string> BreaksOn { get; init; } = [];

    public IReadOnlyList<string> Trashed => _trashed;

    public Task<bool> MoveToTrashAsync(string path, CancellationToken cancellationToken = default)
    {
        string name = Path.GetFileName(path);

        if (Failure is not null)
        {
            return Task.FromException<bool>(Failure);
        }

        if (BreaksOn.Contains(name))
        {
            return Task.FromException<bool>(new IOException($"the shell said no to '{name}'"));
        }

        if (!IsAvailable || !Accepts || Declines.Contains(name))
        {
            return Task.FromResult(false);
        }

        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
        else
        {
            File.Delete(path);
        }

        _trashed.Add(path);

        return Task.FromResult(true);
    }
}
