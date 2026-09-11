using System;
using System.IO;
using System.Threading.Tasks;
using Enigma.Configuration;
using Enigma.MarkdownEditor.Core.Settings;
using Enigma.MarkdownEditor.Core.Workspaces;
using Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;
using Enigma.MarkdownEditor.Desktop.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.ViewModels;

/// <summary>
/// Tests for the welcome dialog's ViewModel, over a real settings file in a temp folder — for the same
/// reason <see cref="SettingsViewModelTests"/> uses one: the design under test is that there is
/// no working copy, so a fake configuration would test the wrong thing.
/// </summary>
public sealed class FirstRunViewModelTests : IDisposable
{
    private readonly string _root;
    private readonly string _settingsFile;

    public FirstRunViewModelTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "Enigma.MarkdownEditor.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _settingsFile = Path.Combine(_root, AppPaths.SettingsFileName);
    }

    [Fact]
    public void EveryPropertyStartsOnTheApplicationsDefaults()
    {
        FirstRunViewModel viewModel = Create(Load());

        // The defaults the dialog offers are the application's own, not a second list written down
        // here: someone who presses "Get started" without touching anything gets what they would have
        // had anyway.
        Assert.True(viewModel.VimEnabled);
        Assert.Equal(AppTheme.System, viewModel.Theme);
        Assert.Null(viewModel.WorkspaceFolder);
        Assert.False(viewModel.HasWorkspaceFolder);
        Assert.Equal(FirstRunViewModel.NoFolderChosenText, viewModel.FolderText);
    }

    [Fact]
    public void TheThemeChoices_AreTheSettingsDialogsOwnThree()
        => Assert.Equal([AppTheme.System, AppTheme.Dark, AppTheme.Light], Create(Load()).Themes);

    [Fact]
    public void SettingVimAndTheTheme_MutatesTheLiveSettingsObject()
    {
        IConfig<AppSettings> config = Load();
        FirstRunViewModel viewModel = Create(config);

        viewModel.VimEnabled = false;
        viewModel.Theme = AppTheme.Dark;

        // Not a copy: everything already holding this AppSettings sees the change at once, which is
        // what lets the dialog's own answers reach the editor without a restart.
        Assert.False(config.Value.VimEnabled);
        Assert.Equal(AppTheme.Dark, config.Value.Theme);
    }

    [Fact]
    public void SettingTheTheme_AppliesItImmediatelyRatherThanWaitingForTheSave()
    {
        FakeThemeService theme = new();
        FirstRunViewModel viewModel = Create(Load(), theme: theme);

        viewModel.Theme = AppTheme.Light;

        // So the dialog repaints in the theme being chosen, which is the only way to choose one.
        Assert.Equal([AppTheme.Light], theme.Applied);
    }

    [Fact]
    public void SettingTheThemeToWhatItAlreadyIs_DoesNotReapplyIt()
    {
        FakeThemeService theme = new();
        FirstRunViewModel viewModel = Create(Load(), theme: theme);

        viewModel.Theme = viewModel.Theme;

        Assert.Empty(theme.Applied);
    }

    [Fact]
    public async Task ChoosingAFolder_ShowsItAsTheOneToBeUsed()
    {
        FakeFolderPicker picker = new() { Picked = _root };
        FirstRunViewModel viewModel = Create(Load(), picker: picker);

        await viewModel.ChooseFolderCommand.ExecuteAsync(null);

        Assert.Equal(_root, viewModel.WorkspaceFolder);
        Assert.True(viewModel.HasWorkspaceFolder);
        Assert.Equal(_root, viewModel.FolderText);
        Assert.Equal(1, picker.PickCount);
    }

    [Fact]
    public async Task DismissingTheFolderPicker_LeavesTheRowEmpty()
    {
        FirstRunViewModel viewModel = Create(Load(), picker: new FakeFolderPicker { Picked = null });

        await viewModel.ChooseFolderCommand.ExecuteAsync(null);

        Assert.Null(viewModel.WorkspaceFolder);
        Assert.Equal(FirstRunViewModel.NoFolderChosenText, viewModel.FolderText);
    }

    [Fact]
    public async Task AFolderPickerThatCannotOpen_IsReportedInTheDialogRatherThanSwallowed()
    {
        FirstRunViewModel viewModel = Create(
            Load(),
            picker: new FakeFolderPicker { Failure = new InvalidOperationException("no storage provider") });

        // An AsyncRelayCommand parks a faulted task instead of rethrowing, so without the ViewModel's
        // own catch the button would simply appear dead — on the user's very first screen.
        await viewModel.ChooseFolderCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasError);
        Assert.NotNull(viewModel.ErrorMessage);
        Assert.Contains("no storage provider", viewModel.ErrorMessage);
    }

    [Fact]
    public async Task CompleteAsync_WritesTheSettingsFileSoTheDialogNeverComesBack()
    {
        IConfig<AppSettings> config = Load();
        FirstRunViewModel viewModel = Create(config);
        viewModel.VimEnabled = false;
        viewModel.Theme = AppTheme.Dark;

        string? failure = await viewModel.CompleteAsync(TestContext.Current.CancellationToken);

        Assert.Null(failure);

        // The file's existence *is* the first-run signal, so this assertion is the "never nags" half of
        // the design as much as it is a persistence check.
        Assert.True(File.Exists(_settingsFile));

        AppSettings written = SettingsLoader.Load(_settingsFile).Config.Value;
        Assert.False(written.VimEnabled);
        Assert.Equal(AppTheme.Dark, written.Theme);
    }

    [Fact]
    public async Task CompleteAsync_CreatesTheWorkspaceOverTheChosenFolder()
    {
        FakeWorkspaceService workspaces = new();
        FirstRunViewModel viewModel = Create(
            Load(),
            workspaces: workspaces,
            picker: new FakeFolderPicker { Picked = _root });

        await viewModel.ChooseFolderCommand.ExecuteAsync(null);
        string? failure = await viewModel.CompleteAsync(TestContext.Current.CancellationToken);

        Assert.Null(failure);

        Workspace created = Assert.Single(workspaces.Workspaces);
        Assert.Equal(WorkspaceValidation.DirectoryName(_root), created.Name);
        Assert.Equal([_root], created.RootDirectories);

        // And it is the open one, so the tree has something to show the moment the dialog closes.
        Assert.Same(created, workspaces.ActiveWorkspace);
    }

    [Fact]
    public async Task CompleteAsync_AddsNoWorkspaceWhenNoFolderWasChosen()
    {
        FakeWorkspaceService workspaces = new();
        FirstRunViewModel viewModel = Create(Load(), workspaces: workspaces);

        string? failure = await viewModel.CompleteAsync(TestContext.Current.CancellationToken);

        // Choosing no folder is a legitimate answer: the editor opens to its existing empty state, and
        // the dialog is still done with.
        Assert.Null(failure);
        Assert.Empty(workspaces.Workspaces);
        Assert.True(File.Exists(_settingsFile));
    }

    [Fact]
    public async Task CompleteAsync_ReportsARejectedSettingsValueRatherThanThrowing()
    {
        IConfig<AppSettings> config = Load();

        // Not something the dialog itself can produce — it edits two properties, both enumerable. It
        // stands in for the general case: the settings object is shared, and a write can be refused.
        config.Value.ImageFolderName = "nested/folder";

        string? failure = await Create(config).CompleteAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(failure);
        Assert.Contains(nameof(AppSettings.ImageFolderName), failure);
        Assert.False(File.Exists(_settingsFile));
    }

    [Fact]
    public async Task CompleteAsync_ReportsAWorkspaceThatCouldNotBeCreatedAndStillWritesTheSettings()
    {
        FakeWorkspaceService workspaces = new() { Failure = new IOException("workspaces.json is read-only") };
        FirstRunViewModel viewModel = Create(
            Load(),
            workspaces: workspaces,
            picker: new FakeFolderPicker { Picked = _root });

        await viewModel.ChooseFolderCommand.ExecuteAsync(null);
        string? failure = await viewModel.CompleteAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(failure);
        Assert.Contains("read-only", failure);

        // The settings write is not conditional on the workspace: it is what stops the dialog coming
        // back, and a folder that could not be added is not a reason to ask everything again.
        Assert.True(File.Exists(_settingsFile));
    }

    [Fact]
    public void EveryDependency_IsRequired()
    {
        IConfig<AppSettings> config = Load();
        FakeThemeService theme = new();
        FakeWorkspaceService workspaces = new();
        FakeFolderPicker picker = new();
        NullLogger<FirstRunViewModel> logger = NullLogger<FirstRunViewModel>.Instance;

        Assert.Throws<ArgumentNullException>(() => new FirstRunViewModel(null!, theme, workspaces, picker, logger));
        Assert.Throws<ArgumentNullException>(() => new FirstRunViewModel(config, null!, workspaces, picker, logger));
        Assert.Throws<ArgumentNullException>(() => new FirstRunViewModel(config, theme, null!, picker, logger));
        Assert.Throws<ArgumentNullException>(() => new FirstRunViewModel(config, theme, workspaces, null!, logger));
        Assert.Throws<ArgumentNullException>(() => new FirstRunViewModel(config, theme, workspaces, picker, null!));
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

    private static FirstRunViewModel Create(
        IConfig<AppSettings> config,
        FakeThemeService? theme = null,
        FakeWorkspaceService? workspaces = null,
        FakeFolderPicker? picker = null)
        => new(
            config,
            theme ?? new FakeThemeService(),
            workspaces ?? new FakeWorkspaceService(),
            picker ?? new FakeFolderPicker(),
            NullLogger<FirstRunViewModel>.Instance);
}
