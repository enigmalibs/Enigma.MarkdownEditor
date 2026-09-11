using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Enigma.MarkdownEditor.Desktop.Services;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;

/// <summary>
/// Records what the tree asked to be opened. What opening actually means is covered where it is
/// decided — see <c>DocumentOpenerTests</c>.
/// </summary>
internal sealed class FakeDocumentOpener : IDocumentOpener
{
    private readonly List<string> _opened = [];

    /// <summary>
    /// Set to make every call fail, standing in for a file that went away mid-click.
    /// </summary>
    public Exception? Failure { get; init; }

    public IReadOnlyList<string> Opened => _opened;

    public Task OpenAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (Failure is not null)
        {
            return Task.FromException(Failure);
        }

        _opened.Add(filePath);

        return Task.CompletedTask;
    }
}
