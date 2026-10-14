using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using Enigma.Avalonia.Desktop.Controls.ContentDialog;
using Enigma.Avalonia.Desktop.Services;
using Enigma.MarkdownEditor.Core.Files;
using Enigma.MarkdownEditor.Desktop.ViewModels;
using Enigma.MarkdownEditor.Desktop.Views;

namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// <see cref="IFileOperationPrompts"/> over the application's one <c>ContentDialog</c>.
/// </summary>
public sealed class FileOperationPrompts : IFileOperationPrompts
{
    /// <summary>
    /// How many names a confirmation for several entries lists before it says how many more there are.
    /// </summary>
    private const int ListedNames = 5;

    private readonly IContentDialogService _dialogService;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileOperationPrompts"/> class.
    /// </summary>
    /// <param name="dialogService">Hosts every dialog this asks through.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dialogService"/> is <see langword="null"/>.</exception>
    public FileOperationPrompts(IContentDialogService dialogService)
        => _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="directory"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="directory"/> is <see langword="null"/>.</exception>
    public Task<string?> AskForNewFileNameAsync(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        // Pre-filled with the extension alone, and the view puts the caret before it: this is a
        // markdown editor, so .md is the answer often enough to be worth typing for the user.
        return AskForNameAsync(
            "New file",
            "Create",
            new FileNamePromptViewModel(directory, isDirectory: false, ".md"));
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="directory"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="directory"/> is <see langword="null"/>.</exception>
    public Task<string?> AskForNewFolderNameAsync(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        return AskForNameAsync(
            "New folder",
            "Create",
            new FileNamePromptViewModel(directory, isDirectory: true, string.Empty));
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    public Task<string?> AskForNewNameAsync(string path, bool isDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
        string parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(path)) ?? path;

        return AskForNameAsync(
            isDirectory ? "Rename folder" : "Rename file",
            "Rename",
            new FileNamePromptViewModel(parent, isDirectory, name, currentName: name));
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">Either string is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException">Either string is <see langword="null"/>.</exception>
    public async Task<MoveResolution> AskHowToResolveAsync(string name, bool isDirectory, string targetDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDirectory);

        string what = isDirectory ? "folder" : "file";

        DialogResult result = await _dialogService.ShowAsync(dialog =>
        {
            dialog.Title = $"There is already a {what} called '{name}' here";
            dialog.Content = Message(
                $"'{targetDirectory}' already holds a {what} of that name. Overwriting it cannot be " +
                $"undone; keeping both numbers the one being moved.");

            dialog.PrimaryButtonText = "Overwrite";
            dialog.SecondaryButtonText = "Keep both";
            dialog.CloseButtonText = "Cancel";
        });

        return result switch
        {
            DialogResult.Primary => MoveResolution.Overwrite,
            DialogResult.Secondary => MoveResolution.KeepBoth,

            // Close, and None from Escape or a click on the scrim. A question about destroying
            // something has exactly one safe default, and it is not answering it.
            _ => MoveResolution.Cancel,
        };
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public Task<bool> ConfirmMoveToTrashAsync(string name, bool isDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        string what = isDirectory ? "folder and everything in it" : "file";

        return ConfirmAsync(
            $"Delete '{name}'?",
            $"The {what} will be moved to the {TrashName}, where it can be restored from.",
            "Delete");
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">
    /// <paramref name="names"/> is empty, or one of the names in it is empty or white-space.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="names"/> is <see langword="null"/>.</exception>
    public Task<bool> ConfirmMoveToTrashAsync(IReadOnlyList<string> names)
    {
        ThrowIfNoNames(names);

        return ConfirmAsync(
            names.Count == 1 ? $"Delete '{names[0]}'?" : $"Delete {names.Count} items?",
            $"These will be moved to the {TrashName}, where they can be restored from — a folder with " +
            $"everything in it.\n\n" +
            Listing(names),
            "Delete");
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public Task<bool> ConfirmPermanentDeleteAsync(string name, bool isDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        string what = isDirectory ? "folder and everything in it" : "file";

        return ConfirmAsync(
            $"Delete '{name}' permanently?",
            $"It cannot be moved to the {TrashName}, so the {what} would be deleted outright. This " +
            $"cannot be undone.",
            "Delete permanently");
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">
    /// <paramref name="names"/> is empty, or one of the names in it is empty or white-space.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="names"/> is <see langword="null"/>.</exception>
    public Task<bool> ConfirmPermanentDeleteAsync(IReadOnlyList<string> names)
    {
        ThrowIfNoNames(names);

        return ConfirmAsync(
            names.Count == 1 ? $"Delete '{names[0]}' permanently?" : $"Delete {names.Count} items permanently?",
            $"These cannot be moved to the {TrashName}, so they would be deleted outright — a folder " +
            $"with everything in it. This cannot be undone.\n\n" +
            Listing(names),
            "Delete permanently");
    }

    // What the platform calls the place a deleted entry can be fetched back from — the name the user
    // knows it by, rather than one term for every system.
    private static string TrashName => OperatingSystem.IsWindows() ? "recycle bin" : "trash";

    private static void ThrowIfNoNames(IReadOnlyList<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);

        if ((names.Count == 0) || names.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Every entry to delete needs a name, and there has to be one.", nameof(names));
        }
    }

    // Names the entries rather than counting them — a confirmation is only worth asking if the user
    // can see what they are confirming — but only the first few, so that deleting a long selection
    // does not open a dialog taller than the window.
    private static string Listing(IReadOnlyList<string> names)
    {
        IEnumerable<string> listed = names.Take(ListedNames).Select(name => $"• {name}");
        string more = names.Count > ListedNames ? $"\n…and {names.Count - ListedNames} more." : string.Empty;

        return string.Join("\n", listed) + more;
    }

    // A question about removing something from disk. No default button, and only an explicit press of
    // the primary one counts: Escape and a click on the scrim resolve as None, and a question about
    // destroying something has exactly one safe reading of no answer.
    private async Task<bool> ConfirmAsync(string title, string message, string confirmText)
    {
        DialogResult result = await _dialogService.ShowAsync(dialog =>
        {
            dialog.Title = title;
            dialog.Content = Message(message);

            dialog.PrimaryButtonText = confirmText;
            dialog.CloseButtonText = "Cancel";
        });

        return result == DialogResult.Primary;
    }

    private static TextBlock Message(string text)
        => new()
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
        };

    private async Task<string?> AskForNameAsync(string title, string confirmText, FileNamePromptViewModel prompt)
    {
        FileNamePromptView view = new()
        {
            DataContext = prompt,
        };

        // The dialog's buttons belong to the ContentDialog, so Enter cannot press one. It closes the
        // dialog instead — which resolves it as None — and this flag is what tells that dismissal
        // apart from an Escape.
        bool committedFromKeyboard = false;

        void OnCommitted(object? sender, EventArgs e)
        {
            committedFromKeyboard = true;
            _ = _dialogService.HideAsync();
        }

        prompt.Committed += OnCommitted;

        try
        {
            DialogResult result = await _dialogService.ShowAsync(dialog =>
            {
                dialog.Title = title;
                dialog.Content = view;
                dialog.PrimaryButtonText = confirmText;
                dialog.CloseButtonText = "Cancel";
                dialog.DefaultButton = DefaultButton.Primary;

                // Applied here and then followed for every keystroke, so a dialog that opens on an
                // empty box opens with its confirm button already disabled.
                dialog.IsPrimaryButtonEnabled = prompt.IsValid;

                prompt.PropertyChanged += (_, changed) =>
                {
                    if (changed.PropertyName == nameof(FileNamePromptViewModel.IsValid))
                    {
                        dialog.IsPrimaryButtonEnabled = prompt.IsValid;
                    }
                };
            });

            bool confirmed = (result == DialogResult.Primary) ||
                             ((result == DialogResult.None) && committedFromKeyboard);

            // Re-checked rather than trusted: the button state is a view concern, and the answer this
            // returns is what a file gets created with.
            return confirmed && prompt.IsValid ? prompt.TrimmedName : null;
        }
        finally
        {
            prompt.Committed -= OnCommitted;
        }
    }
}
