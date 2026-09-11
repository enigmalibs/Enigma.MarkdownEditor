using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Enigma.Configuration;
using Enigma.MarkdownEditor.Core.Settings;
using Enigma.MarkdownEditor.Desktop.Services;
using Microsoft.Extensions.Logging;

namespace Enigma.MarkdownEditor.Desktop.ViewModels;

/// <summary>
/// State for the settings dialog: one property per <see cref="AppSettings"/> member, edited in place.
/// </summary>
/// <remarks>
/// <para>
/// There is no working copy. Every property reads and writes the live <see cref="AppSettings"/>
/// object the container holds, so a change is visible to the rest of the application immediately —
/// which is what lets the theme switch live. <see cref="SaveAsync"/> then persists that same object
/// once, after the dialog has closed.
/// </para>
/// <para>
/// Settings whose feature has not been built yet are still shown, disabled, by the view: the list of
/// what the editor will do is more useful than a settings dialog with two rows in it.
/// </para>
/// </remarks>
public sealed class SettingsViewModel : ObservableObject
{
    private readonly IConfig<AppSettings> _config;
    private readonly IThemeService _themeService;
    private readonly IFontCatalog _fontCatalog;
    private readonly ILogger<SettingsViewModel> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsViewModel"/> class.
    /// </summary>
    /// <param name="config">The live settings, read and mutated in place.</param>
    /// <param name="themeService">Applies a theme change without waiting for the save.</param>
    /// <param name="fontCatalog">Supplies the font pickers' rows and says what "automatic" resolves to.</param>
    /// <param name="logger">Records what was saved, and what was rejected.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public SettingsViewModel(
        IConfig<AppSettings> config,
        IThemeService themeService,
        IFontCatalog fontCatalog,
        ILogger<SettingsViewModel> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        _fontCatalog = fontCatalog ?? throw new ArgumentNullException(nameof(fontCatalog));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        EditorFontChoices = BuildFontChoices(
            FontChoice.Automatic(_fontCatalog.DefaultMonospaceFamily),
            _fontCatalog.MonospaceFamilies,
            EditorFontFamily);

        PreviewFontChoices = BuildFontChoices(
            FontChoice.AutomaticReading(_fontCatalog.SystemDefaultFamily),
            _fontCatalog.AllFamilies,
            PreviewFontFamily);

        PreviewCodeFontChoices = BuildFontChoices(
            FontChoice.Automatic(_fontCatalog.DefaultMonospaceFamily),
            _fontCatalog.MonospaceFamilies,
            PreviewCodeFontFamily);
    }

    private AppSettings Settings => _config.Value;

    /// <summary>
    /// Gets the theme choices, in the order the picker lists them.
    /// </summary>
    public IReadOnlyList<AppTheme> Themes { get; } = [AppTheme.System, AppTheme.Dark, AppTheme.Light];

    /// <summary>
    /// Gets or sets the theme variant. Applied to the running application as it is set.
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
    /// Gets or sets whether vim emulation is on. Read by the editor pane when this window closes, so
    /// switching it reaches the document that is already open.
    /// </summary>
    public bool VimEnabled
    {
        get => Settings.VimEnabled;
        set => SetProperty(Settings.VimEnabled, value, Settings, static (settings, on) => settings.VimEnabled = on);
    }

    /// <summary>
    /// Gets or sets whether modified documents are written back automatically. No consumer until FEATURE-3C44.
    /// </summary>
    public bool AutosaveEnabled
    {
        get => Settings.AutosaveEnabled;
        set => SetProperty(Settings.AutosaveEnabled, value, Settings, static (settings, on) => settings.AutosaveEnabled = on);
    }

    /// <summary>
    /// Gets or sets the idle delay before an autosave runs. No consumer until FEATURE-3C44.
    /// </summary>
    public int? AutosaveDelaySeconds
    {
        get => Settings.AutosaveDelaySeconds;
        set => SetProperty(
            Settings.AutosaveDelaySeconds,
            value ?? Settings.AutosaveDelaySeconds,
            Settings,
            static (settings, seconds) => settings.AutosaveDelaySeconds = seconds);
    }

    /// <summary>
    /// Gets or sets whether the workspace tree lists every file. No consumer until FEATURE-0DE1.
    /// </summary>
    public bool ShowAllFiles
    {
        get => Settings.ShowAllFiles;
        set => SetProperty(Settings.ShowAllFiles, value, Settings, static (settings, on) => settings.ShowAllFiles = on);
    }

    /// <summary>
    /// Gets the rows the editor's font picker offers: "automatic" first, then every installed
    /// monospace family, plus the stored family when this machine does not have it.
    /// </summary>
    /// <remarks>
    /// Built once, in the constructor, as are the two preview pickers below. This ViewModel is
    /// transient and built fresh on every visit to settings, so the list is as current as the fonts
    /// were when it was opened — and no font is installed or removed while the dialog is up.
    /// </remarks>
    public IReadOnlyList<FontChoice> EditorFontChoices { get; }

    /// <summary>
    /// Gets or sets the picker's selected row, which is <see cref="EditorFontFamily"/> seen as one of
    /// <see cref="EditorFontChoices"/>.
    /// </summary>
    /// <remarks>
    /// Selecting nothing is ignored rather than treated as "automatic": a <c>ComboBox</c> whose
    /// <c>SelectedItem</c> momentarily goes null while its items are being applied would otherwise
    /// overwrite the stored family on the way to showing it.
    /// </remarks>
    public FontChoice? EditorFontChoice
    {
        get => Selected(EditorFontChoices, EditorFontFamily);
        set
        {
            if (value is not null)
            {
                EditorFontFamily = value.Family;
            }
        }
    }

    /// <summary>
    /// Gets or sets the editor's font family, or the empty string for "the best monospace font this
    /// machine has". Read by the editor pane when this window closes.
    /// </summary>
    public string EditorFontFamily
    {
        get => Settings.EditorFontFamily ?? string.Empty;
        set
        {
            if (SetProperty(
                Settings.EditorFontFamily,
                value ?? string.Empty,
                Settings,
                static (settings, family) => settings.EditorFontFamily = family))
            {
                OnPropertyChanged(nameof(EditorFontChoice));
            }
        }
    }

    /// <summary>
    /// Gets or sets the editor's font size. No consumer until FEATURE-3C44.
    /// </summary>
    public int? EditorFontSize
    {
        get => Settings.EditorFontSize;
        set => SetProperty(
            Settings.EditorFontSize,
            value ?? Settings.EditorFontSize,
            Settings,
            static (settings, size) => settings.EditorFontSize = size);
    }

    /// <summary>
    /// Gets the rows the preview's prose picker offers: every installed family, not only the
    /// fixed-width ones, because a document is read in whatever reads well.
    /// </summary>
    public IReadOnlyList<FontChoice> PreviewFontChoices { get; }

    /// <inheritdoc cref="EditorFontChoice"/>
    public FontChoice? PreviewFontChoice
    {
        get => Selected(PreviewFontChoices, PreviewFontFamily);
        set
        {
            if (value is not null)
            {
                PreviewFontFamily = value.Family;
            }
        }
    }

    /// <summary>
    /// Gets or sets the family the preview sets prose in, or the empty string for "the default
    /// reading font". Read by the preview pane when this window closes.
    /// </summary>
    public string PreviewFontFamily
    {
        get => Settings.PreviewFontFamily ?? string.Empty;
        set
        {
            if (SetProperty(
                Settings.PreviewFontFamily,
                value ?? string.Empty,
                Settings,
                static (settings, family) => settings.PreviewFontFamily = family))
            {
                OnPropertyChanged(nameof(PreviewFontChoice));
            }
        }
    }

    /// <summary>
    /// Gets or sets the size the preview sets prose at. Read by the preview pane when this window
    /// closes.
    /// </summary>
    public int? PreviewFontSize
    {
        get => Settings.PreviewFontSize;
        set => SetProperty(
            Settings.PreviewFontSize,
            value ?? Settings.PreviewFontSize,
            Settings,
            static (settings, size) => settings.PreviewFontSize = size);
    }

    /// <summary>
    /// Gets the rows the preview's code picker offers — the fixed-width families, like the editor's.
    /// </summary>
    public IReadOnlyList<FontChoice> PreviewCodeFontChoices { get; }

    /// <inheritdoc cref="EditorFontChoice"/>
    public FontChoice? PreviewCodeFontChoice
    {
        get => Selected(PreviewCodeFontChoices, PreviewCodeFontFamily);
        set
        {
            if (value is not null)
            {
                PreviewCodeFontFamily = value.Family;
            }
        }
    }

    /// <summary>
    /// Gets or sets the family the preview sets code in, or the empty string for "the best monospace
    /// font this machine has". Read by the preview pane when this window closes.
    /// </summary>
    public string PreviewCodeFontFamily
    {
        get => Settings.PreviewCodeFontFamily ?? string.Empty;
        set
        {
            if (SetProperty(
                Settings.PreviewCodeFontFamily,
                value ?? string.Empty,
                Settings,
                static (settings, family) => settings.PreviewCodeFontFamily = family))
            {
                OnPropertyChanged(nameof(PreviewCodeFontChoice));
            }
        }
    }

    /// <summary>
    /// Gets or sets the size the preview sets code at — spans and fenced blocks alike. Read by the
    /// preview pane when this window closes.
    /// </summary>
    public int? PreviewCodeFontSize
    {
        get => Settings.PreviewCodeFontSize;
        set => SetProperty(
            Settings.PreviewCodeFontSize,
            value ?? Settings.PreviewCodeFontSize,
            Settings,
            static (settings, size) => settings.PreviewCodeFontSize = size);
    }

    /// <summary>
    /// Gets or sets how far apart the preview's lines sit, as a multiple of the size they are set at.
    /// Read by the preview pane when this window closes.
    /// </summary>
    public double? PreviewLineHeight
    {
        get => Settings.PreviewLineHeight;
        set => SetProperty(
            Settings.PreviewLineHeight,
            value ?? Settings.PreviewLineHeight,
            Settings,
            static (settings, height) => settings.PreviewLineHeight = height);
    }

    /// <summary>
    /// Gets or sets whether the editor wraps long lines. No consumer until FEATURE-3C44.
    /// </summary>
    public bool WordWrap
    {
        get => Settings.WordWrap;
        set => SetProperty(Settings.WordWrap, value, Settings, static (settings, on) => settings.WordWrap = on);
    }

    /// <summary>
    /// Gets or sets the folder pasted images are written into. No consumer until FEATURE-3C44.
    /// </summary>
    public string ImageFolderName
    {
        get => Settings.ImageFolderName;
        set => SetProperty(
            Settings.ImageFolderName,
            value ?? string.Empty,
            Settings,
            static (settings, folder) => settings.ImageFolderName = folder);
    }

    /// <summary>
    /// Gets or sets whether the log file records <c>Debug</c> entries. Takes effect on the next start.
    /// </summary>
    public bool DebugLogging
    {
        get => Settings.DebugLogging;
        set => SetProperty(Settings.DebugLogging, value, Settings, static (settings, on) => settings.DebugLogging = on);
    }

    /// <summary>
    /// Gets the settings file this window writes to.
    /// </summary>
    public string SettingsFilePath => _config.FilePath;

    /// <summary>
    /// Gets the folder the log files are written to.
    /// </summary>
    public string LogsFolderPath => AppPaths.LogsFolder;

    /// <summary>
    /// Writes the settings back to their file.
    /// </summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>
    /// <see langword="null"/> when the file was written; otherwise a message to show the user,
    /// explaining why it was not.
    /// </returns>
    public async Task<string?> SaveAsync(CancellationToken cancellationToken = default)
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

    private static FontChoice? Selected(IEnumerable<FontChoice> choices, string family)
        => choices.FirstOrDefault(
            choice => string.Equals(choice.Family, family, StringComparison.OrdinalIgnoreCase));

    private List<FontChoice> BuildFontChoices(
        FontChoice automatic,
        IEnumerable<string> offered,
        string storedFamily)
    {
        List<FontChoice> choices = [automatic];

        // The stored family, when this machine does not have it, goes directly under "automatic" —
        // where it is both visible and already selected, so closing the window leaves it exactly as
        // the file holds it. Dropping it would silently rewrite the setting to whatever the picker
        // happened to land on, and a settings file is carried between machines.
        if ((storedFamily.Length > 0) && !_fontCatalog.IsAvailable(storedFamily))
        {
            choices.Add(FontChoice.Missing(storedFamily, _fontCatalog.SystemDefaultFamily));
        }

        choices.AddRange(offered.Select(FontChoice.Installed));

        return choices;
    }
}
