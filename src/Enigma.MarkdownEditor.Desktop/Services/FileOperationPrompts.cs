using System;
using System.IO;
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
    public async Task<bool> ConfirmPermanentDeleteAsync(string name, bool isDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        string what = isDirectory ? "folder and everything in it" : "file";

        DialogResult result = await _dialogService.ShowAsync(dialog =>
        {
            dialog.Title = $"Delete '{name}' permanently?";
            dialog.Content = Message(
                $"There is no trash on this system to move it to, so the {what} would be deleted " +
                $"outright. This cannot be undone.");

            dialog.PrimaryButtonText = "Delete permanently";
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
