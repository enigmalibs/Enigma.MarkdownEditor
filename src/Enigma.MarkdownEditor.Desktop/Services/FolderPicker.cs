using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Enigma.Avalonia.Desktop.Services;

namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// The folder picker, over Enigma's <see cref="IFolderDialogService"/>.
/// </summary>
public sealed class FolderPicker : IFolderPicker
{
    private readonly IFolderDialogService _folderDialogService;

    /// <summary>
    /// Initializes a new instance of the <see cref="FolderPicker"/> class.
    /// </summary>
    /// <param name="folderDialogService">The picker, which needs the main window's storage provider.</param>
    /// <exception cref="ArgumentNullException"><paramref name="folderDialogService"/> is <see langword="null"/>.</exception>
    public FolderPicker(IFolderDialogService folderDialogService)
        => _folderDialogService = folderDialogService ?? throw new ArgumentNullException(nameof(folderDialogService));

    /// <inheritdoc />
    public async Task<string?> PickAsync(string title)
    {
        // The string-path overload drops anything the platform gave no local path for, so an empty
        // sequence means either "dismissed" or "picked something that is not a folder on this disk".
        // Both are "nothing to add" as far as a workspace is concerned.
        IEnumerable<string> picked = await _folderDialogService.ShowOpenFolderDialogAsync(title);

        return picked.FirstOrDefault();
    }
}
