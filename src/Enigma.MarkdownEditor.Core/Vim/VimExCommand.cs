namespace Enigma.MarkdownEditor.Core.Vim;

/// <summary>
/// An ex command the engine has recognised and is asking the application to carry out.
/// </summary>
/// <remarks>
/// The whole of the engine's ex line: four requests, all of them about the file rather than about
/// the text. That is why they leave the engine as a request rather than as a call on
/// <see cref="IVimBuffer"/> — saving and closing are the application's business, and the engine knows
/// nothing about tabs, dialogs or what an unsaved document is supposed to ask before it goes. The
/// two normal-mode spellings, <c>ZZ</c> and <c>ZQ</c>, raise the same requests.
/// </remarks>
public enum VimExCommand
{
    /// <summary>
    /// <c>:w</c> — save the document being edited.
    /// </summary>
    Write,

    /// <summary>
    /// <c>:q</c> — close it, through whatever the application asks before throwing work away.
    /// </summary>
    Quit,

    /// <summary>
    /// <c>:wq</c>, <c>:x</c>, <c>:wq!</c> and <c>ZZ</c> — save it, and close it only if the save
    /// worked.
    /// </summary>
    /// <remarks>
    /// One value rather than a <see cref="Write"/> followed by a <see cref="Quit"/>: the save is
    /// asynchronous, and two requests in a row would be a close racing a write of the same file.
    /// </remarks>
    WriteQuit,

    /// <summary>
    /// <c>:q!</c> and <c>ZQ</c> — close it and throw away whatever has not been saved, without
    /// asking.
    /// </summary>
    /// <remarks>
    /// A different request from <see cref="Quit"/> rather than a flag on it, because the whole
    /// point is what the application does <em>not</em> do: the unsaved-changes question is the one
    /// thing the user has said they do not want.
    /// </remarks>
    QuitDiscard,
}
