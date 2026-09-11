using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Avalonia.Desktop.Controls.InfoBar;
using Enigma.Avalonia.Desktop.Services;
using Enigma.Configuration;
using Enigma.MarkdownEditor.Core.Files;
using Enigma.MarkdownEditor.Core.Markdown;
using Enigma.MarkdownEditor.Core.Settings;
using Microsoft.Extensions.Logging;

namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// Writes a pasted image into the note's image folder and returns the link to it.
/// </summary>
/// <remarks>
/// <para>
/// The folder is <see cref="AppSettings.ImageFolderName"/> resolved against the note's own folder, and
/// it is created on demand — a note that never has an image pasted into it never grows one. The name
/// comes from the note and the moment, and is numbered further if that name is somehow taken already,
/// so no paste can ever overwrite an earlier one.
/// </para>
/// <para>
/// Nothing here throws at the caller. A paste is a keystroke, and the only useful answer to one that
/// could not be filed is a sentence saying so — which is why every failure ends as an info bar and a
/// <see langword="null"/>.
/// </para>
/// </remarks>
public sealed class ImagePasteService : IImagePasteService
{
    private readonly IConfig<AppSettings> _settings;
    private readonly IInfoBarService _infoBarService;
    private readonly ILogger<ImagePasteService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImagePasteService"/> class.
    /// </summary>
    /// <param name="settings">Read for the image folder's name.</param>
    /// <param name="infoBarService">Tells the user about an image that could not be filed.</param>
    /// <param name="logger">Records the same failure for the log file.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public ImagePasteService(
        IConfig<AppSettings> settings,
        IInfoBarService infoBarService,
        ILogger<ImagePasteService> logger)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _infoBarService = infoBarService ?? throw new ArgumentNullException(nameof(infoBarService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="documentPath"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
    public async Task<string?> SaveAsync(
        IPastedImage image,
        string documentPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);

        cancellationToken.ThrowIfCancellationRequested();

        string full = Path.GetFullPath(documentPath);
        string? noteFolder = Path.GetDirectoryName(full);

        if (string.IsNullOrEmpty(noteFolder))
        {
            await ReportAsync("This note has no folder to file an image beside.");
            return null;
        }

        try
        {
            string imageFolder = Path.Combine(noteFolder, _settings.Value.ImageFolderName);
            Directory.CreateDirectory(imageFolder);

            string name = UniqueName.In(
                imageFolder,
                ImageLink.FileName(full, DateTimeOffset.Now),
                isDirectory: false);

            string imagePath = Path.Combine(imageFolder, name);

            // Written on this thread rather than handed to the thread pool: the bitmap comes from the
            // clipboard and belongs to the UI, and a screenshot is small enough that the write is over
            // before a frame would have been dropped.
            image.Save(imagePath);

            _logger.LogDebug("A pasted image was written to {ImagePath}.", imagePath);

            return ImageLink.Markdown(ImageLink.RelativePath(full, imagePath));
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            // Caught broadly and on purpose, as with the file launcher: encoding the bitmap goes
            // through the platform's graphics stack, whose failures are not a documented set, and a
            // paste that silently does nothing is the one outcome the user cannot act on.
            _logger.LogError(failure, "A pasted image could not be filed beside {DocumentPath}.", full);

            await ReportAsync($"{Path.GetFileName(full)} — {failure.Message}");

            return null;
        }
    }

    private Task ReportAsync(string message)
        => _infoBarService.ShowAsync(bar =>
        {
            bar.Title = "This image could not be pasted";
            bar.Message = message;
            bar.Severity = InfoBarSeverity.Error;
        });
}
