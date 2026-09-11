using System.Collections.Generic;
using Enigma.MarkdownEditor.Core.Settings;
using Enigma.MarkdownEditor.Desktop.Services;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;

/// <summary>
/// Records the theme changes that were applied, in order. The real service assigns to a running
/// <c>Application</c>, which a unit test does not have.
/// </summary>
internal sealed class FakeThemeService : IThemeService
{
    private readonly List<AppTheme> _applied = [];

    public IReadOnlyList<AppTheme> Applied => _applied;

    public void Apply(AppTheme theme) => _applied.Add(theme);
}
