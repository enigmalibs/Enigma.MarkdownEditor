using System.Threading.Tasks;

namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// Asks the user for one folder.
/// </summary>
/// <remarks>
/// A one-method view of <c>IFolderDialogService</c>, which answers in Avalonia storage items. Those
/// cannot be implemented outside Avalonia — the interface carries a marker that makes the compiler
/// refuse — so a ViewModel that depends on the picker directly cannot be tested against a picked
/// folder at all. This is the seam that fixes that, and it is also the shape every caller here wants:
/// a path, or nothing because the dialog was dismissed.
/// </remarks>
public interface IFolderPicker
{
    /// <summary>
    /// Shows the folder picker.
    /// </summary>
    /// <param name="title">The dialog's title, saying what the folder is for.</param>
    /// <returns>The chosen folder's path, or <see langword="null"/> when the dialog was dismissed.</returns>
    Task<string?> PickAsync(string title);
}
