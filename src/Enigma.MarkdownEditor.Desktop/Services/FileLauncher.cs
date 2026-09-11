using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Enigma.MarkdownEditor.Desktop.Views;

namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// <see cref="IFileLauncher"/> over the main window's platform launcher.
/// </summary>
public sealed class FileLauncher : IFileLauncher
{
    private readonly Func<MainWindow> _mainWindow;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileLauncher"/> class.
    /// </summary>
    /// <param name="mainWindow">
    /// Resolves the window whose launcher is used. A factory rather than the window itself: the window
    /// depends on the ViewModel that depends on this service, so taking the instance here would close
    /// the cycle.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="mainWindow"/> is <see langword="null"/>.</exception>
    public FileLauncher(Func<MainWindow> mainWindow)
        => _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="filePath"/> is <see langword="null"/>.</exception>
    public Task<bool> LaunchAsync(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        return _mainWindow().Launcher.LaunchFileInfoAsync(new FileInfo(filePath));
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="address"/> is <see langword="null"/>.</exception>
    public Task<bool> LaunchUriAsync(Uri address)
    {
        ArgumentNullException.ThrowIfNull(address);

        return _mainWindow().Launcher.LaunchUriAsync(address);
    }
}
