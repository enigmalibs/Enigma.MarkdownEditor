using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Enigma.MarkdownEditor.Desktop.ViewModels;

/// <summary>
/// One file's search hits, drawn as a group under the file's name.
/// </summary>
/// <remarks>
/// Observable where <see cref="SearchHitItem"/> is not, because a group grows: hits are reported as
/// they are found, and the count in the header has to keep up with the rows under it.
/// </remarks>
public sealed class SearchResultGroup : ObservableObject
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SearchResultGroup"/> class.
    /// </summary>
    /// <param name="filePath">The file this group is for.</param>
    /// <param name="location">
    /// The file's folder as the workspace sees it — relative to the root it was found under, and
    /// empty when it sits directly in one.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException">Either argument is <see langword="null"/>.</exception>
    public SearchResultGroup(string filePath, string location)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(location);

        FilePath = filePath;
        Location = location;
    }

    /// <summary>
    /// Gets the file's absolute path.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Gets the file's name, which is the group's heading.
    /// </summary>
    public string Name => Path.GetFileName(FilePath);

    /// <summary>
    /// Gets the folder shown beside the name: workspace-relative, and empty for a file sitting in a
    /// root itself.
    /// </summary>
    public string Location { get; }

    /// <summary>
    /// Gets a value indicating whether there is a folder worth showing.
    /// </summary>
    public bool HasLocation => Location.Length > 0;

    /// <summary>
    /// Gets the full path, for the group's tool tip — the one place the whole of it is readable.
    /// </summary>
    public string ToolTip => FilePath;

    /// <summary>
    /// Gets the hits, in the order the search reported them: the file's name first when it matched,
    /// then its lines in order.
    /// </summary>
    public ObservableCollection<SearchHitItem> Hits { get; } = [];

    /// <summary>
    /// Gets how many hits this file produced, as the header shows it.
    /// </summary>
    public string HitCountText => Hits.Count.ToString(CultureInfo.CurrentCulture);

    /// <summary>
    /// Adds a hit and tells the header about it.
    /// </summary>
    /// <param name="hit">The hit to add.</param>
    /// <exception cref="ArgumentNullException"><paramref name="hit"/> is <see langword="null"/>.</exception>
    public void Add(SearchHitItem hit)
    {
        ArgumentNullException.ThrowIfNull(hit);

        Hits.Add(hit);
        OnPropertyChanged(nameof(HitCountText));
    }
}
