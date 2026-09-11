namespace Enigma.MarkdownEditor.Desktop.ViewModels;

/// <summary>
/// One line of the About dialog's attribution list: something this application redistributes, and the
/// licence it is redistributed under.
/// </summary>
/// <param name="Name">The component as its own project names it.</param>
/// <param name="License">Its licence, as an SPDX identifier or the licence's own name.</param>
/// <remarks>
/// The list is fixed at compile time and nothing edits a row, so a record is enough: there is no state
/// here to notify anybody about.
/// </remarks>
public sealed record CreditEntry(string Name, string License);
