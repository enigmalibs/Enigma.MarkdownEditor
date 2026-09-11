using System;
using Avalonia;
using Avalonia.Styling;
using Enigma.MarkdownEditor.Core.Settings;

namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// Drives <see cref="Application.RequestedThemeVariant"/> from the persisted <see cref="AppTheme"/>.
/// </summary>
public sealed class ThemeService : IThemeService
{
    private readonly Application _application;

    /// <summary>
    /// Initializes a new instance of the <see cref="ThemeService"/> class.
    /// </summary>
    /// <param name="application">The running application whose variant is switched.</param>
    /// <exception cref="ArgumentNullException"><paramref name="application"/> is <see langword="null"/>.</exception>
    public ThemeService(Application application)
        => _application = application ?? throw new ArgumentNullException(nameof(application));

    /// <summary>
    /// Maps a persisted theme choice onto the Avalonia variant that expresses it.
    /// </summary>
    /// <param name="theme">The persisted choice.</param>
    /// <returns>The matching <see cref="ThemeVariant"/>.</returns>
    /// <remarks>
    /// <see cref="AppTheme.System"/> becomes <see cref="ThemeVariant.Default"/> rather than a snapshot
    /// of the current OS setting: <c>Default</c> keeps following the OS while the app runs, so a user
    /// switching Windows to dark mode sees the editor follow without touching anything.
    /// </remarks>
    public static ThemeVariant ToVariant(AppTheme theme) => theme switch
    {
        AppTheme.Dark => ThemeVariant.Dark,
        AppTheme.Light => ThemeVariant.Light,
        _ => ThemeVariant.Default,
    };

    /// <inheritdoc />
    public void Apply(AppTheme theme) => _application.RequestedThemeVariant = ToVariant(theme);
}
