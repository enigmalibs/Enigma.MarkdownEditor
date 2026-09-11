using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Enigma.Avalonia.Desktop.Controls.InfoBar;
using Enigma.Avalonia.Desktop.Services;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;

/// <summary>
/// Records what the shell asked to be shown.
/// </summary>
/// <remarks>
/// The <c>configure</c> delegates are kept rather than invoked: applying one needs a real
/// <see cref="InfoBar"/> control, and therefore an initialized Avalonia application. What each
/// delegate would have written is covered where it is decided — see the
/// <c>SettingsStartupNoticeTests</c>.
/// </remarks>
internal sealed class FakeInfoBarService : IInfoBarService
{
    private readonly List<Action<InfoBar>?> _shown = [];

    public IReadOnlyList<Action<InfoBar>?> Shown => _shown;

    public int HideCount { get; private set; }

    public void RegisterHost(InfoBar infoBar)
    {
    }

    public Task ShowAsync(Action<InfoBar>? configure = null)
    {
        _shown.Add(configure);
        return Task.CompletedTask;
    }

    public Task HideAsync()
    {
        HideCount++;
        return Task.CompletedTask;
    }
}
