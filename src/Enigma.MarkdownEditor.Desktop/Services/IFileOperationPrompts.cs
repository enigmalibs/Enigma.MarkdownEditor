using System.Collections.Generic;
using System.Threading.Tasks;
using Enigma.MarkdownEditor.Core.Files;

namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// Everything a file operation has to ask the user before it can go ahead.
/// </summary>
/// <remarks>
/// A seam for the same reason <see cref="IFolderPicker"/> is one: every method here ends in a
/// <c>ContentDialog</c>, which needs a registered host and therefore a running application — and the
/// tree's operations are worth testing without one. Behind this interface the answers come from the
/// user; in a test they come from a fake, and the ViewModel cannot tell.
/// </remarks>
public interface IFileOperationPrompts
{
    /// <summary>
    /// Asks for the name of a new file, checking it as it is typed.
    /// </summary>
    /// <param name="directory">The folder the file would be created in.</param>
    /// <returns>The name, or <see langword="null"/> when the user backed out.</returns>
    Task<string?> AskForNewFileNameAsync(string directory);

    /// <summary>
    /// Asks for the name of a new folder, checking it as it is typed.
    /// </summary>
    /// <param name="directory">The folder the new one would be created in.</param>
    /// <returns>The name, or <see langword="null"/> when the user backed out.</returns>
    Task<string?> AskForNewFolderNameAsync(string directory);

    /// <summary>
    /// Asks what an existing entry should be called instead, checking it as it is typed.
    /// </summary>
    /// <param name="path">The entry being renamed.</param>
    /// <param name="isDirectory">Whether it is a directory.</param>
    /// <returns>
    /// The new name, or <see langword="null"/> when the user backed out. Keeping the current name
    /// counts as a name, not as backing out — the caller treats it as the no-op it is.
    /// </returns>
    Task<string?> AskForNewNameAsync(string path, bool isDirectory);

    /// <summary>
    /// Asks what to do about a name the target folder is already using.
    /// </summary>
    /// <param name="name">The name that is taken.</param>
    /// <param name="isDirectory">Whether the entry being moved is a directory.</param>
    /// <param name="targetDirectory">The folder it is being moved into.</param>
    /// <returns>
    /// What the user chose. Dismissing the dialog is <see cref="MoveResolution.Cancel"/> — the only
    /// safe reading of a question about overwriting something.
    /// </returns>
    Task<MoveResolution> AskHowToResolveAsync(string name, bool isDirectory, string targetDirectory);

    /// <summary>
    /// Asks whether to delete an entry outright, because this platform has no trash to move it to.
    /// </summary>
    /// <param name="name">What is about to be deleted.</param>
    /// <param name="isDirectory">Whether it is a directory.</param>
    /// <returns><see langword="true"/> only when the user explicitly confirmed.</returns>
    /// <remarks>
    /// The one delete that asks. A trashed entry can be fetched back out of the recycle bin, so
    /// confirming it would be a click the user learns to ignore; this one cannot be undone at all.
    /// </remarks>
    Task<bool> ConfirmPermanentDeleteAsync(string name, bool isDirectory);

    /// <summary>
    /// Asks, once for all of them, whether to delete several entries outright, because none of them
    /// can go to the trash.
    /// </summary>
    /// <param name="names">What is about to be deleted, in the order it was selected.</param>
    /// <returns><see langword="true"/> only when the user explicitly confirmed.</returns>
    /// <remarks>
    /// One question for a whole selection rather than one per entry: a dialog per file is the kind a
    /// user learns to click through, which is the last thing a delete that cannot be undone should
    /// teach.
    /// </remarks>
    Task<bool> ConfirmPermanentDeleteAsync(IReadOnlyList<string> names);
}
