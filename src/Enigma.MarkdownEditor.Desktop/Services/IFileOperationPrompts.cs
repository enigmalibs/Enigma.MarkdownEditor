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
    /// Asks whether to move an entry to the trash.
    /// </summary>
    /// <param name="name">What is about to be deleted.</param>
    /// <param name="isDirectory">Whether it is a directory.</param>
    /// <returns><see langword="true"/> only when the user explicitly confirmed.</returns>
    /// <remarks>
    /// Every delete asks before anything leaves the disk, the recoverable ones included. This is the
    /// question asked first whenever there is a trash; <see cref="ConfirmPermanentDeleteAsync(string, bool)"/>
    /// is asked instead of it when there is none, and after it for whatever the trash then declines.
    /// </remarks>
    Task<bool> ConfirmMoveToTrashAsync(string name, bool isDirectory);

    /// <summary>
    /// Asks, once for all of them, whether to move several entries to the trash.
    /// </summary>
    /// <param name="names">What is about to be deleted, in the order it was selected.</param>
    /// <returns><see langword="true"/> only when the user explicitly confirmed.</returns>
    /// <remarks>
    /// One question for a whole selection rather than one per entry: a dialog per file is the kind a
    /// user learns to click through, which is the last thing a confirmation should teach.
    /// </remarks>
    Task<bool> ConfirmMoveToTrashAsync(IReadOnlyList<string> names);

    /// <summary>
    /// Asks whether to delete an entry outright, because it cannot be moved to the trash.
    /// </summary>
    /// <param name="name">What is about to be deleted.</param>
    /// <param name="isDirectory">Whether it is a directory.</param>
    /// <returns><see langword="true"/> only when the user explicitly confirmed.</returns>
    /// <remarks>
    /// Asked instead of <see cref="ConfirmMoveToTrashAsync(string, bool)"/> when the platform has no
    /// trash, so that a delete asks once rather than twice. When the trash is there but declines the
    /// entry, it is asked as well, after the trash question: agreeing to a delete that can be undone
    /// is not agreeing to one that cannot.
    /// </remarks>
    Task<bool> ConfirmPermanentDeleteAsync(string name, bool isDirectory);

    /// <summary>
    /// Asks, once for all of them, whether to delete several entries outright, because none of them
    /// can go to the trash.
    /// </summary>
    /// <param name="names">What is about to be deleted, in the order it was selected.</param>
    /// <returns><see langword="true"/> only when the user explicitly confirmed.</returns>
    /// <remarks>
    /// One question for a whole selection, for the reason <see cref="ConfirmMoveToTrashAsync(IReadOnlyList{string})"/>
    /// gives — and all the more so for a delete that cannot be undone.
    /// </remarks>
    Task<bool> ConfirmPermanentDeleteAsync(IReadOnlyList<string> names);
}
