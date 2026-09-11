using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Enigma.Configuration;
using Enigma.MarkdownEditor.Core;
using Enigma.MarkdownEditor.Core.Session;
using Enigma.MarkdownEditor.Core.Settings;
using Enigma.MarkdownEditor.Core.Vim;
using Enigma.MarkdownEditor.Desktop.Services;
using Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;
using Enigma.MarkdownEditor.Desktop.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.ViewModels;

/// <summary>
/// Tests for the shell ViewModel: the pane flags and the status-bar slots — including that a no-op
/// assignment stays silent, since the view rewrites grid column widths on every notification — plus
/// the two things it now drives, opening the settings window and surfacing a start-up notice.
/// </summary>
public sealed class MainWindowViewModelTests
{
    [Fact]
    public void TheShell_HostsTheWorkspaceSwitcher()
        => Assert.NotNull(Create().Workspaces);

    [Fact]
    public void Title_IsTheProductDisplayName()
        => Assert.Equal(AppInfo.DisplayName, Create().Title);

    [Fact]
    public void EveryPane_IsVisibleByDefault()
    {
        MainWindowViewModel viewModel = Create();

        Assert.True(viewModel.IsSidebarVisible);
        Assert.Equal(ViewMode.Split, viewModel.ViewMode);
        Assert.True(viewModel.IsEditorVisible);
        Assert.True(viewModel.IsPreviewVisible);
    }

    [Fact]
    public void StatusSlots_StartOnTheirPlaceholders()
    {
        MainWindowViewModel viewModel = Create();

        Assert.Equal("NORMAL", viewModel.VimModeText);
        Assert.Equal("Ln 1, Col 1", viewModel.CaretPositionText);
        Assert.Equal("No file open", viewModel.FileInfoText);
    }

    [Fact]
    public void HidingTheSidebar_NotifiesOnceAndIgnoresARepeat()
        => AssertPaneTogglesAndNotifiesOnce(
            nameof(MainWindowViewModel.IsSidebarVisible),
            viewModel => viewModel.IsSidebarVisible,
            (viewModel, value) => viewModel.IsSidebarVisible = value);

    [Theory]
    [InlineData(ViewMode.EditorOnly, true, false)]
    [InlineData(ViewMode.Split, true, true)]
    [InlineData(ViewMode.PreviewOnly, false, true)]
    public void EachViewMode_DecidesWhichPanesAreShown(ViewMode mode, bool editor, bool preview)
    {
        MainWindowViewModel viewModel = Create();

        viewModel.ViewMode = mode;

        Assert.Equal(editor, viewModel.IsEditorVisible);
        Assert.Equal(preview, viewModel.IsPreviewVisible);

        // Never both hidden, whatever the mode: that is the arrangement the enum exists to rule out.
        Assert.True(viewModel.IsEditorVisible || viewModel.IsPreviewVisible);
    }

    [Fact]
    public void ChangingTheViewMode_NotifiesEveryFlagTheViewReads()
    {
        MainWindowViewModel viewModel = Create();
        List<string?> raised = [];
        viewModel.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        viewModel.ViewMode = ViewMode.PreviewOnly;

        // The view rewrites grid column widths off these, so a mode change that stayed quiet would
        // hide a pane and leave its column at full width.
        Assert.Contains(nameof(MainWindowViewModel.ViewMode), raised);
        Assert.Contains(nameof(MainWindowViewModel.IsEditorVisible), raised);
        Assert.Contains(nameof(MainWindowViewModel.IsPreviewVisible), raised);
        Assert.Contains(nameof(MainWindowViewModel.IsEditorOnly), raised);
        Assert.Contains(nameof(MainWindowViewModel.IsSplit), raised);
        Assert.Contains(nameof(MainWindowViewModel.IsPreviewOnly), raised);
    }

    [Fact]
    public void SettingTheSameViewMode_StaysSilent()
    {
        MainWindowViewModel viewModel = Create();
        List<string?> raised = [];
        viewModel.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        viewModel.ViewMode = ViewMode.Split;

        Assert.Empty(raised);
    }

    [Fact]
    public void TheShortcut_CyclesTheThreeModesAndWrapsRound()
    {
        MainWindowViewModel viewModel = Create();

        viewModel.CycleViewModeCommand.Execute(null);
        Assert.Equal(ViewMode.PreviewOnly, viewModel.ViewMode);

        viewModel.CycleViewModeCommand.Execute(null);
        Assert.Equal(ViewMode.EditorOnly, viewModel.ViewMode);

        viewModel.CycleViewModeCommand.Execute(null);
        Assert.Equal(ViewMode.Split, viewModel.ViewMode);
    }

    [Fact]
    public void AToolbarSegment_SelectsItsModeAndReportsTheOthersOff()
    {
        MainWindowViewModel viewModel = Create();

        viewModel.IsPreviewOnly = true;

        Assert.Equal(ViewMode.PreviewOnly, viewModel.ViewMode);
        Assert.True(viewModel.IsPreviewOnly);
        Assert.False(viewModel.IsSplit);
        Assert.False(viewModel.IsEditorOnly);
    }

    [Fact]
    public void SwitchingTheActiveSegmentOff_LeavesTheModeAloneAndPutsTheButtonBack()
    {
        MainWindowViewModel viewModel = Create();
        List<string?> raised = [];
        viewModel.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        // A toggle can be unchecked, and this one's mode is already the active one. Refusing means
        // saying so, or the button stays down in the ViewModel and up on screen.
        viewModel.IsSplit = false;

        Assert.Equal(ViewMode.Split, viewModel.ViewMode);
        Assert.True(viewModel.IsSplit);
        Assert.Equal([nameof(MainWindowViewModel.IsSplit)], raised);
    }

    [Fact]
    public void TheSidebar_StartsOnTheExplorer()
    {
        MainWindowViewModel viewModel = Create();

        Assert.Equal(SidebarTab.Explorer, viewModel.SidebarTab);
        Assert.True(viewModel.IsExplorerTab);
        Assert.False(viewModel.IsSearchTab);
    }

    [Fact]
    public void ASidebarSegment_SelectsItsTabAndReportsTheOtherOff()
    {
        MainWindowViewModel viewModel = Create();
        List<string?> raised = [];
        viewModel.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        viewModel.IsSearchTab = true;

        Assert.Equal(SidebarTab.Search, viewModel.SidebarTab);
        Assert.False(viewModel.IsExplorerTab);

        // Both flags, because the view draws both segments off the pair.
        Assert.Contains(nameof(MainWindowViewModel.IsExplorerTab), raised);
        Assert.Contains(nameof(MainWindowViewModel.IsSearchTab), raised);
    }

    [Fact]
    public void SwitchingTheActiveSidebarTabOff_LeavesItAloneAndPutsTheButtonBack()
    {
        MainWindowViewModel viewModel = Create();
        List<string?> raised = [];
        viewModel.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        // The same refusal the view modes make, for the same reason: a sidebar showing neither tab is
        // not an arrangement the pair can express.
        viewModel.IsExplorerTab = false;

        Assert.Equal(SidebarTab.Explorer, viewModel.SidebarTab);
        Assert.True(viewModel.IsExplorerTab);
        Assert.Equal([nameof(MainWindowViewModel.IsExplorerTab)], raised);
    }

    [Fact]
    public void TheShell_HostsTheSearchTab()
        => Assert.NotNull(Create().Search);

    [Fact]
    public void TheVimSlots_MirrorTheEditorPane()
    {
        using EditorHarness harness = new();
        MainWindowViewModel viewModel = Create(harness: harness);
        List<string?> raised = [];
        viewModel.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        harness.Vim.ReportMode(VimMode.Insert);

        // The status bar is the shell's, but the mode belongs to the editor: the shell only repeats it.
        Assert.Equal("INSERT", viewModel.VimModeText);
        Assert.True(viewModel.IsVimInsertMode);
        Assert.Contains(nameof(MainWindowViewModel.VimModeText), raised);
        Assert.Contains(nameof(MainWindowViewModel.IsVimInsertMode), raised);
    }

    [Fact]
    public void AMessageFromTheVimEngine_ReachesTheStatusBar()
    {
        using EditorHarness harness = new();
        MainWindowViewModel viewModel = Create(harness: harness);

        harness.Vim.ReportStatus("Not a command: gx");

        Assert.Equal("Not a command: gx", viewModel.VimStatusText);
    }

    [Fact]
    public void TheCommandLine_ReachesTheStatusBarAndTakesTheSlotWhileItIsUp()
    {
        using EditorHarness harness = new();
        MainWindowViewModel viewModel = Create(harness: harness);
        List<string?> raised = [];
        viewModel.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        Assert.False(viewModel.IsVimCommandLineVisible);

        harness.Vim.ReportCommandLine(":wq");

        Assert.Equal(":wq", viewModel.VimCommandLineText);
        Assert.True(viewModel.IsVimCommandLineVisible);
        Assert.Contains(nameof(MainWindowViewModel.VimCommandLineText), raised);
        Assert.Contains(nameof(MainWindowViewModel.IsVimCommandLineVisible), raised);

        harness.Vim.ReportCommandLine(null);

        Assert.False(viewModel.IsVimCommandLineVisible);
    }

    [Fact]
    public void TheModeSlot_IsShownOnlyWhileVimIsSwitchedOn()
    {
        using EditorHarness harness = new();
        MainWindowViewModel viewModel = Create(harness: harness);

        Assert.True(viewModel.IsVimEnabled);

        harness.Settings.Value.VimEnabled = false;
        harness.Documents.OnSettingsChanged();

        Assert.False(viewModel.IsVimEnabled);
    }

    [Fact]
    public async Task ClosingTheSettingsDialog_TellsThePreviewPaneToo()
    {
        using EditorHarness harness = new();
        MainWindowViewModel viewModel = Create(harness: harness);

        List<string?> raised = [];
        viewModel.Preview.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        harness.Settings.Value.PreviewFontSize = 26;

        await viewModel.OpenSettingsCommand.ExecuteAsync(null);

        // The same seam the editor pane and the file tree are told through: the window is closed, so
        // whatever was edited is final, and the preview rebuilds around it without a restart.
        Assert.Contains(nameof(MarkdownPreviewViewModel.Typography), raised);
        Assert.Equal(26, viewModel.Preview.Typography.BodyFontSize);
    }

    [Fact]
    public async Task TheSettingsCommand_OpensTheSettingsDialogOnce()
    {
        FakeSettingsDialogService settingsDialog = new();
        MainWindowViewModel viewModel = Create(settingsDialog: settingsDialog);

        await viewModel.OpenSettingsCommand.ExecuteAsync(null);

        Assert.Equal(1, settingsDialog.ShowCount);
    }

    [Fact]
    public async Task TheAboutCommand_OpensTheAboutDialogOnce()
    {
        FakeAboutDialogService about = new();
        MainWindowViewModel viewModel = Create(about: about);

        await viewModel.OpenAboutCommand.ExecuteAsync(null);

        Assert.Equal(1, about.ShowCount);
    }

    [Fact]
    public async Task WithNoStartupNotice_NothingIsShownWhenTheWindowOpens()
    {
        FakeInfoBarService infoBar = new();
        MainWindowViewModel viewModel = Create(infoBar: infoBar);

        await viewModel.OnOpenedAsync();

        Assert.Empty(infoBar.Shown);
    }

    [Fact]
    public async Task WithAStartupNotice_ItIsShownWhenTheWindowOpens()
    {
        FakeInfoBarService infoBar = new();
        MainWindowViewModel viewModel = Create(
            infoBar: infoBar,
            startupNotice: new StartupNotice("Settings could not be read", "Running on defaults."));

        await viewModel.OnOpenedAsync();

        Assert.Single(infoBar.Shown);
        Assert.NotNull(infoBar.Shown[0]);
    }

    [Fact]
    public async Task AFailureOpeningSettings_IsLoggedAndSurfacedRatherThanSwallowed()
    {
        FakeInfoBarService infoBar = new();
        MainWindowViewModel viewModel = Create(
            settingsDialog: new FakeSettingsDialogService { Failure = new InvalidOperationException("no dialog") },
            infoBar: infoBar);

        // An AsyncRelayCommand parks a faulted task instead of rethrowing, so a command that throws
        // is invisible unless the ViewModel handles it. The user must not be left with a dead button.
        await viewModel.OpenSettingsCommand.ExecuteAsync(null);

        Assert.Single(infoBar.Shown);
    }

    [Fact]
    public async Task AFailureOpeningAbout_IsLoggedAndSurfacedRatherThanSwallowed()
    {
        FakeInfoBarService infoBar = new();
        MainWindowViewModel viewModel = Create(
            about: new FakeAboutDialogService { Failure = new InvalidOperationException("no dialog host") },
            infoBar: infoBar);

        // The About dialog needs a ContentDialog host that is registered after this ViewModel is
        // built, so "the host is not there yet" is a real failure mode rather than a hypothetical one.
        await viewModel.OpenAboutCommand.ExecuteAsync(null);

        Assert.Single(infoBar.Shown);
    }

    [Fact]
    public async Task OnAReturningStart_TheWelcomeDialogIsNotShown()
    {
        FakeFirstRunDialogService firstRunDialog = new();
        MainWindowViewModel viewModel = Create(firstRunDialog: firstRunDialog, firstRun: FirstRunState.Returning);

        await viewModel.OnOpenedAsync();

        // The whole point of the gate: someone who has been using the editor for months must never see
        // the welcome dialog again — including after a corrupt settings file has been quarantined.
        Assert.Equal(0, firstRunDialog.ShowCount);
    }

    [Fact]
    public async Task OnAFirstRun_TheWelcomeDialogIsShownOnce()
    {
        FakeFirstRunDialogService firstRunDialog = new();
        MainWindowViewModel viewModel = Create(firstRunDialog: firstRunDialog, firstRun: FirstRunState.First);

        await viewModel.OnOpenedAsync();

        Assert.Equal(1, firstRunDialog.ShowCount);
    }

    [Fact]
    public async Task AfterTheWelcomeDialog_TheOpenPanesRereadTheSettings()
    {
        using EditorHarness harness = new();
        MainWindowViewModel viewModel = Create(
            firstRunDialog: new FakeFirstRunDialogService
            {
                // Stands in for the user answering the dialog: it edits the very AppSettings object
                // the panes hold, exactly as the real ViewModel does.
                OnShow = () => harness.Settings.Value.PreviewFontSize = 26,
            },
            firstRun: FirstRunState.First,
            harness: harness);

        await viewModel.OnOpenedAsync();

        // The same three seams the settings window is followed by. Without them the choices would only
        // reach the editor at the next start, which is the one thing a first-run dialog must not ask
        // for.
        Assert.Equal(26, viewModel.Preview.Typography.BodyFontSize);
    }

    [Fact]
    public async Task AFailureShowingTheWelcomeDialog_IsSurfacedAndTheRestOfTheStartStillRuns()
    {
        FakeInfoBarService infoBar = new();
        using FakeSessionService session = new();
        MainWindowViewModel viewModel = Create(
            firstRunDialog: new FakeFirstRunDialogService { Failure = new InvalidOperationException("no dialog host") },
            infoBar: infoBar,
            firstRun: FirstRunState.First,
            session: session);

        await viewModel.OnOpenedAsync();

        // It runs before the session is restored, so an unguarded failure here would cost the user
        // their tabs as well as the questions.
        Assert.Single(infoBar.Shown);
        Assert.NotEmpty(session.Documents);
    }

    [Fact]
    public void TheTestExceptionCommand_ThrowsOnTheThreadTheCrashHandlerWatches()
    {
        // Raised from the shell, not the settings window: the ContentDialog host is a control in the
        // main window, so a dialog raised behind a modal child is logged and flushed but never seen.
        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(
            () => Create().ThrowTestExceptionCommand.Execute(null));

        Assert.Contains("Test exception", failure.Message);
    }

    [Fact]
    public Task OpeningADocument_MovesTheStatusBarOffItsEmptyState()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            MainWindowViewModel viewModel = Create(harness: harness);

            await harness.Documents.OpenAsync(harness.Write("notes.md"));

            // Both slots are the editor pane's, mirrored onto the shell that draws the status bar.
            Assert.Equal("notes.md", viewModel.FileInfoText);
            Assert.Equal("Ln 1, Col 1", viewModel.CaretPositionText);
        });

    [Fact]
    public void TheShell_ComesBackWhereTheSessionLeftIt()
    {
        using FakeSessionService session = new();
        session.State.ViewMode = ViewMode.PreviewOnly;
        session.State.SidebarTab = SidebarTab.Search;
        session.State.IsSidebarVisible = false;

        MainWindowViewModel viewModel = Create(session: session);

        // In the constructor rather than when the window opens: the window reads these while it is
        // being built, and a mode arriving later is a pane opening and closing in front of the user.
        Assert.Equal(ViewMode.PreviewOnly, viewModel.ViewMode);
        Assert.Equal(SidebarTab.Search, viewModel.SidebarTab);
        Assert.False(viewModel.IsSidebarVisible);
    }

    [Fact]
    public void PuttingTheShellBack_IsNotRecordedAsAChange()
    {
        using FakeSessionService session = new();
        session.State.ViewMode = ViewMode.EditorOnly;
        session.State.SidebarTab = SidebarTab.Search;

        _ = Create(session: session);

        // Restoring a value and then writing it back is a file write per start, saying what the file
        // already said.
        Assert.Empty(session.Shell);
        Assert.Empty(session.Documents);
    }

    [Fact]
    public void EachOfTheShellsOwnChanges_IsRecorded()
    {
        using FakeSessionService session = new();
        MainWindowViewModel viewModel = Create(session: session);

        viewModel.ViewMode = ViewMode.PreviewOnly;
        viewModel.SidebarTab = SidebarTab.Search;
        viewModel.IsSidebarVisible = false;

        Assert.Equal(3, session.Shell.Count);
        Assert.Equal((ViewMode.PreviewOnly, SidebarTab.Search, false), session.Shell[^1]);
    }

    [Fact]
    public void AShellChangeThatChangesNothing_IsNotRecorded()
    {
        using FakeSessionService session = new();
        MainWindowViewModel viewModel = Create(session: session);

        viewModel.ViewMode = ViewMode.Split;
        viewModel.IsSidebarVisible = true;

        Assert.Empty(session.Shell);
    }

    [Fact]
    public void TheWindowsOwnGeometry_GoesStraightToTheSession()
    {
        using FakeSessionService session = new();
        MainWindowViewModel viewModel = Create(session: session);

        viewModel.ReportWindowPlacement(new WindowPlacement { X = 4, Y = 8, Width = 1280, Height = 800 });
        viewModel.ReportPaneLayout(new PaneLayout { SidebarWidth = 320, EditorWeight = 2, PreviewWeight = 1 });

        Assert.Equal(1280, Assert.Single(session.Windows).Width);
        Assert.Equal(320, Assert.Single(session.Panes).SidebarWidth);
    }

    [Fact]
    public void WhatTheWindowRestoresWith_IsWhatTheSessionHeldWhenTheShellWasBuilt()
    {
        using FakeSessionService session = new();
        session.State.Window = new WindowPlacement { X = 1, Y = 2, Width = 1024, Height = 768 };
        session.State.Panes = new PaneLayout { SidebarWidth = 300, EditorWeight = 1, PreviewWeight = 2 };

        MainWindowViewModel viewModel = Create(session: session);

        // Taken once, not read through: the same state the recorders overwrite, and the window applies
        // its value after the first resize has already replaced it.
        viewModel.ReportWindowPlacement(new WindowPlacement { X = 99, Y = 99, Width = 640, Height = 480 });

        Assert.Equal(1024, viewModel.RestoredWindow?.Width);
        Assert.Equal(300, viewModel.RestoredPanes?.SidebarWidth);
    }

    [Fact]
    public Task OpeningADocument_RecordsTheTabList()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            using FakeSessionService session = new();
            _ = Create(harness: harness, session: session);

            string note = harness.Write("notes.md");
            await harness.Documents.OpenAsync(note);

            (IReadOnlyList<string> paths, int activeIndex) = session.Documents[^1];

            Assert.Equal([note], paths);
            Assert.Equal(0, activeIndex);
        });

    [Fact]
    public Task TheSessionsTabs_ComeBackInOrderWithTheOneThatWasOnScreen()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            using FakeSessionService session = new();

            string first = harness.Write("first.md");
            string second = harness.Write("second.md");
            string third = harness.Write("third.md");

            session.State.OpenDocuments = [first, second, third];
            session.State.ActiveDocumentIndex = 1;

            MainWindowViewModel viewModel = Create(harness: harness, session: session);

            await viewModel.OnOpenedAsync();

            Assert.Equal(
                [first, second, third],
                harness.Documents.Documents.Select(document => document.FilePath));
            Assert.Equal(second, harness.Documents.ActiveDocument?.FilePath);
        });

    [Fact]
    public Task ANoteThatIsGone_IsSkippedAndTheShorterListIsWrittenBack()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            using FakeSessionService session = new();

            string kept = harness.Write("kept.md");
            string alsoKept = harness.Write("also-kept.md");

            session.State.OpenDocuments = [kept, harness.Combine("deleted.md"), alsoKept];
            session.State.ActiveDocumentIndex = 2;

            MainWindowViewModel viewModel = Create(harness: harness, session: session);

            await viewModel.OnOpenedAsync();

            Assert.Equal([kept, alsoKept], harness.Documents.Documents.Select(document => document.FilePath));
            Assert.Equal(alsoKept, harness.Documents.ActiveDocument?.FilePath);

            // Written back, so a note that has gone stops being asked for at every start.
            (IReadOnlyList<string> paths, int activeIndex) = session.Documents[^1];
            Assert.Equal([kept, alsoKept], paths);
            Assert.Equal(1, activeIndex);
        });

    [Fact]
    public Task TabsBelongingToAnotherWorkspace_AreNotReopened()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            using FakeSessionService session = new();

            session.State.OpenDocuments = [harness.Write("elsewhere.md")];
            session.State.ActiveDocumentIndex = 0;
            session.State.ActiveWorkspaceId = Guid.NewGuid();

            // The shell opened with no workspace, so the stamp does not match: the paths would all
            // open, which is exactly why they must not.
            MainWindowViewModel viewModel = Create(harness: harness, session: session);

            await viewModel.OnOpenedAsync();

            Assert.Empty(harness.Documents.Documents);

            // And the list is written back empty, so a session belonging to another workspace stops
            // being carried around under this one's stamp.
            (IReadOnlyList<string> paths, int activeIndex) = session.Documents[^1];
            Assert.Empty(paths);
            Assert.Equal(-1, activeIndex);
        });

    [Fact]
    public void EveryDependency_IsRequired()
    {
        using EditorHarness harness = new();
        FakeSettingsDialogService settingsDialog = new();
        FakeAboutDialogService about = new();
        FakeFirstRunDialogService firstRunDialog = new();
        FakeInfoBarService infoBar = new();
        NullLogger<MainWindowViewModel> logger = NullLogger<MainWindowViewModel>.Instance;

        WorkspacesViewModel workspaces = CreateWorkspaces();
        using FileTreeViewModel tree = CreateTree(harness.Settings);
        using SearchViewModel search = CreateSearch(harness.Documents);
        DocumentManagerViewModel document = harness.Documents;
        using MarkdownPreviewViewModel preview = CreatePreview(harness.Documents, harness.Settings);
        using FakeSessionService session = new();

        Assert.Throws<ArgumentNullException>(
            () => new MainWindowViewModel(null!, tree, search, document, preview, settingsDialog, about, firstRunDialog, infoBar, StartupNotice.None, FirstRunState.Returning, session, logger));
        Assert.Throws<ArgumentNullException>(
            () => new MainWindowViewModel(workspaces, null!, search, document, preview, settingsDialog, about, firstRunDialog, infoBar, StartupNotice.None, FirstRunState.Returning, session, logger));
        Assert.Throws<ArgumentNullException>(
            () => new MainWindowViewModel(workspaces, tree, null!, document, preview, settingsDialog, about, firstRunDialog, infoBar, StartupNotice.None, FirstRunState.Returning, session, logger));
        Assert.Throws<ArgumentNullException>(
            () => new MainWindowViewModel(workspaces, tree, search, null!, preview, settingsDialog, about, firstRunDialog, infoBar, StartupNotice.None, FirstRunState.Returning, session, logger));
        Assert.Throws<ArgumentNullException>(
            () => new MainWindowViewModel(workspaces, tree, search, document, null!, settingsDialog, about, firstRunDialog, infoBar, StartupNotice.None, FirstRunState.Returning, session, logger));
        Assert.Throws<ArgumentNullException>(
            () => new MainWindowViewModel(workspaces, tree, search, document, preview, null!, about, firstRunDialog, infoBar, StartupNotice.None, FirstRunState.Returning, session, logger));
        Assert.Throws<ArgumentNullException>(
            () => new MainWindowViewModel(workspaces, tree, search, document, preview, settingsDialog, null!, firstRunDialog, infoBar, StartupNotice.None, FirstRunState.Returning, session, logger));
        Assert.Throws<ArgumentNullException>(
            () => new MainWindowViewModel(workspaces, tree, search, document, preview, settingsDialog, about, null!, infoBar, StartupNotice.None, FirstRunState.Returning, session, logger));
        Assert.Throws<ArgumentNullException>(
            () => new MainWindowViewModel(workspaces, tree, search, document, preview, settingsDialog, about, firstRunDialog, null!, StartupNotice.None, FirstRunState.Returning, session, logger));
        Assert.Throws<ArgumentNullException>(
            () => new MainWindowViewModel(workspaces, tree, search, document, preview, settingsDialog, about, firstRunDialog, infoBar, null!, FirstRunState.Returning, session, logger));
        Assert.Throws<ArgumentNullException>(
            () => new MainWindowViewModel(workspaces, tree, search, document, preview, settingsDialog, about, firstRunDialog, infoBar, StartupNotice.None, null!, session, logger));
        Assert.Throws<ArgumentNullException>(
            () => new MainWindowViewModel(workspaces, tree, search, document, preview, settingsDialog, about, firstRunDialog, infoBar, StartupNotice.None, FirstRunState.Returning, null!, logger));
        Assert.Throws<ArgumentNullException>(
            () => new MainWindowViewModel(workspaces, tree, search, document, preview, settingsDialog, about, firstRunDialog, infoBar, StartupNotice.None, FirstRunState.Returning, session, null!));
    }

    private static MainWindowViewModel Create(
        FakeSettingsDialogService? settingsDialog = null,
        FakeAboutDialogService? about = null,
        FakeFirstRunDialogService? firstRunDialog = null,
        FakeInfoBarService? infoBar = null,
        StartupNotice? startupNotice = null,
        FirstRunState? firstRun = null,
        EditorHarness? harness = null,
        FakeSessionService? session = null)
    {
        // Not disposed on purpose when the caller supplied none: in the application the tree and the
        // editor pane live as long as the shell does, and these tests open nothing in either.
        harness ??= new EditorHarness();

        return new MainWindowViewModel(
            CreateWorkspaces(),
            CreateTree(harness.Settings),
            CreateSearch(harness.Documents),
            harness.Documents,
            CreatePreview(harness.Documents, harness.Settings),
            settingsDialog ?? new FakeSettingsDialogService(),
            about ?? new FakeAboutDialogService(),
            firstRunDialog ?? new FakeFirstRunDialogService(),
            infoBar ?? new FakeInfoBarService(),
            startupNotice ?? StartupNotice.None,
            firstRun ?? FirstRunState.Returning,
            session ?? new FakeSessionService(),
            NullLogger<MainWindowViewModel>.Instance);
    }

    // The shell only hosts the switcher; what it does is covered by WorkspacesViewModelTests.
    private static WorkspacesViewModel CreateWorkspaces()
        => new(
            new FakeWorkspaceService(),
            new FakeWorkspaceManagerDialogService(),
            new FakeInfoBarService(),
            NullLogger<WorkspacesViewModel>.Instance);

    // Likewise the search tab — see SearchViewModelTests.
    private static SearchViewModel CreateSearch(DocumentManagerViewModel documents)
        => new(
            new FakeWorkspaceService(),
            documents,
            new FakeIdleScheduler(),
            new FakeUiDispatcher(),
            NullLogger<SearchViewModel>.Instance);

    // Likewise the preview pane — see MarkdownPreviewViewModelTests.
    private static MarkdownPreviewViewModel CreatePreview(
        DocumentManagerViewModel documents,
        IConfig<AppSettings> settings)
        => new(
            documents,
            settings,
            TestFonts.Catalog(),
            new FakeDocumentOpener(),
            new FakeFileLauncher(),
            new FakeClipboardWriter(),
            new FakeIdleScheduler(),
            NullLogger<MarkdownPreviewViewModel>.Instance);

    // Likewise the tree and the editor pane — see FileTreeViewModelTests and
    // DocumentManagerViewModelTests.
    private static FileTreeViewModel CreateTree(IConfig<AppSettings> settings)
        => new(
            new FakeWorkspaceService(),
            settings,
            new FakeDirectoryWatcherFactory(),
            new FakeDocumentOpener(),
            new FakeFileOperationPrompts(),
            new FakeTrashService(),
            new FakeUiDispatcher(),
            new FakeInfoBarService(),
            NullLogger<FileTreeViewModel>.Instance);

    private static void AssertPaneTogglesAndNotifiesOnce(
        string propertyName,
        Func<MainWindowViewModel, bool> read,
        Action<MainWindowViewModel, bool> write)
    {
        MainWindowViewModel viewModel = Create();
        List<string?> raised = [];
        viewModel.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        write(viewModel, false);
        write(viewModel, false);

        Assert.False(read(viewModel));
        Assert.Single(raised);
        Assert.Equal(propertyName, raised[0]);
    }
}
