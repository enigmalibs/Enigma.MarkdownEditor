using System;
using Enigma.MarkdownEditor.Desktop.Views;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Views;

/// <summary>
/// The splash screen's minimum-display arithmetic.
/// </summary>
/// <remarks>
/// The window sequence around it — a splash shown before the message loop starts, a main window shown
/// from a posted continuation, one closed after the other — is not something a headless test can
/// assert, and it is verified by running the application instead. This is the part that can be:
/// whether the hold is a floor, and whether it ever asks a timer for a negative interval.
/// </remarks>
public sealed class SplashTimingTests
{
    [Theory]
    // A start too fast to see: nearly all of the minimum is still owed.
    [InlineData(0, 1000, 1000)]
    [InlineData(400, 1000, 600)]
    // One millisecond short of the floor is still one millisecond of hold.
    [InlineData(999, 1000, 1)]
    // Exactly at the floor, and past it: no hold at all. This is the cold-machine start, where
    // composition took longer than the minimum on its own.
    [InlineData(1000, 1000, 0)]
    [InlineData(1001, 1000, 0)]
    [InlineData(5000, 1000, 0)]
    // A floor of zero holds nothing, whatever has elapsed.
    [InlineData(0, 0, 0)]
    [InlineData(250, 0, 0)]
    public void TheRemainingDelay_IsWhatIsStillOwed(int elapsedMs, int minimumMs, int expectedMs)
    {
        TimeSpan remaining = SplashTiming.RemainingDelay(
            TimeSpan.FromMilliseconds(elapsedMs),
            TimeSpan.FromMilliseconds(minimumMs));

        Assert.Equal(TimeSpan.FromMilliseconds(expectedMs), remaining);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(999)]
    [InlineData(1000)]
    [InlineData(60000)]
    public void TheRemainingDelay_IsNeverNegative(int elapsedMs)
    {
        // The caller hands this straight to a dispatcher timer, which throws on a negative interval.
        TimeSpan remaining = SplashTiming.RemainingDelay(
            TimeSpan.FromMilliseconds(elapsedMs),
            SplashTiming.MinimumDisplay);

        Assert.True(remaining >= TimeSpan.Zero);
    }

    [Fact]
    public void TheMinimumDisplay_IsOneSecond()
        => Assert.Equal(TimeSpan.FromMilliseconds(1000), SplashTiming.MinimumDisplay);
}
