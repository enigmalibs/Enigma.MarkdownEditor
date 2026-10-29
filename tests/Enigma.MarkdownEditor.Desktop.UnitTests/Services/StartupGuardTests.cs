using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Enigma.MarkdownEditor.Core;
using Enigma.MarkdownEditor.Desktop.Services;
using Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;
using Enigma.MarkdownEditor.Desktop.Views;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Services;

/// <summary>
/// The guard around the start-up: nothing at all when it succeeds, a reported failure and a window
/// that says so when it throws.
/// </summary>
/// <remarks>
/// Over a fresh router each time, for the reason <see cref="UnhandledExceptionRouterTests"/> gives.
/// </remarks>
public sealed class StartupGuardTests
{
    private const string LogFile = @"C:\logs\markdown-editor.log";

    [AvaloniaFact]
    public void AStartThatSucceeds_IsLeftAlone()
    {
        UnhandledExceptionRouter router = new();
        RecordingCrashReporter reporter = new();
        router.Attach(reporter);
        List<Window> windows = [];
        bool started = false;

        StartupGuard.Run(() => started = true, windows.Add, router, LogFile);

        Assert.True(started);
        Assert.Empty(windows);
        Assert.Empty(reporter.Reports);
    }

    [AvaloniaFact]
    public void AStartThatThrows_IsReportedAndGetsTheErrorWindow()
    {
        UnhandledExceptionRouter router = new();
        RecordingCrashReporter reporter = new();
        router.Attach(reporter);
        List<Window> windows = [];
        InvalidOperationException failure = new("no main window today");

        StartupGuard.Run(() => throw failure, windows.Add, router, LogFile);

        (Exception exception, UnhandledErrorSource source) = Assert.Single(reporter.Reports);
        Assert.Same(failure, exception);
        Assert.Equal(UnhandledErrorSource.Startup, source);

        StartupErrorWindow window = Assert.IsType<StartupErrorWindow>(Assert.Single(windows));
        Assert.Contains("InvalidOperationException: no main window today", window.Message, StringComparison.Ordinal);
        Assert.Contains($"{AppInfo.DisplayName} could not start", window.Message, StringComparison.Ordinal);
        Assert.Contains(LogFile, window.Message, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void AStartThatThrowsBeforeLogging_SaysTheLogHasNothing()
    {
        UnhandledExceptionRouter router = new();
        List<Window> windows = [];
        InvalidOperationException failure = new("too early");

        StartupGuard.Run(() => throw failure, windows.Add, router, LogFile);

        StartupErrorWindow window = Assert.IsType<StartupErrorWindow>(Assert.Single(windows));
        Assert.DoesNotContain(LogFile, window.Message, StringComparison.Ordinal);
        Assert.Contains("could not be written to the log", window.Message, StringComparison.Ordinal);

        // Kept for a reporter all the same, as every early report is.
        RecordingCrashReporter late = new();
        router.Attach(late);
        Assert.Same(failure, Assert.Single(late.Reports).Exception);
    }

    [Fact]
    public void NullArguments_AreRejected()
    {
        UnhandledExceptionRouter router = new();

        Assert.Throws<ArgumentNullException>(() => StartupGuard.Run(null!, _ => { }, router, LogFile));
        Assert.Throws<ArgumentNullException>(() => StartupGuard.Run(() => { }, null!, router, LogFile));
        Assert.Throws<ArgumentNullException>(() => StartupGuard.Run(() => { }, _ => { }, null!, LogFile));
        Assert.Throws<ArgumentNullException>(() => StartupGuard.Run(() => { }, _ => { }, router, null!));
        Assert.Throws<ArgumentNullException>(() => StartupGuard.Describe(null!, LogFile));
    }
}
