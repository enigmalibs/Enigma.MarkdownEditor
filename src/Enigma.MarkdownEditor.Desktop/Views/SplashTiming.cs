using System;

namespace Enigma.MarkdownEditor.Desktop.Views;

/// <summary>
/// How long the splash screen stays on screen: a floor, not a fixed duration.
/// </summary>
/// <remarks>
/// <para>
/// Composition takes as long as it takes — a cold machine, a large session to restore, an antivirus
/// reading the executable — so the splash cannot be shown for a fixed time without either flashing
/// past on a fast start or padding a slow one. The rule is a minimum: the splash is held until
/// <see cref="MinimumDisplay"/> has passed since it appeared, and not one frame longer.
/// </para>
/// <para>
/// The arithmetic lives here, apart from the window and apart from <c>App</c>, because it is the only
/// part of the hand-over that can be tested. What the window sequence does — a splash shown before
/// the message loop, a main window shown from a posted continuation — is not something a headless
/// test can assert; whether 1 s minus 1.2 s is zero rather than a negative delay is.
/// </para>
/// </remarks>
public static class SplashTiming
{
    /// <summary>
    /// How long the splash is shown for at the very least, measured from the moment it is shown.
    /// </summary>
    /// <remarks>
    /// One second. Long enough to be read as a splash screen rather than as a flicker, short enough
    /// that nobody waits for it: on this machine composition already takes most of it.
    /// </remarks>
    public static readonly TimeSpan MinimumDisplay = TimeSpan.FromMilliseconds(1000);

    /// <summary>
    /// How much longer the splash has to be held.
    /// </summary>
    /// <param name="elapsed">How long the splash has been on screen.</param>
    /// <param name="minimum">The floor it is held to — normally <see cref="MinimumDisplay"/>.</param>
    /// <returns>
    /// The delay still owed, or <see cref="TimeSpan.Zero"/> when the floor has already been reached.
    /// Never negative: a caller passes this straight to a timer, and a negative interval throws.
    /// </returns>
    public static TimeSpan RemainingDelay(TimeSpan elapsed, TimeSpan minimum)
    {
        TimeSpan remaining = minimum - elapsed;

        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }
}
