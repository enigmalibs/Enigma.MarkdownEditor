namespace Enigma.MarkdownEditor.Core.Vim;

/// <summary>
/// What a <c>:s</c> asked for, read off its argument.
/// </summary>
/// <param name="Pattern">
/// The pattern as typed, or the empty string for "the last search pattern again".
/// </param>
/// <param name="Replacement">
/// What goes in its place, in .NET's replacement syntax, with vim's <c>\r</c> already a line break
/// and its <c>\t</c> a tab.
/// </param>
/// <param name="Global">Whether every match on a line goes, or only the first — the <c>g</c> flag.</param>
/// <param name="IgnoreCase">
/// <see langword="true"/> for the <c>i</c> flag, <see langword="false"/> for <c>I</c>, or
/// <see langword="null"/> to read the options.
/// </param>
internal readonly record struct VimSubstituteRequest(string Pattern, string Replacement, bool Global, bool? IgnoreCase);
