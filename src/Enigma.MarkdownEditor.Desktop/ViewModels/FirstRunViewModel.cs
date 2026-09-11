using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Enigma.Configuration;
using Enigma.MarkdownEditor.Core.Settings;
using Enigma.MarkdownEditor.Core.Workspaces;
using Enigma.MarkdownEditor.Desktop.Services;
using Microsoft.Extensions.Logging;

namespace Enigma.MarkdownEditor.Desktop.ViewModels;

/// <summary>
/// State for the welcome dialog: the three things worth deciding before the editor is used for the
/// first time — vim emulation, the theme, and the folder the notes live in.
/// </summary>
/// <remarks>
/// <para>
/// There is no working copy, exactly as in <see cref="SettingsViewModel"/>: every property reads
/// and writes the live <see cref="AppSettings"/> object the container holds, so a change is visible to
/// the rest of the application at once — which is what lets the theme repaint the dialog it is being
/// chosen in. <see cref="CompleteAsync"/> then persists that same object once, on the way out.
/// </para>
/// <para>
/// Persisting is not optional housekeeping: the settings file's absence *is* the first-run signal
/// (see <see cref="FirstRunState"/>), so a dialog that closed without writing it would come back at
/// every start. That is why every way out of the dialog leads here, and why the settings write is
/// attempted even when the workspace could not be created.
/// </para>
/// </remarks>
public sealed class FirstRunViewModel : ObservableObject
{
    /// <summary>
    /// What the folder row shows until a folder has been picked.
    /// </summary>
    public const string NoFolderChosenText = "No folder chosen yet";

    private readonly IConfig<AppSettings> _config;
    private readonly IThemeService _themeService;
    private readonly IWorkspaceService _workspaces;
    private readonly IFolderPicker _folderPicker;
    private readonly ILogger<FirstRunViewModel> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FirstRunViewModel"/> class.
    /// </summary>
    /// <param name="config">The live settings, read and mutated in place.</param>
    /// <param name="themeService">Applies a theme change without waiting for the save.</param>
    /// <param name="workspaces">Creates the workspace over the chosen folder.</param>
    /// <param name="folderPicker">Asks for that folder.</param>
    /// <param name="logger">Records what the first run decided.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public FirstRunViewModel(
        IConfig<AppSettings> config,
        IThemeService themeService,
        IWorkspaceService workspaces,
        IFolderPicker folderPicker,
        ILogger<FirstRunViewModel> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        _workspaces = workspaces ?? throw new ArgumentNullException(nameof(workspaces));
        _folderPicker = folderPicker ?? throw new ArgumentNullException(nameof(folderPicker));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        ChooseFolderCommand = new AsyncRelayCommand(ChooseFolderAsync);
    }

    private AppSettings Settings => _config.Value;

    /// <summary>
    /// Gets the theme choices, in the order the picker lists them — the settings dialog's own list.
    /// </summary>
    public IReadOnlyList<AppTheme> Themes { get; } = [AppTheme.System, AppTheme.Dark, AppTheme.Light];

    /// <summary>
    /// Gets or sets the theme variant. Applied to the running application as it is set, so the dialog
    /// repaints in the theme being chosen.
    /// </summary>
    public AppTheme Theme
    {
        get => Settings.Theme;
        set
        {
            if (SetProperty(Settings.Theme, value, Settings, static (settings, theme) => settings.Theme = theme))
            {
                _themeService.Apply(value);
            }
        }
    }

    /// <summary>
    /// Gets or sets whether vim emulation is on. Read by the editor pane once the dialog is done, so
    /// the choice reaches the session that is already starting.
    /// </summary>
    public bool VimEnabled
    {
        get => Settings.VimEnabled;
        set => SetProperty(Settings.VimEnabled, value, Settings, static (settings, on) => settings.VimEnabled = on);
    }

    /// <summary>
    /// Gets the folder the first workspace will be created over, or <see langword="null"/> when none
    /// has been chosen.
    /// </summary>
    /// <remarks>
    /// Set only by <see cref="ChooseFolderCommand"/>: the value has to be a folder that exists on this
    /// machine, and a box to type one into is a box to mistype one into.
    /// </remarks>
    public string? WorkspaceFolder
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(FolderText));
                OnPropertyChanged(nameof(HasWorkspaceFolder));
            }
        }
    }

    /// <summary>
    /// Gets the chosen folder as the row shows it, or the empty state.
    /// </summary>
    public string FolderText => WorkspaceFolder ?? NoFolderChosenText;

    /// <summary>
    /// Gets a value indicating whether a folder has been chosen.
    /// </summary>
    public bool HasWorkspaceFolder => WorkspaceFolder is not null;

    /// <summary>
    /// Gets what went wrong picking a folder, or <see langword="null"/> when nothing did.
    /// </summary>
    /// <remarks>
    /// Inline, like the manage-workspaces dialog's own error bar: the picker is raised from inside this
    /// dialog, so its failure has to be shown inside it as well — a <c>ContentDialog</c> cannot stack a
    /// second one on top of itself. Everything <see cref="CompleteAsync"/> can fail at is reported
    /// after the dialog has closed instead.
    /// </remarks>
    public string? ErrorMessage
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the inline error line is shown.
    /// </summary>
    public bool HasError => ErrorMessage is not null;

    /// <summary>
    /// Gets the command that asks for the folder the notes live in.
    /// </summary>
    public IAsyncRelayCommand ChooseFolderCommand { get; }

    /// <summary>
    /// Applies everything the dialog decided: the workspace, then the settings file.
    /// </summary>
    /// <param name="cancellationToken">Cancels the writes.</param>
    /// <returns>
    /// <see langword="null"/> when everything was applied; otherwise a message to show the user,
    /// explaining what was not.
    /// </returns>
    /// <remarks>
    /// Called on every way out of the dialog — the primary button, the close button, <c>Escape</c> and
    /// a click on the scrim — because writing the settings file is what stops the dialog coming back.
    /// The two steps are therefore independent: a workspace that could not be created is reported, but
    /// it does not cost the user the rest of their answers.
    /// </remarks>
    public async Task<string?> CompleteAsync(CancellationToken cancellationToken = default)
    {
        string? workspaceFailure = await CreateWorkspaceAsync(cancellationToken);
        string? settingsFailure = await SaveSettingsAsync(cancellationToken);

        _logger.LogInformation(
            "First run finished: vim emulation {VimEnabled}, theme {Theme}, workspace added {WorkspaceAdded}.",
            Settings.VimEnabled,
            Settings.Theme,
            (WorkspaceFolder is not null) && (workspaceFailure is null));

        return Combine(workspaceFailure, settingsFailure);
    }

    private static string? Combine(string? first, string? second)
    {
        if (first is null)
        {
            return second;
        }

        return second is null
            ? first
            : $"{first}{Environment.NewLine}{Environment.NewLine}{second}";
    }

    private async Task ChooseFolderAsync()
    {
        try
        {
            ErrorMessage = null;

            if (await _folderPicker.PickAsync("Choose the folder your notes live in") is { } folder)
            {
                WorkspaceFolder = folder;
            }
        }
        catch (Exception failure)
        {
            // Caught broadly and on purpose: an AsyncRelayCommand parks a faulted task rather than
            // letting the exception escape, so without this a picker that cannot open would leave the
            // button looking simply dead — on the user's very first screen.
            _logger.LogError(failure, "The folder picker could not be opened.");
            ErrorMessage = $"The folder picker could not be opened. {failure.Message}";
        }
    }

    private async Task<string?> CreateWorkspaceAsync(CancellationToken cancellationToken)
    {
        if (WorkspaceFolder is not { } folder)
        {
            return null;
        }

        try
        {
            // The same name suggestion and the same validation the manage-workspaces dialog uses:
            // there is no second path into the workspace list.
            await _workspaces.CreateAsync(
                WorkspaceValidation.SuggestName(folder, _workspaces.Workspaces),
                folder,
                cancellationToken);

            return null;
        }
        catch (WorkspaceValidationException invalid)
        {
            // Expected: a rule the chosen folder broke, already phrased for the user.
            _logger.LogWarning(invalid, "The first workspace could not be created over {Folder}.", folder);

            return invalid.Message;
        }
        catch (Exception failure)
        {
            // As broad as the manage dialog's own catch, and for the same reason: whatever the
            // workspaces file did, the editor is running, and the user has to be told rather than have
            // the answer disappear into a faulted task.
            _logger.LogError(failure, "The first workspace could not be created over {Folder}.", folder);

            return $"'{folder}' could not be added as a workspace: {failure.Message}";
        }
    }

    private async Task<string?> SaveSettingsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _config.WriteAsync(cancellationToken);
            _logger.LogInformation("Settings written to {SettingsFile}.", _config.FilePath);

            return null;
        }
        catch (ConfigurationValidationException invalid)
        {
            _logger.LogWarning(invalid, "Settings were rejected by validation; the file was left untouched.");

            return "These values were rejected, so nothing was written:" +
                   Environment.NewLine +
                   string.Join(Environment.NewLine, invalid.Errors.Select(error => $"  • {error}"));
        }
        catch (Exception failure)
            when (failure is EnigmaConfigurationException or IOException or UnauthorizedAccessException)
        {
            _logger.LogError(failure, "Settings could not be written to {SettingsFile}.", _config.FilePath);

            return $"{_config.FilePath} could not be written: {failure.Message}";
        }
    }
}
