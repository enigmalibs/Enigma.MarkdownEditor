using System;
using System.Collections.Generic;

namespace Enigma.MarkdownEditor.Core.Files;

/// <summary>
/// The result of listing one directory: its visible entries, or why there are none.
/// </summary>
public sealed class DirectoryListing
{
    private DirectoryListing(DirectoryListingStatus status, IReadOnlyList<FileSystemEntry> entries)
    {
        Status = status;
        Entries = entries;
    }

    /// <summary>
    /// Gets how the directory answered.
    /// </summary>
    public DirectoryListingStatus Status { get; }

    /// <summary>
    /// Gets the entries that passed the filter, already in tree order. Empty unless
    /// <see cref="Status"/> is <see cref="DirectoryListingStatus.Ok"/>.
    /// </summary>
    public IReadOnlyList<FileSystemEntry> Entries { get; }

    /// <summary>
    /// Gets a value indicating whether the directory was read and holds nothing the tree shows.
    /// </summary>
    public bool IsEmpty => (Status == DirectoryListingStatus.Ok) && (Entries.Count == 0);

    /// <summary>
    /// Builds the result of a directory that was read.
    /// </summary>
    /// <param name="entries">The entries that passed the filter, in tree order.</param>
    /// <returns>The listing.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entries"/> is <see langword="null"/>.</exception>
    public static DirectoryListing Ok(IReadOnlyList<FileSystemEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return new DirectoryListing(DirectoryListingStatus.Ok, entries);
    }

    /// <summary>
    /// Builds the result of a directory that could not be read.
    /// </summary>
    /// <param name="status">Why it could not be read.</param>
    /// <returns>The listing, with no entries.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="status"/> is <see cref="DirectoryListingStatus.Ok"/>, which is not a failure.
    /// </exception>
    public static DirectoryListing Failed(DirectoryListingStatus status)
    {
        if (status == DirectoryListingStatus.Ok)
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "A failed listing needs a reason.");
        }

        return new DirectoryListing(status, []);
    }
}
