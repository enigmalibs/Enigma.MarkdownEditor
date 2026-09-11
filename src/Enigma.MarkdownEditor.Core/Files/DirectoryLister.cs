using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Enigma.MarkdownEditor.Core.Files;

/// <summary>
/// Reads one directory's visible entries, applying <see cref="FileTreeFilter"/> and putting the
/// result in tree order.
/// </summary>
/// <remarks>
/// One level at a time and never recursively: the tree expands lazily, so a workspace over a folder
/// with a hundred thousand files costs one listing, not a walk.
/// </remarks>
public static class DirectoryLister
{
    /// <summary>
    /// Lists a directory off the calling thread.
    /// </summary>
    /// <param name="directory">The directory to list.</param>
    /// <param name="showAllFiles">The ShowAllFiles setting.</param>
    /// <param name="cancellationToken">Abandons the listing.</param>
    /// <returns>The directory's visible entries, or why there are none.</returns>
    /// <exception cref="ArgumentException"><paramref name="directory"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="directory"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
    /// <remarks>
    /// Offloaded with <see cref="Task.Run(Func{DirectoryListing}, CancellationToken)"/> rather than
    /// awaiting a genuinely asynchronous API, because there is none: directory enumeration is
    /// synchronous all the way down. The offload is not decoration — a root on a sleeping network
    /// share blocks for seconds, and this is what keeps that off the UI thread.
    /// </remarks>
    public static Task<DirectoryListing> ListAsync(
        string directory,
        bool showAllFiles,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        return Task.Run(() => List(directory, showAllFiles, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Lists a directory on the calling thread.
    /// </summary>
    /// <param name="directory">The directory to list.</param>
    /// <param name="showAllFiles">The ShowAllFiles setting.</param>
    /// <param name="cancellationToken">Abandons the listing.</param>
    /// <returns>The directory's visible entries, or why there are none.</returns>
    /// <exception cref="ArgumentException"><paramref name="directory"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="directory"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
    public static DirectoryListing List(
        string directory,
        bool showAllFiles,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        List<FileSystemEntry> entries = [];

        try
        {
            DirectoryInfo root = new(directory);

            // Asked before enumerating so that "not there" is reported as itself rather than as the
            // DirectoryNotFoundException the enumerator would raise on its first move.
            if (!root.Exists)
            {
                return DirectoryListing.Failed(DirectoryListingStatus.Missing);
            }

            EnumerationOptions options = new()
            {
                RecurseSubdirectories = false,

                // An entry this user cannot stat is skipped rather than aborting the whole listing:
                // one unreadable subfolder must not empty its parent.
                IgnoreInaccessible = true,

                // Nothing is skipped by attribute here — the hidden/system rule belongs to
                // FileTreeFilter, which is the one place the tree's contents are decided and tested.
                AttributesToSkip = 0,
            };

            foreach (FileSystemInfo info in root.EnumerateFileSystemInfos("*", options))
            {
                cancellationToken.ThrowIfCancellationRequested();

                FileAttributes attributes = SafeAttributes(info);
                bool isDirectory = (attributes & FileAttributes.Directory) != 0;
                bool isHidden = (attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0;

                if (!FileTreeFilter.Includes(info.Name, isDirectory, isHidden, showAllFiles))
                {
                    continue;
                }

                entries.Add(new FileSystemEntry(info.FullName, info.Name, isDirectory));
            }
        }
        catch (UnauthorizedAccessException)
        {
            return DirectoryListing.Failed(DirectoryListingStatus.AccessDenied);
        }
        catch (DirectoryNotFoundException)
        {
            // The directory went away between the Exists check and the enumeration.
            return DirectoryListing.Failed(DirectoryListingStatus.Missing);
        }
        catch (IOException)
        {
            return DirectoryListing.Failed(DirectoryListingStatus.Unavailable);
        }

        entries.Sort(FileSystemEntryComparer.Instance);

        return DirectoryListing.Ok(entries);
    }

    // A broken symlink or a reparse point the OS refuses to follow throws from the property rather
    // than from the enumerator. Treated as a hidden file, so it is skipped rather than drawn as a
    // node that leads nowhere.
    private static FileAttributes SafeAttributes(FileSystemInfo info)
    {
        try
        {
            return info.Attributes;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return FileAttributes.Hidden;
        }
    }
}
