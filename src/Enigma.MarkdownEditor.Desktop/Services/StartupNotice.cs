using System;
using System.Diagnostics.CodeAnalysis;

namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// A message the shell shows once, as soon as it has a window to show it in.
/// </summary>
/// <remarks>
/// Composition happens before any window exists, so a problem found there — an unreadable settings
/// file, say — has nowhere to go at the moment it is found. It is parked in this singleton instead
/// and picked up by the shell when the window opens.
/// </remarks>
public sealed class StartupNotice
{
    private StartupNotice()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StartupNotice"/> class.
    /// </summary>
    /// <param name="title">Short heading, a few words.</param>
    /// <param name="message">What happened and what it means for the user.</param>
    /// <exception cref="ArgumentException"><paramref name="title"/> or <paramref name="message"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="title"/> or <paramref name="message"/> is <see langword="null"/>.</exception>
    public StartupNotice(string title, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Title = title;
        Message = message;
    }

    /// <summary>
    /// Gets the "nothing to say" notice — the normal case, and the container's default registration.
    /// </summary>
    public static StartupNotice None { get; } = new StartupNotice();

    /// <summary>
    /// Merges two notices, so that two files failing at once are still one message.
    /// </summary>
    /// <param name="first">The first notice, or <see cref="None"/>.</param>
    /// <param name="second">The second notice, or <see cref="None"/>.</param>
    /// <returns>
    /// Whichever notice has something to say when only one does — the same instance, not a copy — and
    /// a combined notice when both do.
    /// </returns>
    /// <remarks>
    /// The shell shows one notice, because the InfoBar shows one message: a second
    /// <c>ShowAsync</c> replaces the first rather than queueing behind it.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static StartupNotice Combine(StartupNotice first, StartupNotice second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        if (!second.HasNotice)
        {
            return first;
        }

        if (!first.HasNotice)
        {
            return second;
        }

        return new StartupNotice(
            "Some saved state could not be read",
            $"{first.Title}. {first.Message}{Environment.NewLine}{Environment.NewLine}" +
            $"{second.Title}. {second.Message}");
    }

    /// <summary>
    /// Gets the heading, or <see langword="null"/> when there is nothing to show.
    /// </summary>
    public string? Title { get; }

    /// <summary>
    /// Gets the body, or <see langword="null"/> when there is nothing to show.
    /// </summary>
    public string? Message { get; }

    /// <summary>
    /// Gets a value indicating whether there is anything to show at all.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Title), nameof(Message))]
    public bool HasNotice => Title is not null && Message is not null;
}
