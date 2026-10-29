using System;
using System.Linq;
using Enigma.MarkdownEditor.Desktop.Services;
using Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Services;

/// <summary>
/// The router between the process-wide hooks and the crash reporter: straight through once the
/// reporter is there, kept until it is.
/// </summary>
/// <remarks>
/// Over a fresh router each time rather than <see cref="UnhandledExceptionHooks.Router"/>: the static
/// one belongs to the process, and a test that attached to it would leave its fake behind for every
/// other test in the run.
/// </remarks>
public sealed class UnhandledExceptionRouterTests
{
    [Fact]
    public void AReport_WithAReporterAttached_GoesStraightToIt()
    {
        UnhandledExceptionRouter router = new();
        RecordingCrashReporter reporter = new();
        InvalidOperationException failure = new("boom");

        router.Attach(reporter);
        router.Report(failure, UnhandledErrorSource.Dispatcher);

        (Exception exception, UnhandledErrorSource source) = Assert.Single(reporter.Reports);
        Assert.Same(failure, exception);
        Assert.Equal(UnhandledErrorSource.Dispatcher, source);
    }

    [Fact]
    public void Reports_BeforeTheReporter_AreReplayedInOrderWhenItAttaches()
    {
        UnhandledExceptionRouter router = new();
        RecordingCrashReporter reporter = new();
        InvalidOperationException first = new("first");
        InvalidOperationException second = new("second");

        router.Report(first, UnhandledErrorSource.UnobservedTask);
        router.Report(second, UnhandledErrorSource.Dispatcher);
        router.Attach(reporter);

        Assert.Equal(
            [(first, UnhandledErrorSource.UnobservedTask), (second, UnhandledErrorSource.Dispatcher)],
            reporter.Reports);
    }

    [Fact]
    public void TheReportsKeptForTheReporter_AreBounded()
    {
        UnhandledExceptionRouter router = new();
        RecordingCrashReporter reporter = new();
        InvalidOperationException[] failures = [.. Enumerable.Range(0, UnhandledExceptionRouter.EarlyLimit + 5)
            .Select(index => new InvalidOperationException($"failure {index}"))];

        foreach (InvalidOperationException failure in failures)
        {
            router.Report(failure, UnhandledErrorSource.Dispatcher);
        }

        router.Attach(reporter);

        // The first ones: a loop that faults says everything on its first few turns.
        Assert.Equal(
            failures.Take(UnhandledExceptionRouter.EarlyLimit),
            reporter.Reports.Select(report => report.Exception));
    }

    [Fact]
    public void AReplay_HappensOnce()
    {
        UnhandledExceptionRouter router = new();
        RecordingCrashReporter first = new();
        RecordingCrashReporter second = new();

        router.Report(new InvalidOperationException("early"), UnhandledErrorSource.Dispatcher);
        router.Attach(first);
        router.Attach(second);

        Assert.Single(first.Reports);
        Assert.Empty(second.Reports);
    }

    [Fact]
    public void AfterDetach_ReportsAreKeptForTheNextReporter()
    {
        UnhandledExceptionRouter router = new();
        RecordingCrashReporter detached = new();
        RecordingCrashReporter next = new();
        InvalidOperationException late = new("late");

        router.Attach(detached);
        router.Detach();
        router.Report(late, UnhandledErrorSource.UnobservedTask);

        Assert.Empty(detached.Reports);

        router.Attach(next);

        Assert.Same(late, Assert.Single(next.Reports).Exception);
    }

    [Fact]
    public void IsAttached_FollowsTheReporter()
    {
        UnhandledExceptionRouter router = new();

        Assert.False(router.IsAttached);

        router.Attach(new RecordingCrashReporter());
        Assert.True(router.IsAttached);

        router.Detach();
        Assert.False(router.IsAttached);
    }

    [Fact]
    public void NullArguments_AreRejected()
    {
        UnhandledExceptionRouter router = new();

        Assert.Throws<ArgumentNullException>(() => router.Attach(null!));
        Assert.Throws<ArgumentNullException>(() => router.Report(null!, UnhandledErrorSource.Dispatcher));
    }
}
