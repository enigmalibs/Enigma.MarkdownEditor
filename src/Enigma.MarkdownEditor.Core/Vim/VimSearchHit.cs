namespace Enigma.MarkdownEditor.Core.Vim;

/// <summary>
/// Where a search landed, and whether it had to go round the end of the buffer to get there.
/// </summary>
/// <param name="Offset">The first character of the match.</param>
/// <param name="Wrapped">
/// Whether the match was found only after starting again from the other end of the buffer.
/// </param>
/// <remarks>
/// The wrap is carried back rather than worked out by the caller, because the caller cannot: from an
/// offset alone, a match found behind the caret is indistinguishable from one the search walked
/// backwards to. And it has to be known — the wrap is the one thing vim says out loud about a search
/// that succeeded.
/// </remarks>
internal readonly record struct VimSearchHit(int Offset, bool Wrapped);
