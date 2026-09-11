using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Enigma.Configuration;
using Enigma.MarkdownEditor.Core.Settings;
using Enigma.MarkdownEditor.Desktop.Services;
using Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;
using Enigma.MarkdownEditor.Desktop.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.ViewModels;

/// <summary>
/// Tests for the settings ViewModel, over a real settings file in a temp folder. The point of the
/// design under test is that there is no working copy — the ViewModel edits the live settings object
/// — so a fake configuration would test the wrong thing.
/// </summary>
public sealed class SettingsViewModelTests : IDisposable
{
    private readonly string _root;
    private readonly string _settingsFile;

    public SettingsViewModelTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "Enigma.MarkdownEditor.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _settingsFile = Path.Combine(_root, AppPaths.SettingsFileName);
    }

    [Fact]
    public void EveryPropertyStartsOnTheCurrentSettings()
    {
        IConfig<AppSettings> config = Load();
        config.Value.Theme = AppTheme.Dark;
        config.Value.EditorFontSize = 20;

        SettingsViewModel viewModel = Create(config);

        Assert.Equal(AppTheme.Dark, viewModel.Theme);
        Assert.Equal(20, viewModel.EditorFontSize);
        Assert.Empty(viewModel.EditorFontFamily);
    }

    [Fact]
    public void SettingAProperty_MutatesTheLiveSettingsObject()
    {
        IConfig<AppSettings> config = Load();
        SettingsViewModel viewModel = Create(config);

        viewModel.VimEnabled = false;
        viewModel.ImageFolderName = "pictures";
        viewModel.DebugLogging = true;

        // Not a copy: everything already holding this AppSettings sees the change at once.
        Assert.False(config.Value.VimEnabled);
        Assert.Equal("pictures", config.Value.ImageFolderName);
        Assert.True(config.Value.DebugLogging);
    }

    [Fact]
    public void SettingTheTheme_AppliesItImmediatelyRatherThanWaitingForTheSave()
    {
        FakeThemeService theme = new();
        SettingsViewModel viewModel = Create(Load(), theme);

        viewModel.Theme = AppTheme.Light;

        Assert.Equal([AppTheme.Light], theme.Applied);
    }

    [Fact]
    public void SettingTheThemeToWhatItAlreadyIs_DoesNotReapplyIt()
    {
        FakeThemeService theme = new();
        SettingsViewModel viewModel = Create(Load(), theme);

        viewModel.Theme = viewModel.Theme;

        Assert.Empty(theme.Applied);
    }

    [Fact]
    public void ClearingANumericEditor_KeepsThePreviousValue()
    {
        IConfig<AppSettings> config = Load();
        SettingsViewModel viewModel = Create(config);

        // IntEditor.Value is int?, and an empty box means "no number", not "zero" — which the
        // validator would reject on the way out.
        viewModel.EditorFontSize = null;

        Assert.Equal(new AppSettings().EditorFontSize, config.Value.EditorFontSize);
    }

    [Fact]
    public void TheFontPicker_OffersAutomaticFirstAndThenEveryInstalledMonospaceFamily()
    {
        SettingsViewModel viewModel = Create(Load());

        FontChoice automatic = viewModel.EditorFontChoices[0];

        Assert.True(automatic.IsAutomatic);
        Assert.Empty(automatic.Family);
        Assert.Equal(FontChoice.AutomaticLabel, automatic.Label);

        // Drawn in the font it resolves to, which is how the row says which font that is.
        Assert.Equal(TestFonts.Catalog().DefaultMonospaceFamily, automatic.PreviewFamily);

        // Only fixed-width families behind it, each drawn in itself.
        Assert.Equal(
            TestFonts.Monospace.Order(StringComparer.OrdinalIgnoreCase),
            viewModel.EditorFontChoices.Skip(1).Select(choice => choice.Family));
        Assert.All(
            viewModel.EditorFontChoices.Skip(1),
            choice => Assert.Equal(choice.Family, choice.PreviewFamily));
        Assert.DoesNotContain(TestFonts.UiFamily, viewModel.EditorFontChoices.Select(choice => choice.Family));
    }

    [Fact]
    public void AFreshSettingsFile_StartsOnAutomatic()
    {
        SettingsViewModel viewModel = Create(Load());

        Assert.Same(viewModel.EditorFontChoices[0], viewModel.EditorFontChoice);
    }

    [Fact]
    public void AStoredFamilyThisMachineDoesNotHave_IsShownMarkedAndSelected()
    {
        IConfig<AppSettings> config = Load();

        // The comma stack an older settings file holds. As a family name it is simply not installed,
        // which is the same case as a font from another machine.
        const string Stack = "Cascadia Mono, Consolas, DejaVu Sans Mono, Liberation Mono, monospace";
        config.Value.EditorFontFamily = Stack;

        SettingsViewModel viewModel = Create(config);
        FontChoice missing = viewModel.EditorFontChoices[1];

        Assert.Equal(Stack, missing.Family);
        Assert.Equal(Stack + " (not installed)", missing.Label);

        // It cannot be drawn in itself, so it is drawn in the interface font.
        Assert.Equal(TestFonts.UiFamily, missing.PreviewFamily);

        // Selected, which is what keeps closing the window from replacing it.
        Assert.Same(missing, viewModel.EditorFontChoice);
        Assert.Equal(Stack, config.Value.EditorFontFamily);
    }

    [Fact]
    public void AnInstalledStoredFamily_GetsNoExtraRow()
    {
        IConfig<AppSettings> config = Load();
        config.Value.EditorFontFamily = TestFonts.Monospace[0];

        SettingsViewModel viewModel = Create(config);

        Assert.Equal(TestFonts.Monospace.Length + 1, viewModel.EditorFontChoices.Count);
        Assert.All(viewModel.EditorFontChoices, choice => Assert.DoesNotContain("not installed", choice.Label));
        Assert.Equal(TestFonts.Monospace[0], viewModel.EditorFontChoice?.Family);
    }

    [Fact]
    public void PickingAFont_WritesTheFamilyNameAndNothingElse()
    {
        IConfig<AppSettings> config = Load();
        SettingsViewModel viewModel = Create(config);

        viewModel.EditorFontChoice = viewModel.EditorFontChoices.First(choice => !choice.IsAutomatic);

        Assert.Equal(viewModel.EditorFontChoice.Family, config.Value.EditorFontFamily);
        Assert.DoesNotContain(",", config.Value.EditorFontFamily);
    }

    [Fact]
    public void PickingAutomatic_ClearsTheStoredFamily()
    {
        IConfig<AppSettings> config = Load();
        config.Value.EditorFontFamily = TestFonts.Monospace[0];

        SettingsViewModel viewModel = Create(config);
        viewModel.EditorFontChoice = viewModel.EditorFontChoices[0];

        Assert.Empty(config.Value.EditorFontFamily);
    }

    [Fact]
    public void TheComboBoxMomentarilySelectingNothing_LeavesTheStoredFamilyAlone()
    {
        IConfig<AppSettings> config = Load();
        config.Value.EditorFontFamily = TestFonts.NotInstalled;

        SettingsViewModel viewModel = Create(config);
        viewModel.EditorFontChoice = null;

        // A ComboBox whose SelectedItem goes null while its items are applied must not be able to
        // overwrite the setting on the way to showing it.
        Assert.Equal(TestFonts.NotInstalled, config.Value.EditorFontFamily);
    }

    [Fact]
    public void ThePreviewProsePicker_OffersEveryInstalledFamilyRatherThanTheFixedWidthOnes()
    {
        SettingsViewModel viewModel = Create(Load());

        FontChoice automatic = viewModel.PreviewFontChoices[0];

        Assert.True(automatic.IsAutomatic);

        // Its own wording: what automatic means here is the font this machine reads in, not the best
        // code font it has.
        Assert.Equal(FontChoice.AutomaticReadingLabel, automatic.Label);
        Assert.Equal(TestFonts.UiFamily, automatic.PreviewFamily);

        // A note is read in whatever reads well, so the proportional families are in the list — which
        // is exactly what the editor's own picker leaves out.
        Assert.Equal(
            TestFonts.Monospace.Concat(TestFonts.Proportional).Order(StringComparer.OrdinalIgnoreCase),
            viewModel.PreviewFontChoices.Skip(1).Select(choice => choice.Family));
        Assert.All(
            viewModel.PreviewFontChoices.Skip(1),
            choice => Assert.Equal(choice.Family, choice.PreviewFamily));

        Assert.Same(viewModel.PreviewFontChoices[0], viewModel.PreviewFontChoice);
    }

    [Fact]
    public void ThePreviewCodePicker_OffersTheFixedWidthFamiliesLikeTheEditorsOwn()
    {
        SettingsViewModel viewModel = Create(Load());

        Assert.Equal(FontChoice.AutomaticLabel, viewModel.PreviewCodeFontChoices[0].Label);
        Assert.Equal(
            TestFonts.Monospace.Order(StringComparer.OrdinalIgnoreCase),
            viewModel.PreviewCodeFontChoices.Skip(1).Select(choice => choice.Family));
        Assert.DoesNotContain(
            TestFonts.UiFamily,
            viewModel.PreviewCodeFontChoices.Select(choice => choice.Family));
    }

    [Fact]
    public void APreviewFamilyThisMachineDoesNotHave_IsShownMarkedAndSelectedInBothPickers()
    {
        IConfig<AppSettings> config = Load();
        config.Value.PreviewFontFamily = TestFonts.NotInstalled;
        config.Value.PreviewCodeFontFamily = TestFonts.NotInstalled;

        SettingsViewModel viewModel = Create(config);

        Assert.Same(viewModel.PreviewFontChoices[1], viewModel.PreviewFontChoice);
        Assert.Same(viewModel.PreviewCodeFontChoices[1], viewModel.PreviewCodeFontChoice);
        Assert.Equal(TestFonts.NotInstalled + " (not installed)", viewModel.PreviewFontChoice?.Label);

        // Untouched by having been shown, so closing the window cannot replace it.
        Assert.Equal(TestFonts.NotInstalled, config.Value.PreviewFontFamily);
        Assert.Equal(TestFonts.NotInstalled, config.Value.PreviewCodeFontFamily);
    }

    [Fact]
    public void PickingPreviewFonts_WritesTheFamilyNamesAndAutomaticClearsThem()
    {
        IConfig<AppSettings> config = Load();
        SettingsViewModel viewModel = Create(config);

        viewModel.PreviewFontChoice = viewModel.PreviewFontChoices.First(choice => !choice.IsAutomatic);
        viewModel.PreviewCodeFontChoice =
            viewModel.PreviewCodeFontChoices.First(choice => !choice.IsAutomatic);

        Assert.Equal(viewModel.PreviewFontChoice.Family, config.Value.PreviewFontFamily);
        Assert.Equal(viewModel.PreviewCodeFontChoice.Family, config.Value.PreviewCodeFontFamily);

        viewModel.PreviewFontChoice = viewModel.PreviewFontChoices[0];
        viewModel.PreviewCodeFontChoice = viewModel.PreviewCodeFontChoices[0];

        Assert.Empty(config.Value.PreviewFontFamily);
        Assert.Empty(config.Value.PreviewCodeFontFamily);
    }

    [Fact]
    public void APreviewComboBoxMomentarilySelectingNothing_LeavesTheStoredFamilyAlone()
    {
        IConfig<AppSettings> config = Load();
        config.Value.PreviewFontFamily = TestFonts.NotInstalled;
        config.Value.PreviewCodeFontFamily = TestFonts.NotInstalled;

        SettingsViewModel viewModel = Create(config);
        viewModel.PreviewFontChoice = null;
        viewModel.PreviewCodeFontChoice = null;

        Assert.Equal(TestFonts.NotInstalled, config.Value.PreviewFontFamily);
        Assert.Equal(TestFonts.NotInstalled, config.Value.PreviewCodeFontFamily);
    }

    [Fact]
    public void ClearingTheNumericPreviewEditors_KeepsWhatWasThere()
    {
        IConfig<AppSettings> config = Load();
        config.Value.PreviewFontSize = 24;
        config.Value.PreviewCodeFontSize = 16;
        config.Value.PreviewLineHeight = 1.8;

        SettingsViewModel viewModel = Create(config);

        // An empty editor means "no number", not zero — which the range would reject on the way out.
        viewModel.PreviewFontSize = null;
        viewModel.PreviewCodeFontSize = null;
        viewModel.PreviewLineHeight = null;

        Assert.Equal(24, config.Value.PreviewFontSize);
        Assert.Equal(16, config.Value.PreviewCodeFontSize);
        Assert.Equal(1.8, config.Value.PreviewLineHeight);
    }

    [Fact]
    public void TheCodeFontSize_ReadsAndWritesItsOwnSettingRatherThanTheProseOne()
    {
        IConfig<AppSettings> config = Load();
        config.Value.PreviewFontSize = 24;
        config.Value.PreviewCodeFontSize = 16;

        SettingsViewModel viewModel = Create(config);

        Assert.Equal(16, viewModel.PreviewCodeFontSize);

        viewModel.PreviewCodeFontSize = 11;

        Assert.Equal(11, config.Value.PreviewCodeFontSize);
        Assert.Equal(24, config.Value.PreviewFontSize);
    }

    [Fact]
    public async Task SaveAsync_WritesTheFileAndReportsNoFailure()
    {
        IConfig<AppSettings> config = Load();
        SettingsViewModel viewModel = Create(config);
        viewModel.Theme = AppTheme.Dark;

        string? failure = await viewModel.SaveAsync(TestContext.Current.CancellationToken);

        Assert.Null(failure);
        Assert.Equal(AppTheme.Dark, SettingsLoader.Load(_settingsFile).Config.Value.Theme);
    }

    [Fact]
    public async Task SaveAsync_ReportsARejectedValueAndLeavesTheFileAlone()
    {
        IConfig<AppSettings> config = Load();
        SettingsViewModel viewModel = Create(config);
        viewModel.ImageFolderName = "nested/folder";

        string? failure = await viewModel.SaveAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(failure);
        Assert.Contains(nameof(AppSettings.ImageFolderName), failure);
        Assert.Contains("single folder name", failure);
        Assert.False(File.Exists(_settingsFile));
    }

    [Fact]
    public void TheThemeChoices_AreTheThreeAppThemes()
        => Assert.Equal([AppTheme.System, AppTheme.Dark, AppTheme.Light], Create(Load()).Themes);

    [Fact]
    public void ThePathsShown_AreTheOnesActuallyInUse()
    {
        SettingsViewModel viewModel = Create(Load());

        Assert.Equal(_settingsFile, viewModel.SettingsFilePath);
        Assert.Equal(AppPaths.LogsFolder, viewModel.LogsFolderPath);
    }

    [Fact]
    public void EveryDependency_IsRequired()
    {
        IConfig<AppSettings> config = Load();
        FakeThemeService theme = new();
        FontCatalog fonts = TestFonts.Catalog();

        Assert.Throws<ArgumentNullException>(
            () => new SettingsViewModel(null!, theme, fonts, NullLogger<SettingsViewModel>.Instance));
        Assert.Throws<ArgumentNullException>(
            () => new SettingsViewModel(config, null!, fonts, NullLogger<SettingsViewModel>.Instance));
        Assert.Throws<ArgumentNullException>(
            () => new SettingsViewModel(config, theme, null!, NullLogger<SettingsViewModel>.Instance));
        Assert.Throws<ArgumentNullException>(
            () => new SettingsViewModel(config, theme, fonts, null!));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A leftover temp folder is not worth failing a green test over.
        }
    }

    private IConfig<AppSettings> Load() => SettingsLoader.Load(_settingsFile).Config;

    private static SettingsViewModel Create(IConfig<AppSettings> config, FakeThemeService? theme = null)
        => new(
            config,
            theme ?? new FakeThemeService(),
            TestFonts.Catalog(),
            NullLogger<SettingsViewModel>.Instance);
}
