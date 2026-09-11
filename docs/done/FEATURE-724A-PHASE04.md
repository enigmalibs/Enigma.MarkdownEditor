# FEATURE-724A-PHASE04 — Regex search, substitute & :set

**Status: DONE** · Phase 04 of 5 · Branch: `feature/feature-724a-phase04-search-substitute-set`
Plan: `docs/plan/FEATURE-724A.md`

## Summary

Search became a regular expression and a motion, and the ex line grew three commands the engine runs
itself. Nothing moved on `IVimBuffer`; everything is in the engine, and `README.md` §*Not implemented*
no longer opens with regex search and `:s`.

**A pattern is a .NET regular expression.** A new `VimPattern` is the one compile point for `/` `?`
`*` `#` and `:s`: it strips vim's `\c` / `\C`, settles the case from them, from a `:s` flag, or from
`ignorecase` + `smartcase` (a capital inside an escape such as `\W` does not count), and compiles with
`Multiline | CultureInvariant` and a one-second timeout. A pattern that will not compile is named on
the status bar as `Invalid pattern: Unterminated [] set.` — .NET's own description with its quoted
pattern-and-offset prefix stripped — and a match that runs past the timeout as `Invalid pattern:
Match timed out after one second.`; neither moves the caret, and neither becomes the last search.
`VimSearch` now enumerates the expression's non-overlapping matches over the whole text and takes the
first past the caret or the last before it, wrapping to the other end — which is vim's own reading
with its default `cpoptions`, so `/aa` from the first `aa` of `aaaa` lands on the third character,
and an empty match (`^`, `\b`, `x*`) is stepped over by one. Whether the wrap was allowed is the
engine's question: with `wrapscan` off, the step that would have gone round fails with vim's
`search hit BOTTOM without match for: foo` / `TOP`.

**Search is a motion, in every grammar.** `VimAction.SearchNext` / `SearchPrevious` are gone;
`VimMotion` gained `SearchForward`, `SearchBackward`, `SearchNext`, `SearchPrevious`,
`SearchWordForward` and `SearchWordBackward`. `n` `N` `*` `#` complete in the parser like `;` does,
so they work behind an operator (`dn`, `d*`) and over a selection for free. `/` and `?` cannot
complete in the parser — their target is still to be typed — so they complete as the
`BeginCommandLine` command that opens the line, *carrying the pending operator, count and register*,
and the engine keeps that opener beside the open line; Enter turns it into `Move(SearchForward)` or
`Operate(op, SearchForward)` with the typed text on the new `VimCommand.Pattern`, and runs it through
`Execute` like any other command. That one decision is what gives `d/foo`, `c?bar`, `3/foo`,
`"ad/foo`, `v/foo` extending the selection, and `.` repeating `d/foo` from the new caret, without a
special case for any of them. `Esc` on the line abandons the operator with it. Behind an operator the
range is exclusive both ways and goes through `VimRanges.ExclusiveLinewise`, so `d/^#` from the first
column takes whole lines. `n` follows the direction of the last search and `N` reverses it without
turning it round, exactly as `;` and `,` do for a find. `*` and `#` take vim's "keyword under or
after the caret" rule (falling back to the run of punctuation), search from the word's first
character so the word itself is the one skipped, wrap the word in `\b`, and are exempt from
`smartcase` — an exemption that follows the pattern into the `n` after them, as in vim.

**The ex line is a real parser.** `ExCommandParser.ParseLine` reads `[range] name[!] [argument]`
into an internal `VimExLine`: the range as symbolic `VimExAddress`es (`N`, `.`, `$`, `'<`, `'>`, `%`,
each with `+N`/`-N` after it, one or two with a comma) that the engine resolves against the buffer
and the last selection. The public `Parse` is unchanged in contract — the four file requests or
`null` — and is now `ParseLine` + `FileCommand`, which refuses a range or an argument on them, so
`:1,3w` is "Not an editor command" rather than a `:w`. The engine runs a bare range (`:42`, `:$`,
`:'>`, `:.+2` — a jump to the range's last line, first non-blank), `s` / `substitute` and `se` /
`set` itself; `w` `q` `wq` `x` still leave through `ExCommandRequested`.

**`:s` is vim's shape with .NET's replacement.** `VimSubstitute.TryParse` reads the argument with
any non-alphanumeric delimiter (not blank, `\`, `"` or `|`), an escaped delimiter, an optional
closing delimiter and replacement, and the flags `g` `i` `I` — anything else is `Trailing
characters: x`. The replacement keeps .NET's `$1` `$0` `$$` (`&` is only an ampersand) and honours
vim's `\r` (the buffer's own line break), `\t` and `\\`. `VimSubstitute.Apply` runs the expression
line by line over the resolved range, counts matches and lines, and hands back *one* replacement
spanning the first to the last changed line — so `:%s` is one `IVimBuffer.Replace` and one `u` — with
where the caret's line begins inside it (the last piece after a `\r` split). The engine reports
`3 substitutions on 2 lines` (singulars where they belong) or `Pattern not found: pat`, puts the
caret on the first non-blank of the last substituted line, and makes the pattern the one `n`
repeats; an empty pattern reuses it.

**`:set` over five options.** `VimOptions` gained `IgnoreCase`, `SmartCase` and `WrapScan` (vim's
defaults). `VimSet.TryApply` reads `name`, `noname`, `name!`, `name=value` and `name?` over
`ignorecase`/`ic`, `smartcase`/`scs`, `wrapscan`/`ws`, `shiftwidth`/`sw`, `expandtab`/`et`, several
arguments per line, stopping at the first refusal — `Invalid argument: sw=abc`, `Unknown option:
foo` — and shows values under their long names (`shiftwidth=4`, `noignorecase`). A bare `:set` shows
all five. `:3set` is `No range allowed`.

## Files/modules touched

**Created — `src/Enigma.MarkdownEditor.Core/Vim`**

- `VimPattern.cs` — `TryCompile` (case rules, `\c`/`\C` stripping, timeout, readable error),
  `Timeout`, `TimedOut`.
- `VimSearchWord.cs` — what `*`/`#` found: the word's start and the pattern that finds it.
- `VimExLine.cs`, `VimExRange.cs`, `VimExAddress.cs`, `VimExAddressKind.cs` — the parsed ex line and
  its symbolic range.
- `VimSubstitute.cs` — `TryParse` (delimiter, fields, escapes, flags) and `Apply` (per-line
  replacement, one write, message counts, caret piece); `VimSubstituteRequest.cs`,
  `VimSubstitution.cs` — its input and output shapes, the latter with the vim-worded `Message`.
- `VimSet.cs` — the five spellings over a `Switch[]` and a `Number[]` table.

**Modified — `src/Enigma.MarkdownEditor.Core/Vim`**

- `VimSearch.cs` — rewritten over `Regex`: non-overlapping `Matches`, first-after / last-before,
  always-wrap; `WordUnderCaret`.
- `VimRanges.cs` — `Search(buffer, caret, target)` through `ExclusiveLinewise`; class remarks.
- `VimOptions.cs` — `IgnoreCase`, `SmartCase`, `WrapScan`.
- `VimCommand.cs` — `string? Pattern`.
- `VimMotion.cs` — the six search motions. `VimAction.cs` — `SearchNext`/`SearchPrevious` removed;
  `BeginCommandLine` now `:` `/` `?` and documents the carried operator.
- `KeySequenceParser.cs` — `n` `N` `*` `#` in `MotionFor`; `/` `?` complete as `BeginCommandLine`
  carrying `_operator` and the count, above the operator dead-end and the visual cut-off; `/` `n`
  `N` removed from `ActionFor`; `TakesRegister` accepts `BeginCommandLine` with an operator; class
  remarks.
- `ExCommandParser.cs` — `ParseLine`, `FileCommand`, the range/address reader; `Parse` re-expressed
  over them; remarks rewritten.
- `VimEngine.cs` — `LastSearch` record replaces `string? _lastSearch`; `_commandLineOpener`;
  `OpenCommandLine(VimCommand)`; `RunCommandLine(line, opener)` builds the search command;
  `RunEx`, `GoToLine`, `Substitute`, `Set`, `ResolveRange`, `ResolveAddress`; `SearchTarget` +
  `Search(regex, …)` replace the old `Search(string, …)`; `IsSearch`; search arms in `ApplyMotion`
  and `ApplyOperator`; four new message constants; class remarks.
- `IVimBuffer.cs` — `ShowCommandLine` remarks mention `?`.

**Created — tests (`tests/Enigma.MarkdownEditor.Core.UnitTests/Vim`)**

- `VimRegexSearchTests.cs` — the dialect, non-overlap, empty matches, `\c` `\C`, `ignorecase`,
  `smartcase` (and its escape rule), invalid pattern (message, not remembered), timeout, `?`,
  direction of `n`/`N`, counts both ways, `wrapscan` off (all four shapes), `*`/`#` (keyword,
  punctuation, mid-word, blank-before, count, no word, smartcase exemption), a failing search stops
  a macro.
- `VimSearchMotionTests.cs` — every operator over `/` `?` `n` `N` `*` `#`, counts on either side,
  register, exclusive-linewise, one undo step, `.` (with and without a count, and over a change with
  its text), failure leaves the operator undone, the open line and `Esc` behind an operator, and the
  visual forms (extend, linewise, selection kept while typing / on `Esc` / on a miss, operator over
  the extended selection, `.` extent).
- `VimSubstituteTests.cs` — every range form, marks, the message, delimiters and escapes, `$1` `$0`
  `$$` `&` `\r` `\t` `\\`, caret rules, `i`/`I`/option, trailing characters, empty pattern with and
  without a last search, pattern remembered, not found, invalid, one undo step, unchanged lines
  between changed ones, anchors per line, bare `:s`, `substitute`, not what `.` repeats, scrolls.
- `VimSetTests.cs` — the five spellings over every option, `?` output, several arguments, every
  refusal, `No range allowed`, and the effect of each option on the next search or shift.

**Modified — tests**

- `KeySequenceParserTests.cs` — `n` `N` `*` `#` moved to the motion theories (normal and visual),
  `/`/`?` behind an operator (with count and register) and in visual mode, `"a/` dead, `?` as a
  command-line key; `n` `N` `/` dropped from the visual-refusal theory; `#` swapped for `&` in the
  "spells nothing" theory.
- `VimSearchTests.cs` — remarks re-scoped to the plain-character shape; the two "regex characters
  are literal" rows replaced by a case row; the visual and operator refusals replaced by a
  survives-`Reset` fact.
- `VimCommandLineTests.cs` — `?foo` rows; `:s/a/b/` refusal replaced by `:foo`, ranged file
  commands and malformed lines; the `:N`/`:$`/range jump theory, its scroll and column facts, and
  `Mark not set`.
- `VimVisualTests.cs` — `/` `n` `N` dropped from the visual-refusal theory. `ExCommandParserTests.cs`
  — `set ic`, `42`, `1,3w`, `%q` as non-file-commands; remarks.

**Modified — prose**

- `README.md` — §*Search and ex commands* rewritten (table + three paragraphs: the .NET dialect,
  search as a motion, `:s` and `:set`); §*Not implemented* drops regex search and `:s`, adds vim's
  regex dialect, the `c` flag / `&`, and highlighted or incremental search.
- `docs/RELEASE.md` — the *Vim smoke set* checklist item gains `?text`, `*`, `d/text`, `v/text`,
  `:%s/a/b/g` (+ `u`), `:set ic` then `/TEXT`, and `:42` (documentation freshness sweep).

**Workflow artifacts**

- `docs/roadmap.md` — PHASE04 `TODO` → `IN PROGRESS` → `DONE`; the item stays `IN PROGRESS`.
- `docs/plan/FEATURE-724A.md` — PHASE04's status.

## Deviations & follow-ups

- **A counted search that runs out under `nowrapscan` reports the message rather than staying
  silent.** The plan's contract says "a count that runs out of matches (without `wrapscan`) is
  silent", but its previous sentence says `wrapscan` off "stops at the buffer's ends with `search hit
  BOTTOM without match`", and vim reports E385 for `3n` as it does for `n`. Consistency and vim won;
  `3/foo` with one match ahead leaves the caret and says `search hit BOTTOM without match for: foo`.
  The message carries vim's full `for: <pattern>` tail, where the plan quoted only its prefix.
- **A bare `:s` is refused as `Not an editor command: s`.** In vim it repeats the last substitute
  (`&`), which the plan puts out of scope; with the repeat unavailable, refusing is the reading that
  cannot delete anything by surprise. A letter where the delimiter should be (`:sxaxbx`) is refused
  the same way, as it would be in vim.
- **`:` over a selection stays refused.** The plan's visual list is `/` `?` `n` `N` `*` `#`; `'<,'>`
  works from normal mode after leaving the selection. Vim's `:` in visual mode (pre-filling `:'<,'>`)
  is a one-line follow-up when wanted; the README keeps it under *Not implemented*.
- **`*` / `#` use vim's word rule, which is a superset of the plan's.** The plan says "`\b<word>\b`
  when the caret is on a word-class run, the escaped run alone for punctuation"; the engine prefers a
  keyword *under or after* the caret on its line (vim's `find_ident_under_cursor`), so `*` on the `(`
  of `(a)` searches for `a`, and only a line with no keyword left falls back to the punctuation run.
  A line with nothing left is a silent no-op that still stops a macro, like a find that misses.
- **`smartcase` is not applied to `*`/`#`, and that follows the pattern into `n`.** Vim's rule; the
  plan did not say. `LastSearch` carries a `SmartCase` flag for it.
- **`_lastSearch` became a `LastSearch(Pattern, Forward, SmartCase)` record** rather than the plan's
  separate `_lastSearchForward` field — the three travel together.
- **The search range is resolved in the engine, not in `VimRanges.Resolve`.** Where a search lands
  depends on the options, the last pattern and a compile that may fail, none of which `VimRanges`
  has; `VimRanges.Search(buffer, caret, target)` applies the exclusive and exclusive-linewise rules
  to a target the engine found. `VimRanges.Resolve` is unchanged.
- **The `:s` pattern is remembered after it compiles and before it runs**, so a substitute that
  finds nothing still leaves its pattern for `n`, and one that does not compile leaves nothing.
- **`:set` reads several arguments, and a bare `:set` shows all five options** — beyond the plan's
  five spellings, because both fall out of the loop and a bare `:set` had to do something.
  `:set sw=0` is refused (vim reads 0 as "use tabstop", which the engine has no value for).
- **`ExCommandParser`'s new surface is internal** (`ParseLine`, `FileCommand`, the ex records), as
  the plan asks; the tests reach it through the engine. The public `Parse` keeps its contract and
  its tests.
- **A backwards range is swapped silently** where vim asks "Backwards range given, OK to swap"; the
  engine has no way to ask, and the lines meant are the same.
- **Known limitation carried from PHASE01:** `VimEditorBehavior.Refresh()` re-seeds `ShiftWidth` and
  `ExpandTab` from the editor's options on every attach, so a `:set sw=8` is overwritten on the next
  settings change. The three search options are not seeded and so persist for the pane's lifetime;
  none of the five survives a restart. A follow-up could make `:set` values sticky or surface them
  in settings.
- **`:s` runs line by line**, as vim's does, so a pattern cannot match across a line break in a
  substitute (vim's `\n` in `:s` is not in scope); a `/` search can, because it runs over the whole
  text.
- **No line-ending noise.** Every touched and created file is LF, as `.gitattributes` requires.
  Nothing to recommend.

## Build/test evidence

- `dotnet build Enigma.MarkdownEditor.slnx -c Release`: **succeeded, 0 errors, 0 warnings**, with
  `TreatWarningsAsErrors` and `EnforceCodeStyleInBuild` on solution-wide.
- `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release`: **3106 passed, 0 failed, 0 skipped**
  — `Enigma.MarkdownEditor.Core.UnitTests` 2365 (2127 before this phase; the nine that failed on the
  unchanged tests were the deliberate changes — `/` `n` `N` no longer refused over a selection, `d/`
  no longer dead, regex characters no longer literal — and were updated) and
  `Enigma.MarkdownEditor.Desktop.UnitTests` 741 (unchanged).
- **Acceptance criteria, one by one.**
  - Regex, case rules, `\c`/`\C`, timeout and error reporting hold; `n`/`N` follow direction;
    `wrapscan` off stops with the message; `*`/`#` build the right pattern on words and punctuation ✓
    — `VimRegexSearchTests` (`APattern_IsARegularExpressionInDotNetsDialect`,
    `BackslashC_ForcesIgnoreCaseForThisSearch`, `SmartCase_ReadsACapitalInThePatternAsMeaningIt`,
    `AnInvalidPattern_IsNamedAndLeavesTheCaret`, `APatternThatRunsOutOfTime_IsReportedRatherThanWaitedFor`,
    `NAndCapitalN_FollowTheDirectionOfTheLastSearch`, `WithWrapScanOff_ASearchStopsAtTheEndAndSaysWhichEnd`,
    `StarAndHash_SearchForTheWordUnderTheCaret`).
  - `d/foo<CR>`, `dn`, `c?x<CR>`, `d*` operate on the right range; `v/foo<CR>` extends the
    selection ✓ — `VimSearchMotionTests` (`AnOperatorOverASearch_ActsUpToTheMatch`,
    `AChangeToASearch_EntersInsertModeWhereTheTextWas`, `InAVisualMode_ASearchExtendsTheSelection`).
  - `:s` over every range form and flag; `\r` splits a line; `$1` substitutes a group; the message and
    caret rule hold; `u` restores the whole substitute in one step ✓ — `VimSubstituteTests`
    (`TheRange_SaysWhichLines`, `TheVisualMarks_NameTheLastSelection`,
    `TheReplacement_IsDotNetsWithVimsEscapes`, `ALineBreakInTheReplacement_LeavesTheCaretOnTheLastPiece`,
    `TheCaseFlags_SettleTheCaseForThisCommand`, `ASubstituteOverManyLines_IsOneUndoStep`).
  - `:42`, `:$`, `:set` in all five spellings, and every error message ✓ —
    `VimCommandLineTests.ARangeOnItsOwn_GoesToItsLastLine`, `AMarkBeforeAnySelection_IsNotSet`;
    `VimSetTests` (`ASwitch_IsSetClearedAndFlipped`, `ANumber_IsSetWithEquals`,
    `AQuestionMark_ShowsTheValueUnderItsLongName`, `ABadValue_IsAnInvalidArgument`,
    `AnUnknownName_IsSaidToBe`, `ARangeOnSet_IsRefused`).
  - New Core test files `VimRegexSearchTests.cs`, `VimSearchMotionTests.cs`, `VimSubstituteTests.cs`,
    `VimSetTests.cs`; `VimSearchTests.cs`, `VimCommandLineTests.cs`, `ExCommandParserTests.cs`
    updated ✓ (and `KeySequenceParserTests.cs`, `VimVisualTests.cs`, which asserted the old refusals).
  - README §*Search and ex commands* rewritten for regex, `?`, `*` `#`, `:s`, `:N`, `:set`, with the
    ".NET syntax, not vim's" note; §*Not implemented* drops regex search and `:s` ✓. Release build
    0 warnings; full suite green ✓ — as above.
