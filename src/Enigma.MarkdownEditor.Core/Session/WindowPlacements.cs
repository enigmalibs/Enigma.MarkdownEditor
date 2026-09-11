using System;
using System.Collections.Generic;

namespace Enigma.MarkdownEditor.Core.Session;

/// <summary>
/// Brings a remembered window back onto the screens that are actually there.
/// </summary>
/// <remarks>
/// A saved placement is a fact about a machine as it was: docked to a second monitor, or spread across
/// an external display that has since been unplugged. Restored as written, it can put the window
/// somewhere the user cannot reach it — off the side of the only screen left, or with its title bar
/// above the top of it — and a window that cannot be grabbed cannot be moved back. So a placement is
/// never applied as written; it is pulled onto a screen first.
/// </remarks>
public static class WindowPlacements
{
    /// <summary>
    /// Pulls a remembered placement onto the current screen arrangement.
    /// </summary>
    /// <param name="placement">What was remembered, or <see langword="null"/> when nothing was.</param>
    /// <param name="screens">The screens' working areas, in device pixels.</param>
    /// <param name="minWidth">The smallest width the window may be given, in device pixels.</param>
    /// <param name="minHeight">The smallest height, in device pixels.</param>
    /// <returns>
    /// A placement that fits on one of <paramref name="screens"/>, or <see langword="null"/> when
    /// there is nothing to restore — no saved placement, or no screen to put it on — and the window
    /// should therefore open where it would on a first run.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="screens"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="minWidth"/> or <paramref name="minHeight"/> is zero or negative.
    /// </exception>
    /// <remarks>
    /// A window that still touches a screen keeps the corner it was dragged to and is only pushed far
    /// enough to fit. One that touches none of them has lost the monitor it lived on, and is centred on
    /// the first screen instead — putting it back at coordinates that no longer mean anything would be
    /// restoring the number rather than the window.
    /// </remarks>
    public static WindowPlacement? Clamp(
        WindowPlacement? placement,
        IReadOnlyList<ScreenArea> screens,
        int minWidth,
        int minHeight)
    {
        ArgumentNullException.ThrowIfNull(screens);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minHeight);

        if ((placement is null) || (screens.Count == 0))
        {
            return null;
        }

        ScreenArea? overlapped = MostOverlapped(placement, screens);
        ScreenArea target = overlapped ?? screens[0];

        // The screen is the ceiling as well as the floor: a window remembered from a 4K monitor must
        // not come back wider than the laptop panel that is left.
        int width = Math.Clamp(placement.Width, minWidth, Math.Max(minWidth, target.Width));
        int height = Math.Clamp(placement.Height, minHeight, Math.Max(minHeight, target.Height));

        (int x, int y) = overlapped is null
            ? (target.X + ((target.Width - width) / 2), target.Y + ((target.Height - height) / 2))
            : (Fit(placement.X, target.X, target.Width, width), Fit(placement.Y, target.Y, target.Height, height));

        return new WindowPlacement
        {
            X = x,
            Y = y,
            Width = width,
            Height = height,
            IsMaximized = placement.IsMaximized,
        };
    }

    // Math.Clamp throws when the window is wider than the screen it is being fitted onto, which is the
    // very case a monitor swap produces — so the upper bound is never allowed below the lower one, and
    // an oversized window lands flush against the screen's top-left corner.
    private static int Fit(int position, int screenStart, int screenLength, int windowLength)
        => Math.Clamp(position, screenStart, Math.Max(screenStart, screenStart + screenLength - windowLength));

    private static ScreenArea? MostOverlapped(WindowPlacement placement, IReadOnlyList<ScreenArea> screens)
    {
        ScreenArea? best = null;
        long bestArea = 0;

        foreach (ScreenArea screen in screens)
        {
            long area = OverlapArea(placement, screen);

            if (area > bestArea)
            {
                bestArea = area;
                best = screen;
            }
        }

        return best;
    }

    private static long OverlapArea(WindowPlacement placement, ScreenArea screen)
    {
        long width = Overlap(placement.X, placement.Width, screen.X, screen.Width);
        long height = Overlap(placement.Y, placement.Height, screen.Y, screen.Height);

        return width * height;
    }

    private static long Overlap(int start, int length, int otherStart, int otherLength)
        => Math.Max(0, Math.Min((long)start + length, (long)otherStart + otherLength) - Math.Max(start, otherStart));
}
