using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Enigma.Avalonia.Desktop.Controls.ContentDialog;
using Enigma.MarkdownEditor.Core;
using Enigma.MarkdownEditor.Desktop.Services;
using Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;
using Enigma.MarkdownEditor.Desktop.Views;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Services;

/// <summary>
/// The far end of the safety net: what is logged, what is flushed, and when the error dialog is shown.
/// </summary>
/// <remarks>
/// The dialog is posted to the UI thread, so each test runs the dispatcher's queue
/// (<see cref="Dispatcher.RunJobs()"/>) before it looks. The dialog service is a fake; the host the
/// reporter waits on is a real <see cref="ContentDialog"/>, opened and closed through its own API.
/// </remarks>
public sealed class CrashReporterTests
{
    private const string LogFile = @"C:\logs\markdown-editor.log";

    [AvaloniaTheory]
    [InlineData(UnhandledErrorSource.AppDomain, LogLevel.Critical)]
    [InlineData(UnhandledErrorSource.Startup, LogLevel.Critical)]
    [InlineData(UnhandledErrorSource.UnobservedTask, LogLevel.Error)]
    [InlineData(UnhandledErrorSource.Dispatcher, LogLevel.Error)]
    public void EveryReport_IsLoggedAndFlushed(UnhandledErrorSource source, LogLevel level)
    {
        Harness harness = new();
        InvalidOperationException failure = new("boom");

        harness.Reporter.Report(failure, source);

        LogEntry entry = Assert.Single(harness.Logger.Entries);
        Assert.Equal(level, entry.Level);
        Assert.Same(failure, entry.Failure);
        Assert.Equal(1, harness.Handle.Flushes);
    }

    [AvaloniaTheory]
    [InlineData(UnhandledErrorSource.AppDomain)]
    [InlineData(UnhandledErrorSource.Startup)]
    public void AReportTheProcessDoesNotSurvive_ShowsNoDialogOnTheHost(UnhandledErrorSource source)
    {
        // AppDomain: the process is already going down. Startup: the start-up error window shows it,
        // because the main window and its host never came.
        Harness harness = new();
        harness.Reporter.AttachHost(new ContentDialog());

        harness.Reporter.Report(new InvalidOperationException("boom"), source);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, harness.Dialogs.Attempts);
    }

    [AvaloniaTheory]
    [InlineData(UnhandledErrorSource.UnobservedTask)]
    [InlineData(UnhandledErrorSource.Dispatcher)]
    public void AnErrorTheProcessSurvives_IsShownInTheErrorDialog(UnhandledErrorSource source)
    {
        Harness harness = new();
        harness.Reporter.AttachHost(new ContentDialog());

        harness.Reporter.Report(new InvalidOperationException("the disk went away"), source);
        Dispatcher.UIThread.RunJobs();

        ContentDialog dialog = Assert.Single(harness.Dialogs.Shown);
        Assert.Equal(ErrorDialog.Title, dialog.Title);
        Assert.Equal("Close", dialog.CloseButtonText);
        Assert.Equal(DefaultButton.Close, dialog.DefaultButton);

        string text = Assert.IsType<SelectableTextBlock>(dialog.Content).Text ?? string.Empty;
        Assert.Contains("InvalidOperationException: the disk went away", text, StringComparison.Ordinal);
        Assert.Contains($"{AppInfo.DisplayName} is still running", text, StringComparison.Ordinal);
        Assert.Contains(LogFile, text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void AnErrorBeforeTheHost_IsShownOnceTheHostArrives()
    {
        Harness harness = new();

        harness.Reporter.Report(new InvalidOperationException("early"), UnhandledErrorSource.Dispatcher);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, harness.Dialogs.Attempts);

        harness.Reporter.AttachHost(new ContentDialog());

        Assert.Single(harness.Dialogs.Shown);
    }

    [AvaloniaFact]
    public async Task AnErrorWhileAnotherQuestionIsOpen_WaitsForItsAnswer()
    {
        Harness harness = new();
        ContentDialog host = new();
        Task<DialogResult> question = host.ShowAsync();
        harness.Reporter.AttachHost(host);

        harness.Reporter.Report(new InvalidOperationException("boom"), UnhandledErrorSource.Dispatcher);
        Dispatcher.UIThread.RunJobs();

        // Showing now would take the host over and lose the question on it.
        Assert.Equal(0, harness.Dialogs.Attempts);

        await host.HideAsync();
        await question;
        Dispatcher.UIThread.RunJobs();

        Assert.Single(harness.Dialogs.Shown);
    }

    [AvaloniaFact]
    public void ErrorsWhileTheDialogIsOpen_AreOnlyLogged()
    {
        Harness harness = new(new FakeContentDialogService { Hold = true, Result = DialogResult.Close });
        harness.Reporter.AttachHost(new ContentDialog());

        harness.Reporter.Report(new InvalidOperationException("first"), UnhandledErrorSource.Dispatcher);
        harness.Reporter.Report(new InvalidOperationException("second"), UnhandledErrorSource.Dispatcher);
        Dispatcher.UIThread.RunJobs();

        Assert.Single(harness.Dialogs.Shown);
        Assert.Equal(2, harness.Logger.At(LogLevel.Error).Count);

        // Once it is closed, the next error gets a dialog of its own.
        harness.Dialogs.Release();
        harness.Reporter.Report(new InvalidOperationException("third"), UnhandledErrorSource.Dispatcher);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, harness.Dialogs.Shown.Count);
    }

    [AvaloniaFact]
    public void ADialogThatCannotBeShown_IsLoggedAndNeverTriedAgain()
    {
        Harness harness = new(new FakeContentDialogService { Failure = new InvalidOperationException("no host") });
        harness.Reporter.AttachHost(new ContentDialog());

        harness.Reporter.Report(new InvalidOperationException("first"), UnhandledErrorSource.Dispatcher);
        Dispatcher.UIThread.RunJobs();
        harness.Reporter.Report(new InvalidOperationException("second"), UnhandledErrorSource.Dispatcher);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, harness.Dialogs.Attempts);

        // The two errors, plus the dialog's own failure — logged, not reported back into the net.
        Assert.Equal(3, harness.Logger.At(LogLevel.Error).Count);
    }

    [AvaloniaFact]
    public void TheErrorDialog_HasTheUniformLook()
    {
        ContentDialog dialog = new();

        ErrorDialog.Configure(dialog, "what happened");

        Assert.Equal("Something went wrong", dialog.Title);
        Assert.NotNull(dialog.IconData);
        Assert.Equal(Color.Parse("#E8A33D"), Assert.IsType<SolidColorBrush>(dialog.IconBrush).Color);
        Assert.Equal("Close", dialog.CloseButtonText);
        Assert.Equal(DefaultButton.Close, dialog.DefaultButton);
        Assert.Null(dialog.PrimaryButtonText);
        Assert.Null(dialog.SecondaryButtonText);
        Assert.Equal("what happened", Assert.IsType<SelectableTextBlock>(dialog.Content).Text);
    }

    [Fact]
    public void Arguments_AreValidated()
    {
        RecordingLogger<CrashReporter> logger = new();
        FakeFlushableLoggerHandle handle = new();
        FakeContentDialogService dialogs = new();

        Assert.Throws<ArgumentNullException>(() => new CrashReporter(null!, handle, dialogs, LogFile));
        Assert.Throws<ArgumentNullException>(() => new CrashReporter(logger, null!, dialogs, LogFile));
        Assert.Throws<ArgumentNullException>(() => new CrashReporter(logger, handle, null!, LogFile));
        Assert.ThrowsAny<ArgumentException>(() => new CrashReporter(logger, handle, dialogs, " "));

        CrashReporter reporter = new(logger, handle, dialogs, LogFile);
        Assert.Throws<ArgumentNullException>(() => reporter.Report(null!, UnhandledErrorSource.Dispatcher));
        Assert.Throws<ArgumentNullException>(() => reporter.AttachHost(null!));
    }

    private sealed class Harness
    {
        public Harness(FakeContentDialogService? dialogs = null)
        {
            Dialogs = dialogs ?? new FakeContentDialogService { Result = DialogResult.Close };
            Reporter = new CrashReporter(Logger, Handle, Dialogs, LogFile);
        }

        public RecordingLogger<CrashReporter> Logger { get; } = new();

        public FakeFlushableLoggerHandle Handle { get; } = new();

        public FakeContentDialogService Dialogs { get; }

        public CrashReporter Reporter { get; }
    }
}
