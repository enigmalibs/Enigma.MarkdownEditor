using System;
using Avalonia.Styling;
using Enigma.MarkdownEditor.Core.Settings;
using Enigma.MarkdownEditor.Desktop.Services;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Services;

/// <summary>
/// The theme mapping. <see cref="ThemeService.Apply"/> itself needs a running application, but the
/// decision it makes does not — and the interesting case is that "System" must stay dynamic.
/// </summary>
public sealed class ThemeServiceTests
{
    [Theory]
    [InlineData(AppTheme.Dark)]
    [InlineData(AppTheme.Light)]
    public void AnExplicitChoice_PinsTheMatchingVariant(AppTheme theme)
    {
        ThemeVariant variant = ThemeService.ToVariant(theme);

        Assert.Equal(theme.ToString(), variant.Key.ToString());
        Assert.NotEqual(ThemeVariant.Default, variant);
    }

    [Fact]
    public void System_BecomesTheDefaultVariantSoItKeepsFollowingTheOs()
    {
        // Not a snapshot of the current OS setting: Default is what makes the editor follow a
        // light/dark switch made while it is running.
        Assert.Equal(ThemeVariant.Default, ThemeService.ToVariant(AppTheme.System));
    }

    [Fact]
    public void AnUnknownValue_FallsBackToFollowingTheOs()
        => Assert.Equal(ThemeVariant.Default, ThemeService.ToVariant((AppTheme)99));

    [Fact]
    public void TheApplicationIsRequired()
        => Assert.Throws<ArgumentNullException>(() => new ThemeService(null!));
}
