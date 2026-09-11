using System;
using System.Collections.Generic;
using Enigma.MarkdownEditor.Core.Session;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Session;

/// <summary>
/// Bringing a remembered window back onto the screens that are actually there. The cases that matter
/// are the ones where the machine changed: a monitor unplugged, a laptop undocked, a screen that got
/// smaller. A window the user cannot reach is one they cannot move back, so none of these may pass
/// through untouched.
/// </summary>
public sealed class WindowPlacementsTests
{
    private static readonly ScreenArea Primary = new(0, 0, 1920, 1040);
    private static readonly ScreenArea Secondary = new(1920, 0, 2560, 1400);

    [Fact]
    public void AWindowThatStillFits_ComesBackExactlyWhereItWas()
    {
        WindowPlacement? clamped = Clamp(new WindowPlacement { X = 100, Y = 60, Width = 1280, Height = 800 }, Primary);

        Assert.NotNull(clamped);
        Assert.Equal(100, clamped.X);
        Assert.Equal(60, clamped.Y);
        Assert.Equal(1280, clamped.Width);
        Assert.Equal(800, clamped.Height);
    }

    [Fact]
    public void TheMaximizedFlag_SurvivesTheClamp()
    {
        WindowPlacement? clamped = Clamp(
            new WindowPlacement { X = 100, Y = 60, Width = 1280, Height = 800, IsMaximized = true },
            Primary);

        Assert.NotNull(clamped);
        Assert.True(clamped.IsMaximized);
    }

    [Fact]
    public void AWindowOnASecondScreen_StaysOnIt()
    {
        WindowPlacement? clamped = Clamp(
            new WindowPlacement { X = 2000, Y = 100, Width = 1200, Height = 800 },
            Primary,
            Secondary);

        Assert.NotNull(clamped);
        Assert.Equal(2000, clamped.X);
        Assert.Equal(100, clamped.Y);
    }

    [Fact]
    public void AWindowHangingOffTheRightEdge_IsPushedBackOn()
    {
        WindowPlacement? clamped = Clamp(new WindowPlacement { X = 1800, Y = 60, Width = 1280, Height = 800 }, Primary);

        Assert.NotNull(clamped);

        // Flush against the edge, not centred: the user dragged it over there, and only what it takes
        // to make it reachable is taken back.
        Assert.Equal(1920 - 1280, clamped.X);
        Assert.Equal(60, clamped.Y);
    }

    [Fact]
    public void AWindowWithItsTitleBarAboveTheScreen_IsPulledDown()
    {
        WindowPlacement? clamped = Clamp(new WindowPlacement { X = 100, Y = -300, Width = 1280, Height = 800 }, Primary);

        Assert.NotNull(clamped);
        Assert.Equal(0, clamped.Y);
    }

    [Fact]
    public void AWindowFromAScreenThatIsGone_IsCentredOnTheOneThatIsLeft()
    {
        // The whole point of the exercise: the second monitor was unplugged, and these coordinates now
        // name a place nobody can reach. Restoring the number rather than the window would hide it.
        WindowPlacement? clamped = Clamp(new WindowPlacement { X = 2400, Y = 300, Width = 1280, Height = 800 }, Primary);

        Assert.NotNull(clamped);
        Assert.Equal((1920 - 1280) / 2, clamped.X);
        Assert.Equal((1040 - 800) / 2, clamped.Y);
    }

    [Fact]
    public void AWindowBiggerThanTheScreenItComesBackOn_IsCutDownToIt()
    {
        WindowPlacement? clamped = Clamp(
            new WindowPlacement { X = 100, Y = 0, Width = 2400, Height = 1300 },
            Primary);

        Assert.NotNull(clamped);
        Assert.Equal(1920, clamped.Width);
        Assert.Equal(1040, clamped.Height);
        Assert.Equal(0, clamped.X);
        Assert.Equal(0, clamped.Y);
    }

    [Fact]
    public void AWindowSmallerThanTheMinimum_IsGrownToIt()
    {
        WindowPlacement? clamped = Clamp(new WindowPlacement { X = 10, Y = 10, Width = 0, Height = 0 }, Primary);

        Assert.NotNull(clamped);
        Assert.Equal(720, clamped.Width);
        Assert.Equal(480, clamped.Height);
    }

    [Fact]
    public void AScreenSmallerThanTheMinimum_KeepsTheMinimumRatherThanTheScreen()
    {
        // Both bounds cannot be satisfied at once. The window keeps the size it needs to be usable and
        // is put in the corner, which is the half a user can still do something about.
        WindowPlacement? clamped = Clamp(new WindowPlacement { X = 0, Y = 0, Width = 1280, Height = 800 }, new ScreenArea(0, 0, 400, 300));

        Assert.NotNull(clamped);
        Assert.Equal(720, clamped.Width);
        Assert.Equal(480, clamped.Height);
        Assert.Equal(0, clamped.X);
        Assert.Equal(0, clamped.Y);
    }

    [Fact]
    public void TheScreenWithMostOfTheWindowOnIt_IsTheOneItIsFittedTo()
    {
        // Straddling both, mostly on the second: it belongs to the one it is mostly on, and is pushed
        // fully onto that one rather than back onto the primary.
        WindowPlacement? clamped = Clamp(
            new WindowPlacement { X = 1700, Y = 100, Width = 1000, Height = 800 },
            Primary,
            Secondary);

        Assert.NotNull(clamped);
        Assert.Equal(1920, clamped.X);
    }

    [Fact]
    public void NothingRemembered_IsNothingToRestore()
        => Assert.Null(Clamp(null, Primary));

    [Fact]
    public void NoScreensAtAll_IsNothingToRestore()
        => Assert.Null(WindowPlacements.Clamp(
            new WindowPlacement { X = 0, Y = 0, Width = 1280, Height = 800 },
            [],
            720,
            480));

    [Fact]
    public void NoScreenList_IsRejected()
        => Assert.Throws<ArgumentNullException>(() => WindowPlacements.Clamp(null, null!, 720, 480));

    [Theory]
    [InlineData(0, 480)]
    [InlineData(-1, 480)]
    [InlineData(720, 0)]
    [InlineData(720, -1)]
    public void AMinimumSizeThatIsNotASize_IsRejected(int minWidth, int minHeight)
        => Assert.Throws<ArgumentOutOfRangeException>(
            () => WindowPlacements.Clamp(null, [Primary], minWidth, minHeight));

    private static WindowPlacement? Clamp(WindowPlacement? placement, params ScreenArea[] screens)
        => WindowPlacements.Clamp(placement, new List<ScreenArea>(screens), 720, 480);
}
