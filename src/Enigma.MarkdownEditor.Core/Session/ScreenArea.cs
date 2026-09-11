namespace Enigma.MarkdownEditor.Core.Session;

/// <summary>
/// One screen's usable area, in device pixels.
/// </summary>
/// <remarks>
/// The working area rather than the whole screen — a window put back over the taskbar is a window with
/// a piece missing. Core has no idea what a screen is; this is only the shape the view hands it so
/// that <see cref="WindowPlacements.Clamp"/> can be a pure function with a test.
/// </remarks>
/// <param name="X">The left edge, in device pixels.</param>
/// <param name="Y">The top edge, in device pixels.</param>
/// <param name="Width">The usable width, in device pixels.</param>
/// <param name="Height">The usable height, in device pixels.</param>
public readonly record struct ScreenArea(int X, int Y, int Width, int Height);
