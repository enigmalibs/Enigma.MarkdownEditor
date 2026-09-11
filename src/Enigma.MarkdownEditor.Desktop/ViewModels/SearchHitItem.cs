using System;
using System.Globalization;
using System.Windows.Input;
using Enigma.MarkdownEditor.Core.Search;

namespace Enigma.MarkdownEditor.Desktop.ViewModels;

/// <summary>
/// One search hit as the result list draws it: the context line cut into what comes before the match,
/// the match, and what comes after.
/// </summary>
/// <remarks>
/// A snapshot, like <see cref="WorkspaceListItem"/> and for the same reason: a hit describes a file as
/// it was when it was read, and nothing edits it in place — a new search replaces the whole list.
/// </remarks>
public sealed class SearchHitItem
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SearchHitItem"/> class.
    /// </summary>
    /// <param name="hit">The hit this row shows.</param>
    /// <param name="openCommand">
    /// The command that opens it, taking this row as its parameter, or <see langword="null"/> in a
    /// list that opens nothing.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="hit"/> is <see langword="null"/>.</exception>
    public SearchHitItem(WorkspaceSearchHit hit, ICommand? openCommand = null)
    {
        ArgumentNullException.ThrowIfNull(hit);

        Model = hit;
        OpenCommand = openCommand;

        // Cut once, here, rather than by three bindings that would each re-derive the same indices.
        Prefix = hit.LineText[..hit.MatchStart];
        Match = hit.LineText.Substring(hit.MatchStart, hit.MatchLength);
        Suffix = hit.LineText[(hit.MatchStart + hit.MatchLength)..];
    }

    /// <summary>
    /// Gets the hit behind the row.
    /// </summary>
    public WorkspaceSearchHit Model { get; }

    /// <summary>
    /// Gets the file the hit is in.
    /// </summary>
    public string FilePath => Model.FilePath;

    /// <summary>
    /// Gets where in the file to land, and what to select once there.
    /// </summary>
    public DocumentSelection Selection => new(Model.Line, Model.Column, Model.MatchLength);

    /// <summary>
    /// Gets the line number shown at the left of the row. Empty for a hit on the file's name, which
    /// belongs to no line.
    /// </summary>
    public string LineLabel => IsFileName
        ? string.Empty
        : Model.Line.ToString(CultureInfo.CurrentCulture);

    /// <summary>
    /// Gets a value indicating whether the file's own name is what matched.
    /// </summary>
    public bool IsFileName => Model.Kind == WorkspaceSearchHitKind.FileName;

    /// <summary>
    /// Gets the part of the context line before the match.
    /// </summary>
    public string Prefix { get; }

    /// <summary>
    /// Gets the matched text itself, which the row draws highlighted.
    /// </summary>
    public string Match { get; }

    /// <summary>
    /// Gets the part of the context line after the match.
    /// </summary>
    public string Suffix { get; }

    /// <summary>
    /// Gets the command that opens this hit, or <see langword="null"/> when the list opens nothing.
    /// </summary>
    public ICommand? OpenCommand { get; }
}
