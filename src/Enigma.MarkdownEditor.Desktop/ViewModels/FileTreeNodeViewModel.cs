using System;
using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Enigma.Icons.Phosphor;
using Enigma.MarkdownEditor.Core.Files;
using Enigma.MarkdownEditor.Core.Workspaces;
using Enigma.MarkdownEditor.Desktop.Controls;

namespace Enigma.MarkdownEditor.Desktop.ViewModels;

/// <summary>
/// One row of the workspace file tree.
/// </summary>
/// <remarks>
/// A node knows what it is and whether it is open; it does not know how to read a directory. Expanding
/// one calls back into <see cref="FileTreeViewModel"/>, which owns the file system, the watchers and
/// the error reporting — so a node stays cheap enough to have thousands of.
/// </remarks>
public sealed class FileTreeNodeViewModel : ObservableObject
{
    private readonly Action<FileTreeNodeViewModel>? _onExpanded;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileTreeNodeViewModel"/> class.
    /// </summary>
    /// <param name="fullPath">
    /// The entry's absolute path. For the four informational kinds, the directory being described.
    /// </param>
    /// <param name="name">The label shown in the tree.</param>
    /// <param name="kind">What the row stands for.</param>
    /// <param name="isRoot">Whether this is one of the workspace's own root directories.</param>
    /// <param name="onExpanded">
    /// Called the first time a directory is expanded, so its children can be read. Ignored for every
    /// other kind.
    /// </param>
    /// <exception cref="ArgumentException">Either string is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException">Either string is <see langword="null"/>.</exception>
    public FileTreeNodeViewModel(
        string fullPath,
        string name,
        FileTreeNodeKind kind,
        bool isRoot = false,
        Action<FileTreeNodeViewModel>? onExpanded = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        FullPath = fullPath;
        Name = name;
        Kind = kind;
        IsRoot = isRoot;
        _onExpanded = onExpanded;
    }

    /// <summary>
    /// Gets the entry's absolute path, or — for an informational row — the directory it describes.
    /// </summary>
    public string FullPath { get; }

    /// <summary>
    /// Gets the label shown in the tree.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets what the row stands for.
    /// </summary>
    public FileTreeNodeKind Kind { get; }

    /// <summary>
    /// Gets a value indicating whether this is one of the workspace's own root directories.
    /// </summary>
    public bool IsRoot { get; }

    /// <summary>
    /// Gets the node's children. A directory that has not been read yet holds a single
    /// <see cref="FileTreeNodeKind.Loading"/> row, which is what gives it an expander to click.
    /// </summary>
    public ObservableCollection<FileTreeNodeViewModel> Children { get; } = [];

    /// <summary>
    /// Gets a value indicating whether the row is a directory the tree can walk into.
    /// </summary>
    public bool IsDirectory => Kind == FileTreeNodeKind.Directory;

    /// <summary>
    /// Gets a value indicating whether the row is a file that can be opened.
    /// </summary>
    public bool IsFile => Kind == FileTreeNodeKind.File;

    /// <summary>
    /// Gets a value indicating whether the row is a root directory that is not on disk right now.
    /// </summary>
    public bool IsMissing => Kind == FileTreeNodeKind.Missing;

    /// <summary>
    /// Gets a value indicating whether the row describes its parent rather than being an entry of the
    /// file system. Such a row is never opened and never counted.
    /// </summary>
    public bool IsInformational =>
        Kind is FileTreeNodeKind.Loading
             or FileTreeNodeKind.Empty
             or FileTreeNodeKind.AccessDenied
             or FileTreeNodeKind.Unavailable;

    /// <summary>
    /// Gets or sets a value indicating whether this directory's children have been read.
    /// </summary>
    public bool IsLoaded { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the row is expanded. Setting it to
    /// <see langword="true"/> the first time is what triggers the directory's listing.
    /// </summary>
    public bool IsExpanded
    {
        get;
        set
        {
            if (!SetProperty(ref field, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IconKind));

            if (value && IsDirectory)
            {
                _onExpanded?.Invoke(this);
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the row is the selected one.
    /// </summary>
    public bool IsSelected { get; set => SetProperty(ref field, value); }

    /// <summary>
    /// Gets or sets the colour set on this folder, or <see cref="FolderColor.None"/>.
    /// </summary>
    /// <remarks>
    /// The colour set on <em>this</em> folder, not the one it inherits: the tree shows where a colour
    /// lives so it can be found and changed, and a tab is what shows which coloured folder a file
    /// belongs to. Assigned by <see cref="FileTreeViewModel"/>, which is what knows the workspace.
    /// </remarks>
    public FolderColor Color
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(ColorBrush));
            }
        }
    }

    /// <summary>
    /// Gets the brush the row's colour bar is painted with, or <see langword="null"/> when the folder
    /// has no colour.
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> is the whole trick: a <c>Border</c> with no background paints nothing
    /// and still takes its width, so a coloured row and an uncoloured one line up.
    /// </remarks>
    public IBrush? ColorBrush => FolderColorPalette.Brush(Color);

    /// <summary>
    /// Gets the icon the row is drawn with.
    /// </summary>
    public PhosphorIcon IconKind => Kind switch
    {
        FileTreeNodeKind.Directory => IsExpanded ? PhosphorIcon.FolderOpen : PhosphorIcon.Folder,
        FileTreeNodeKind.File => FileTreeFilter.IsMarkdown(Name) ? PhosphorIcon.FileMd : PhosphorIcon.File,
        FileTreeNodeKind.Missing => PhosphorIcon.Warning,
        FileTreeNodeKind.Loading => PhosphorIcon.CircleNotch,
        FileTreeNodeKind.Empty => PhosphorIcon.FolderDashed,
        FileTreeNodeKind.AccessDenied => PhosphorIcon.Lock,
        _ => PhosphorIcon.Warning,
    };

    /// <summary>
    /// Gets what the row says on hover: the full path for a real entry, the reason for the rest.
    /// </summary>
    public string Tooltip => Kind switch
    {
        FileTreeNodeKind.Missing => $"{FullPath} is not there right now.",
        FileTreeNodeKind.Loading => "Reading this folder…",
        FileTreeNodeKind.Empty => "This folder holds nothing the tree is showing.",
        FileTreeNodeKind.AccessDenied => $"{FullPath} could not be read: you do not have access to it.",
        FileTreeNodeKind.Unavailable => $"{FullPath} could not be read.",
        _ => FullPath,
    };

    /// <summary>
    /// Builds the node for one entry of a listed directory.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="onExpanded">The callback that reads a directory's children.</param>
    /// <returns>The node.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <see langword="null"/>.</exception>
    public static FileTreeNodeViewModel For(FileSystemEntry entry, Action<FileTreeNodeViewModel>? onExpanded = null)
    {
        ArgumentNullException.ThrowIfNull(entry);

        FileTreeNodeViewModel node = new(
            entry.FullPath,
            entry.Name,
            entry.IsDirectory ? FileTreeNodeKind.Directory : FileTreeNodeKind.File,
            isRoot: false,
            onExpanded);

        if (entry.IsDirectory)
        {
            node.Children.Add(Informational(entry.FullPath, FileTreeNodeKind.Loading));
        }

        return node;
    }

    /// <summary>
    /// Builds the node for one of the workspace's root directories.
    /// </summary>
    /// <param name="directory">The root directory, which may or may not be on disk.</param>
    /// <param name="onExpanded">The callback that reads a directory's children.</param>
    /// <returns>The node.</returns>
    /// <exception cref="ArgumentException"><paramref name="directory"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="directory"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// A root is labelled with its own folder name and carries the whole path in its tooltip — two
    /// workspaces' roots are routinely called <c>docs</c>, and the path is what tells them apart.
    /// </remarks>
    public static FileTreeNodeViewModel Root(string directory, Action<FileTreeNodeViewModel>? onExpanded = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        bool exists = WorkspaceValidation.DirectoryExists(directory);
        string name = WorkspaceValidation.DirectoryName(directory);

        FileTreeNodeViewModel node = new(
            directory,
            string.IsNullOrEmpty(name) ? directory : name,
            exists ? FileTreeNodeKind.Directory : FileTreeNodeKind.Missing,
            isRoot: true,
            onExpanded);

        if (exists)
        {
            node.Children.Add(Informational(directory, FileTreeNodeKind.Loading));
        }

        return node;
    }

    /// <summary>
    /// Builds one of the rows that describe a directory rather than list it.
    /// </summary>
    /// <param name="directory">The directory being described.</param>
    /// <param name="kind">Which of the informational kinds.</param>
    /// <returns>The node.</returns>
    /// <exception cref="ArgumentException"><paramref name="directory"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="directory"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not an informational kind.</exception>
    public static FileTreeNodeViewModel Informational(string directory, FileTreeNodeKind kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        string label = kind switch
        {
            FileTreeNodeKind.Loading => "Reading…",
            FileTreeNodeKind.Empty => "Nothing here",
            FileTreeNodeKind.AccessDenied => "No access",
            FileTreeNodeKind.Unavailable => "Could not be read",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not an informational kind."),
        };

        return new FileTreeNodeViewModel(directory, label, kind);
    }

    /// <summary>
    /// Builds the informational row that matches a listing that failed.
    /// </summary>
    /// <param name="directory">The directory that was listed.</param>
    /// <param name="status">How it answered.</param>
    /// <returns>The node.</returns>
    /// <exception cref="ArgumentException"><paramref name="directory"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="directory"/> is <see langword="null"/>.</exception>
    public static FileTreeNodeViewModel For(string directory, DirectoryListingStatus status)
        => Informational(
            directory,
            status switch
            {
                DirectoryListingStatus.AccessDenied => FileTreeNodeKind.AccessDenied,
                DirectoryListingStatus.Ok => FileTreeNodeKind.Empty,
                _ => FileTreeNodeKind.Unavailable,
            });
}
