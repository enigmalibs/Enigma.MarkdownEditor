using System;
using System.Threading.Tasks;

namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// Hands a file to the operating system's default application for it.
/// </summary>
/// <remarks>
/// A seam for the same reason <see cref="IFolderPicker"/> is one: the real implementation needs a
/// window, and a unit test has none.
/// </remarks>
public interface IFileLauncher
{
    /// <summary>
    /// Opens a file with whatever the operating system considers its default application.
    /// </summary>
    /// <param name="filePath">The file to open.</param>
    /// <returns>
    /// <see langword="true"/> when the platform accepted it. <see langword="false"/> when it declined —
    /// typically because nothing is registered for that file type.
    /// </returns>
    Task<bool> LaunchAsync(string filePath);

    /// <summary>
    /// Opens an address with whatever the operating system has registered for its scheme — the
    /// browser for <c>http</c> and <c>https</c>, the mail client for <c>mailto</c>.
    /// </summary>
    /// <param name="address">The address to open.</param>
    /// <returns>
    /// <see langword="true"/> when the platform accepted it. <see langword="false"/> when it declined —
    /// typically because nothing handles that scheme.
    /// </returns>
    Task<bool> LaunchUriAsync(Uri address);
}
