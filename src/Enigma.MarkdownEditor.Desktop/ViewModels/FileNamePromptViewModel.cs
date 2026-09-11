using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Enigma.MarkdownEditor.Core.Files;

namespace Enigma.MarkdownEditor.Desktop.ViewModels;

/// <summary>
/// The one box the tree asks a name into: a new file, a new folder, or a rename.
/// </summary>
/// <remarks>
/// <para>
/// All three are the same question — "what should this be called, in this folder?" — so they are one
/// ViewModel with three sets of labels rather than three dialogs. What differs is only the folder the
/// name has to be free in, whether it names a directory, and (for a rename) which existing name is
/// allowed to be kept.
/// </para>
/// <para>
/// The name is checked as it is typed, against the file system rather than against the tree's own
/// listing: the tree shows markdown alone by default, so a listing-based check would happily accept a
/// name that an existing <c>notes.txt</c> is already using and only fail on the create. The check is a
/// pair of existence probes on a directory the tree has just read, so the answer is in the operating
/// system's cache.
/// </para>
/// </remarks>
public sealed class FileNamePromptViewModel : ObservableObject
{
    private readonly string _parentDirectory;
    private readonly bool _isDirectory;
    private readonly string? _currentName;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileNamePromptViewModel"/> class.
    /// </summary>
    /// <param name="parentDirectory">The folder the name has to be free in.</param>
    /// <param name="isDirectory">Whether the name is for a directory.</param>
    /// <param name="initialName">What the box starts with. Empty for a new folder.</param>
    /// <param name="currentName">
    /// The name being changed, when this is a rename, so that keeping it is not reported as a clash.
    /// <see langword="null"/> when something new is being created.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="parentDirectory"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="parentDirectory"/> or <paramref name="initialName"/> is <see langword="null"/>.
    /// </exception>
    public FileNamePromptViewModel(
        string parentDirectory,
        bool isDirectory,
        string initialName,
        string? currentName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parentDirectory);
        ArgumentNullException.ThrowIfNull(initialName);

        _parentDirectory = parentDirectory;
        _isDirectory = isDirectory;
        _currentName = currentName;

        Name = initialName;

        // Explicitly, because the setter only checks a name that changed — and "" is what the field
        // already held, so a New folder dialog would otherwise open valid on an empty box.
        Revalidate();
    }

    /// <summary>
    /// Raised when the user commits the name from the keyboard rather than with the button.
    /// </summary>
    /// <remarks>
    /// The dialog's own buttons are the <c>ContentDialog</c>'s, and nothing here can press one. Enter
    /// therefore has to be reported out to whoever is showing the dialog, which closes it and reads
    /// the name — see <c>FileOperationPrompts</c>.
    /// </remarks>
    public event EventHandler? Committed;

    /// <summary>
    /// Gets or sets the name being typed. Checked on every change.
    /// </summary>
    public string Name
    {
        get;
        set
        {
            if (SetProperty(ref field, value ?? string.Empty))
            {
                Revalidate();
            }
        }
    } = string.Empty;

    /// <summary>
    /// Gets what is wrong with <see cref="Name"/>, or <see langword="null"/> when nothing is.
    /// </summary>
    public string? Error
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(HasError));
                OnPropertyChanged(nameof(IsValid));
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the box is showing a problem.
    /// </summary>
    public bool HasError => Error is not null;

    /// <summary>
    /// Gets a value indicating whether the name can be used. Drives the dialog's confirm button.
    /// </summary>
    public bool IsValid => Error is null;

    /// <summary>
    /// Gets the name the caller should act on: trimmed, because a trailing space is the one typo the
    /// file system silently swallows on Windows and keeps everywhere else.
    /// </summary>
    public string TrimmedName => Name.Trim();

    /// <summary>
    /// Reports that the user pressed Enter on a name that is usable. Does nothing otherwise, so the
    /// key cannot commit a name the confirm button is refusing.
    /// </summary>
    public void Commit()
    {
        if (IsValid)
        {
            Committed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Revalidate()
        => Error = FileNameValidation.ValidateIn(Name, _parentDirectory, _isDirectory, _currentName);
}
