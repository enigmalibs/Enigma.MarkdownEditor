using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Enigma.MarkdownEditor.Desktop.Services;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;

/// <summary>
/// Records what was handed to the operating system — files, and the addresses a preview link points
/// at. The real launcher needs a window, which a unit test has none of.
/// </summary>
internal sealed class FakeFileLauncher : IFileLauncher
{
    private readonly List<string> _launched = [];
    private readonly List<Uri> _addresses = [];

    /// <summary>
    /// Set to make the platform decline, as it does when nothing is registered for the file type.
    /// </summary>
    public bool Declines { get; init; }

    /// <summary>
    /// Set to make the platform throw, as it does when the shell association is broken.
    /// </summary>
    public Exception? Failure { get; init; }

    public IReadOnlyList<string> Launched => _launched;

    /// <summary>
    /// The addresses handed to the platform — what an external link in the preview did.
    /// </summary>
    public IReadOnlyList<Uri> Addresses => _addresses;

    public Task<bool> LaunchAsync(string filePath)
    {
        if (Failure is not null)
        {
            return Task.FromException<bool>(Failure);
        }

        _launched.Add(filePath);

        return Task.FromResult(!Declines);
    }

    public Task<bool> LaunchUriAsync(Uri address)
    {
        if (Failure is not null)
        {
            return Task.FromException<bool>(Failure);
        }

        _addresses.Add(address);

        return Task.FromResult(!Declines);
    }
}
