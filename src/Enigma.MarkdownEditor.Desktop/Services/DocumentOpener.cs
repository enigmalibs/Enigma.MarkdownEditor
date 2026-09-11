using System;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Avalonia.Desktop.Controls.InfoBar;
using Enigma.Avalonia.Desktop.Services;
using Enigma.Configuration;
using Enigma.MarkdownEditor.Core.Files;
using Enigma.MarkdownEditor.Core.Settings;
using Enigma.MarkdownEditor.Desktop.ViewModels;
using Microsoft.Extensions.Logging;

namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// Routes a file the tree activated: markdown and — while the tree is showing them — other text files
/// go to the editor pane, everything else to the operating system.
/// </summary>
public sealed class DocumentOpener : IDocumentOpener
{
    private readonly DocumentManagerViewModel _documents;
    private readonly IFileLauncher _launcher;
    private readonly IConfig<AppSettings> _settings;
    private readonly IInfoBarService _infoBarService;
    private readonly ILogger<DocumentOpener> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentOpener"/> class.
    /// </summary>
    /// <param name="documents">The editor pane, which opens the file in a tab or activates its tab.</param>
    /// <param name="launcher">Hands everything else to the operating system.</param>
    /// <param name="settings">Read for ShowAllFiles, which widens what the editor takes.</param>
    /// <param name="infoBarService">Reports a file that could not be opened at all.</param>
    /// <param name="logger">Records the same failure for the log file.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public DocumentOpener(
        DocumentManagerViewModel documents,
        IFileLauncher launcher,
        IConfig<AppSettings> settings,
        IInfoBarService infoBarService,
        ILogger<DocumentOpener> logger)
    {
        _documents = documents ?? throw new ArgumentNullException(nameof(documents));
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _infoBarService = infoBarService ?? throw new ArgumentNullException(nameof(infoBarService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="filePath"/> is <see langword="null"/>.</exception>
    public async Task OpenAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (FileTreeFilter.OpensInEditor(filePath, _settings.Value.ShowAllFiles))
        {
            await _documents.OpenAsync(filePath, cancellationToken);
            return;
        }

        try
        {
            if (await _launcher.LaunchAsync(filePath))
            {
                _logger.LogDebug("{FilePath} was handed to the operating system.", filePath);
                return;
            }

            await ReportAsync(filePath, "No application is registered for this kind of file.");
        }
        catch (Exception failure)
        {
            // Caught broadly and on purpose: the launcher goes through platform code whose failures
            // are not a documented set, and a double-click that silently does nothing is the one
            // outcome the user cannot act on.
            _logger.LogError(failure, "{FilePath} could not be handed to the operating system.", filePath);

            await ReportAsync(filePath, $"{failure.GetType().Name}: {failure.Message}");
        }
    }

    private Task ReportAsync(string filePath, string message)
        => _infoBarService.ShowAsync(bar =>
        {
            bar.Title = "This file could not be opened";
            bar.Message = $"{filePath} — {message}";
            bar.Severity = InfoBarSeverity.Warning;
        });
}
