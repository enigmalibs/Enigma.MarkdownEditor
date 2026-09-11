using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Enigma.Avalonia.Desktop.Controls.ContentDialog;
using Enigma.Avalonia.Desktop.Services;
using Enigma.MarkdownEditor.Core.Files;

namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// <see cref="IDocumentPrompts"/> over the application's one <c>ContentDialog</c> and the platform's
/// save dialog.
/// </summary>
public sealed class DocumentPrompts : IDocumentPrompts
{
    private readonly IContentDialogService _dialogService;
    private readonly IFileDialogService _fileDialogService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentPrompts"/> class.
    /// </summary>
    /// <param name="dialogService">Hosts the unsaved-changes question.</param>
    /// <param name="fileDialogService">Shows the platform's own save dialog.</param>
    /// <exception cref="ArgumentNullException">Either argument is <see langword="null"/>.</exception>
    public DocumentPrompts(IContentDialogService dialogService, IFileDialogService fileDialogService)
    {
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="fileName"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="fileName"/> is <see langword="null"/>.</exception>
    public async Task<UnsavedChangesChoice> AskAboutUnsavedChangesAsync(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        DialogResult result = await _dialogService.ShowAsync(dialog =>
        {
            dialog.Title = $"Save the changes to '{fileName}'?";
            dialog.Content = Message("Closing it without saving throws the changes away. That cannot be undone.");

            dialog.PrimaryButtonText = "Save";
            dialog.SecondaryButtonText = "Don't save";
            dialog.CloseButtonText = "Cancel";
            dialog.DefaultButton = DefaultButton.Primary;
        });

        return result switch
        {
            DialogResult.Primary => UnsavedChangesChoice.Save,
            DialogResult.Secondary => UnsavedChangesChoice.Discard,

            // Close, and None from Escape or a click on the scrim. Both other answers end the work in
            // the buffer one way or another, so a dismissal has exactly one safe reading.
            _ => UnsavedChangesChoice.Cancel,
        };
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="suggestedFileName"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="suggestedFileName"/> is <see langword="null"/>.</exception>
    public Task<string?> AskWhereToSaveAsync(string suggestedFileName, string? startDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(suggestedFileName);

        return _fileDialogService.ShowSaveFileDialogAsync(
            title: "Save as",
            suggestedStartLocation: startDirectory ?? string.Empty,
            suggestedFileName: suggestedFileName,
            defaultExtension: "md",
            showOverwritePrompt: true,
            fileTypeChoices: FileTypes);
    }

    // Markdown first because that is what this application is, then everything else so a note can be
    // saved as the .txt or .csv the tree would also have opened.
    private static IReadOnlyList<FilePickerFileType> FileTypes { get; } =
    [
        new("Markdown")
        {
            Patterns = [.. Patterns(FileTreeFilter.MarkdownExtensions)],
        },
        new("Text files")
        {
            Patterns = [.. Patterns(FileTreeFilter.TextExtensions)],
        },
        new("All files")
        {
            Patterns = ["*"],
        },
    ];

    private static IEnumerable<string> Patterns(IReadOnlySet<string> extensions)
    {
        foreach (string extension in extensions)
        {
            yield return $"*{extension}";
        }
    }

    private static TextBlock Message(string text)
        => new()
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
        };
}
