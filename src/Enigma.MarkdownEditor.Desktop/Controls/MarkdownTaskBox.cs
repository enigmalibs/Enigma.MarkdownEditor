// Using directives stay at file scope, above the namespace declaration, throughout this project:
// inside a namespace beginning with "Enigma.", the simple name "Avalonia" resolves against the
// referenced Enigma.Avalonia.* assemblies instead of Avalonia itself.
using System;
using Avalonia;
using Avalonia.Controls;

namespace Enigma.MarkdownEditor.Desktop.Controls;

/// <summary>
/// What a task item's checkbox in the preview remembers about the document it came from, and how a
/// tick on it is turned back into an edit.
/// </summary>
/// <remarks>
/// <para>
/// A checkbox in the preview is not a control with a meaning of its own: it stands for three
/// characters of markdown, and the only thing that makes a tick actionable is knowing which three.
/// So the offset of the state character is recorded on the box as it is built — an attached property
/// rather than a <see cref="CheckBox"/> of our own, for the same reason
/// <see cref="MarkdownLinkRegions"/> uses one: the control stays a plain framework
/// <see cref="CheckBox"/> and keeps the theme's template without a style-key override.
/// </para>
/// <para>
/// The offset is <em>where it was</em>, not where it is. The preview is rebuilt on a delay behind the
/// editor, so by the time a box is clicked the buffer may have moved on — which is why the toggle is
/// answered with whether it was applied, and a refused one puts the box back.
/// </para>
/// </remarks>
public sealed class MarkdownTaskBox
{
    /// <summary>
    /// The value <see cref="SourceOffsetProperty"/> holds on a checkbox that stands for nothing in
    /// any document — every checkbox this renderer did not build.
    /// </summary>
    public const int NoOffset = -1;

    /// <summary>
    /// Defines the attached property that carries a task box's source offset.
    /// </summary>
    public static readonly AttachedProperty<int> SourceOffsetProperty =
        AvaloniaProperty.RegisterAttached<MarkdownTaskBox, CheckBox, int>("SourceOffset", NoOffset);

    private MarkdownTaskBox()
    {
    }

    /// <summary>
    /// The source offset recorded for <paramref name="box"/>, or <see cref="NoOffset"/> when it
    /// carries none.
    /// </summary>
    /// <param name="box">The rendered task box.</param>
    /// <returns>The offset of the character between the brackets.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="box"/> is <see langword="null"/>.</exception>
    public static int GetSourceOffset(CheckBox box)
    {
        ArgumentNullException.ThrowIfNull(box);

        return box.GetValue(SourceOffsetProperty);
    }

    /// <summary>
    /// Records <paramref name="value"/> as <paramref name="box"/>'s source offset.
    /// </summary>
    /// <param name="box">The rendered task box.</param>
    /// <param name="value">The offset of the character between the brackets.</param>
    /// <exception cref="ArgumentNullException"><paramref name="box"/> is <see langword="null"/>.</exception>
    public static void SetSourceOffset(CheckBox box, int value)
    {
        ArgumentNullException.ThrowIfNull(box);

        box.SetValue(SourceOffsetProperty, value);
    }

    /// <summary>
    /// Records <paramref name="sourceOffset"/> on <paramref name="box"/> and makes ticking it an
    /// edit of the document.
    /// </summary>
    /// <param name="box">The box that was just built, with its state already set.</param>
    /// <param name="sourceOffset">The offset of the character between the brackets.</param>
    /// <param name="toggle">
    /// Told the offset and the state asked for; answers whether the document took it. Must not throw.
    /// </param>
    /// <remarks>
    /// Driven by the state changing rather than by the click, so that every way a checkbox can be
    /// toggled — the pointer, and <c>Space</c> on a focused one — goes down the same path. The
    /// initial state is assigned by the caller before this is attached, so the render itself is not
    /// mistaken for a tick.
    /// </remarks>
    internal static void Attach(CheckBox box, int sourceOffset, Func<int, bool, bool> toggle)
    {
        SetSourceOffset(box, sourceOffset);

        bool reverting = false;

        box.IsCheckedChanged += (_, _) =>
        {
            // Putting the box back is itself a state change. Without this it would be reported as a
            // second toggle — writing back the very character the refusal declined to write.
            if (reverting)
            {
                return;
            }

            bool requested = box.IsChecked == true;

            if (toggle(sourceOffset, requested))
            {
                return;
            }

            reverting = true;
            box.IsChecked = !requested;
            reverting = false;
        };
    }
}
