# FEATURE-23AB — Vim find motions & text objects

**Status: DONE** · 2 phases · Roadmap: `docs/roadmap.md`
Planned on `develop`; each phase cuts its own branch from `HEAD` at build time.

## Objective

Take the two most-wanted entries off the vim engine's own deferred list. `FEATURE-11AA.md` line 25
reads *"Explicitly out of v1 (most-wanted follow-ups first): text objects (`iw`, `i(`, …), `.`
repeat, marks, …"*, and `README.md` §*Not implemented* says the same to the user. This item
implements the character-find family — which that list did not even mention, and which is the other
thing a vim user reaches for on day one — and then text objects, and rewrites both statements.

Two phases, in order of size:

| Phase | Adds |
|---------|-----------------------------------------------|
| PHASE01 | `f<c>` `F<c>` `t<c>` `T<c>` and `;` `,` |
| PHASE02 | `iw` `aw`, bracket objects, quote objects |

**What is already there and is not this item's business:** the `c` operator. `VimOperator.Change` is
fully wired and `cw` `ce` `c$` `cc` `C` all work today, covered by `VimOperatorTests.cs`. The
`ciw`/`ci(` forms that prompted this item need *text objects*, not the operator.

## Context & constraints

- **`Enigma.MarkdownEditor.Core` never references Avalonia.** The whole of this item lives in
  `src/Enigma.MarkdownEditor.Core/Vim/`. Nothing in `Desktop` changes.
- **No `IVimBuffer` change.** `CharAt`, `GetLine`, `GetLineByOffset`, `TextLength` and `LineCount`
  are enough for every motion and object here. Widening the buffer contract would force
  `FakeVimBuffer` and the real AvaloniaEdit adapter to move together for no gain.
- **No key routing change.** `Views/VimEditorBehavior.cs:256` feeds every printable character to the
  engine through `InputElement.TextInputEvent`, one `VimKey.Typed` per character. `f`, `;`, `,`,
  `(`, `"` and `` ` `` therefore already reach `VimEngine.HandleKey` — including from a non-US
  layout, because the platform's own text input has already resolved the layout and any dead key.
  This is the same path `r<char>` relies on.
- **`TreatWarningsAsErrors` is on solution-wide**, with `EnforceCodeStyleInBuild`. A **0-warning**
  Release build, per phase.
- **The parser already knows how to wait for a literal character.** `KeySequenceParser._awaitingLiteral`
  holds the sequence open for `r`, and takes the next key literally whatever it spells — `rd` writes
  a `d`. `f`, `F`, `t` and `T` need exactly that state, and `VimCommand.Argument` is the `char` field
  that already carries the result.
- **The parser already knows which mode it is reading for.** `Feed(VimKey, VimMode)` takes the mode
  and runs a narrower grammar in visual mode; PHASE02's visual-mode text objects widen that grammar
  rather than introduce a second entry point.
- **`VimEngine.Reset()` deliberately keeps state that outlives a buffer.** Its doc comment
  (`VimEngine.cs:248-254`) states the rule: the register and the search pattern survive a document
  swap because "vim's own outlive the buffer they were filled from". `_lastFind` joins them.
- **`VimMotion`'s contract is written down and must hold.** Its remarks say every member is "both a
  place to move the caret to and a stretch of text to operate on — except `WholeLine`, which is only
  the second". Find motions satisfy it and belong in that enum. Text objects do **not** — there is
  nowhere for `iw` to move a caret to on its own — so they get their own type rather than six more
  exceptions to a one-exception rule.
- **Silence is this engine's existing answer to a motion that cannot go anywhere.** `9j` on a
  three-line file clamps without a word. `NotACommand` is reserved for keys that spell nothing, not
  for commands that ran and found nothing.
- **Test harness:** `VimScript.Run("a|bc", "x")` → `"a|c"`, with `a[b|c]d` for a selection and its
  caret. Every case in this item is expressible in it; no harness change is needed.

---

## PHASE01 — Character find & repeat

**Status: DONE**

### Objective

`f<c>` `F<c>` `t<c>` `T<c>`, and `;` / `,` to repeat the last one forwards / backwards. Usable as
motions, behind an operator, and with counts on either side.

### Behaviour contract

| Keys | Where the caret lands |
|-------------|--------------------------------------------------------|
| `f<c>` | forward to the next `<c>`, **on** it |
| `F<c>` | backward to the previous `<c>`, **on** it |
| `t<c>` | forward to just **before** the next `<c>` |
| `T<c>` | backward to just **after** the previous `<c>` |
| `;` | the last of those four again, same direction |
| `,` | the last of those four again, opposite direction |

- **Line-bound, always.** A find never crosses a line break — vim's own rule, and the reason `f` is
  useful: it is a within-the-line jump, not a search. `/` already covers the other case.
- **Counts multiply as everywhere else.** `3fa` is the third `a`; `2df,` deletes through the second
  comma; `2;` repeats twice. The existing `TotalCount` handles both sides of the operator.
- **Behind an operator, forward finds are inclusive.** `df,` removes the comma; `dt,` stops before
  it. Backward finds are exclusive of the caret's own character and inclusive of the target for `F`
  (`dF,` removes the comma and everything up to but not including the caret), exclusive for `T`.
- **`;` after `t` advances.** After `ta` the caret sits immediately before an `a`; a naive repeat
  would re-find that same `a` and move nowhere. `;` therefore starts its scan one character further
  on for `t`, and one further back for `T`. This is Neovim's and modern Vim's default (`cpoptions`
  without the `;` flag) and the only behaviour under which holding `;` makes progress.
- **`,` does not overwrite the memory.** After `fa , , ;`, the `;` still repeats `fa` forwards. `,`
  reverses a find; it does not become one.
- **No match is a silent no-op.** The caret does not move, the buffer does not change, the status bar
  is not written to, and a pending operator is abandoned without a message.
- **`_lastFind` survives `Reset()`**, alongside `Register` and `_lastSearch`.
- **`;` and `,` with no find yet** are silent no-ops. (Not the `"No previous search pattern"`
  treatment `n` gets — that message exists because `/` is a mode the user visibly entered and may
  reasonably think is still loaded; `;` has no such surface.)

### Design

**1 — `VimMotion` gains six members**, each documented in the file's existing voice:

```
FindForward,        // f<c>
FindBackward,       // F<c>
TillForward,        // t<c>
TillBackward,       // T<c>
RepeatFind,         // ;
RepeatFindReverse,  // ,
```

**2 — `KeySequenceParser`** (`KeySequenceParser.cs`)

- A new field `private VimMotion _awaitingFind;`, `VimMotion.None` when nothing is pending. It is
  read in `Feed` **immediately after** the `_awaitingLiteral` block and before `_awaitingSecondG`,
  because — exactly like `r` — the next key is a character to find, not a command:

  ```
  if (_awaitingFind != VimMotion.None)
  {
      VimMotion pending = _awaitingFind;
      _awaitingFind = VimMotion.None;

      return key.IsCharacter ? FindMotion(pending, key.Character) : Dead();
  }
  ```

  `FindMotion` mirrors the existing `Motion` helper but carries the character in `Argument`:
  `Complete(new VimCommand(action, pending, TotalCount, _operator, character))` where `action` is
  `VimAction.Move` when no operator is waiting and `VimAction.Operate` when one is.
- `HasPendingSequence` gains `|| (_awaitingFind != VimMotion.None)`, and `Reset()` clears it. Both
  matter: `Esc` must cancel a half-typed `df`, and the status bar reads that property.
- `MotionFor` is **not** where `f`/`F`/`t`/`T` go — they do not complete a motion, they open one. A
  new branch in `Feed`, placed with the other openers (beside the `'g'` branch and before
  `MotionFor`):

  ```
  if (key.IsCharacter && (FindOpenerFor(key.Character) is { } find))
  {
      _awaitingFind = find;

      return Pending();
  }
  ```

  This sits **before** the visual-mode cut-off, so `df,`-style finds work in visual mode too — where
  a find is a motion that extends the selection, which is what the existing motion handling already
  does with any other motion.
- `MotionFor` **does** gain `';' => VimMotion.RepeatFind` and `',' => VimMotion.RepeatFindReverse`,
  because those two *are* complete motions the instant they are typed.

**3 — `VimEngine`** (`VimEngine.cs`)

- New state, beside `_lastSearch`:

  ```
  private char _lastFindCharacter;
  private VimMotion _lastFind = VimMotion.None;
  ```

  Not cleared by `Reset()`. Add the reason to `Reset()`'s existing remarks rather than leaving the
  omission to be read as an oversight.
- A single resolver both `ApplyMotion` and `VimRanges` go through, so the rule lives in one place:

  ```
  private int? FindTarget(int caret, VimMotion motion, char character, int count)
  ```

  It returns `null` for no match. Steps: resolve `RepeatFind` / `RepeatFindReverse` against
  `_lastFind` (returning `null` when there is none, and mirroring the direction for `,`); take the
  line via `GetLineByOffset`; scan within `[line.Offset, line.EndOffset)` only; step `count` times,
  each step starting from the previous hit; apply the `t`/`T` ±1 adjustment **after** the last step;
  and for a `;`/`,` that resolves to a till, start the first step one character further along.
  A count that runs out of matches returns `null` — vim's rule: `9fa` with three `a`s on the line
  does nothing at all rather than landing on the third.
- `_lastFind` / `_lastFindCharacter` are recorded by `f` `F` `t` `T` **whether or not they matched**,
  and are never written by `;` or `,`. Recording on a miss is deliberate: `fz` on a line with no `z`
  then `;` on the next line should find the `z` there.
- `ApplyMotion` gains one arm covering all six, before the `default`:
  `if (FindTarget(caret, motion, argument, count) is { } target) SetCaret(target);` — and falls
  through to `_desiredColumn = NoDesiredColumn` like every other horizontal motion. `ApplyMotion`
  therefore takes the command's `Argument`; pass the `VimCommand` itself rather than growing the
  parameter list a second time.

**4 — `VimRanges.Resolve`** gains the operator side. It has no `VimEngine`, so `FindTarget` moves to
a static on `VimRanges` (or a new `VimFind` static class, sibling to `VimWords`) taking the last-find
as parameters, and `VimEngine` calls it too. **`VimFind` is the better home** — `VimWords` sets the
precedent that "where does this key land in this text?" is its own file, and `VimRanges` is already
the longest of the three.

Ranges, with `target` from `VimFind`:

| Motion | Range |
|---------------------|-------------------------------------------|
| `FindForward` | `Characters(caret, target + 1)` — inclusive |
| `TillForward` | `Characters(caret, target + 1)` — `target` is already one short |
| `FindBackward` | `Characters(target, caret)` |
| `TillBackward` | `Characters(target, caret)` |

`RepeatFind` / `RepeatFindReverse` resolve to one of those four first, then take its rule. A `null`
target returns `null` from `Resolve`, which `ApplyOperator` already handles by returning without
touching the buffer — the silent no-op falls out of the existing code and needs no new branch.

### Acceptance criteria

- `f` `F` `t` `T` `;` `,` behave as the contract table says, as motions and behind `d`, `y` and `c`.
- Counts work on either side and multiply: `3fa`, `d2f,`, `2d3fx`.
- No find crosses a line break, in either direction, with or without a count.
- `;` after `t`/`T` advances; `,` reverses without overwriting the memory; `;`/`,` before any find
  do nothing.
- Every no-match case leaves caret, buffer, mode and status bar untouched.
- `Esc` cancels a half-typed `df`; `HasPendingSequence` is true while one is open.
- `_lastFind` survives `VimEngine.Reset()`.
- New file `tests/…Core.UnitTests/Vim/VimFindTests.cs` covers all of the above.
- Release build 0 warnings; full suite green.

---

## PHASE02 — Text objects

**Status: DONE**

### Objective

`iw` `aw` and the bracket and quote objects, behind `d`, `y`, `c` and in visual mode.

### Behaviour contract

| Object | Covers |
|-------------------|--------------------------------------------------------------|
| `iw` | the word under the caret, or the run of blanks if on one |
| `aw` | that, plus the blanks after it — or before it, at end of line |
| `i(` `i)` `ib` | inside the enclosing `(` … `)`, delimiters excluded |
| `a(` `a)` `ab` | the same, delimiters included |
| `i[` `i]` / `a[` `a]` | the same for `[` … `]` |
| `i{` `i}` `iB` / `a{` `a}` `aB` | the same for `{` … `}` |
| `i<` `i>` / `a<` `a>` | the same for `<` … `>` |
| `i"` `i'` `` i` `` | inside the surrounding quote pair |
| `a"` `a'` `` a` `` | the same, quotes included |

- **Word classification is `VimWords`' existing three classes** — blank / word / punctuation — so
  `foo.bar` is three words and `foo_bar` one, matching `w` `b` `e`. `iw` on a blank covers the run
  of blanks, which is vim's rule and what makes `daw` work in both directions.
- **Brackets scan the whole buffer, counting nesting.** `ci(` inside `((a|b))` takes the inner pair.
  A multi-line pair is matched, which is the point: `ci{` over a fenced block is a reason to have
  the feature at all.
- **Quotes match within the current line only.** Otherwise an apostrophe in *It's* three paragraphs
  up captures half the document. No nesting: quotes are matched by pairing off from the start of the
  line, so the caret between the first and second `"` is inside, between the second and third is
  outside.
- **Counts apply to word objects and are dropped on delimiter objects.** `d2aw` takes two words,
  `d3iw` takes word/space/word. `d2i(` behaves as `di(` — real vim would walk one nesting level
  outwards; that is not implemented and the count is ignored rather than rejected. Record this in
  the completion doc as a known deviation from vim.
- **Visual mode**: `viw`, `vi(`, `va"` set the selection to the object's range and stay in visual
  mode. In `V`-line mode the object still resolves, and the selection stays linewise over the lines
  the object touches.
- **No match is a silent no-op** — caret, buffer, mode and status bar untouched, still in normal (or
  still in visual) mode. This includes `ci(` outside any parens and `iw` at the very end of an empty
  buffer.
- **An empty pair is not a no-match.** `ci(` on `(|)` has an empty inner range and must still enter
  insert mode between the parens — the same rule `ApplyOperator` already applies to `c0`.

### Design

**1 — `VimTextObject`** — new enum in `Core/Vim/`, with the two-part shape vim's grammar has:

```
None,
InnerWord, AroundWord,
InnerParen, AroundParen,
InnerBracket, AroundBracket,
InnerBrace, AroundBrace,
InnerAngle, AroundAngle,
InnerDoubleQuote, AroundDoubleQuote,
InnerSingleQuote, AroundSingleQuote,
InnerBacktick, AroundBacktick,
```

A separate type from `VimMotion` for the reason the constraints section gives: `VimMotion`'s own
documented contract is that a member is somewhere a caret can go, and a text object is not.

**2 — `VimCommand`** gains `VimTextObject TextObject = VimTextObject.None` as its last positional
member, plus a `VimCommand.Object(VimOperator, VimTextObject, int)` factory beside `Move` and
`Operate`. `Action` stays `VimAction.Operate` — the operator half is unchanged, only the noun is a
different kind of noun. In visual mode the parser emits `VimAction.Move` with a text object and no
operator, which is what `viw` means: change what is selected, act later.

**3 — `KeySequenceParser`** — a new pending state `private char _awaitingObject;` (`'\0'`, `'i'` or
`'a'`), read in `Feed` after `_awaitingFind`:

- `'i'` / `'a'` open it **only when an operator is waiting or the mode is visual**. Outside both,
  `i` and `a` keep their existing meaning as insert commands — this is the one place the two
  grammars genuinely collide, and vim resolves it the same way. The existing comment at the
  `_operator != VimOperator.None` guard (*"'di' is not 'delete, then insert'"*) documents the old
  behaviour and must be rewritten, not left contradicting the code.
- The key after `i`/`a` selects the object from a table; anything not in it is `Dead()`.
- `HasPendingSequence` and `Reset()` cover it.

**4 — `VimTextObjects`** — new static class, sibling to `VimWords` and `VimFind`:

```
public static VimTextRange? Resolve(IVimBuffer buffer, int caret, VimTextObject o, int count)
```

Three private resolvers behind it — words (uses `VimWords`' class rule directly, and honours
`count`), brackets (outward nesting walk over the whole buffer, `count` ignored), quotes (pair-off
within the line, `count` ignored). Always characterwise. `null` for no match.

**5 — `VimEngine`**

- `ApplyOperator` takes the text object and prefers it over the motion when it is set:
  `VimTextObjects.Resolve(...)` instead of `VimRanges.Resolve(...)`. Everything downstream — the
  empty-range rule, `Yank` / `Delete` / `Change` — is unchanged.
- A new `SelectTextObject(VimTextObject, int)` for the visual case: resolve, then
  `_visualAnchor = range.Start`, `SetCaret(range.End - 1)`, `ShowSelection()`. Returns without
  touching anything when the resolve is `null`.
- `Execute`'s `VimAction.Move` arm routes to `SelectTextObject` when `command.TextObject` is set.

### Acceptance criteria

- Every object in the contract table resolves correctly behind `d`, `y` and `c`, and in `v`.
- `iw`/`aw` follow `VimWords`' three classes and honour a count; delimiter objects ignore theirs.
- Brackets nest and span lines; quotes do neither and stay on the line.
- `ci(` on an empty pair enters insert mode between the delimiters.
- Every no-match case is a silent no-op that leaves the mode alone.
- `i` and `a` still insert when no operator is pending and the mode is not visual.
- New file `tests/…Core.UnitTests/Vim/VimTextObjectTests.cs` covers all of the above.
- `README.md` §*Not implemented* no longer lists text objects, and its §*Operators* / §*Motions*
  tables gain the new keys; `docs/plan/FEATURE-11AA.md` line 25's out-of-v1 list is corrected.
- Release build 0 warnings; full suite green.

## Dependencies

PHASE02 after PHASE01 — not technically coupled, but both touch `KeySequenceParser.Feed`'s pending
chain and `VimEngine.ApplyOperator`, and building them in order avoids resolving the same overlap
twice.

## Out of scope

`.` repeat · marks · named registers · macros · regex search · `:s` substitute · jump lists · folds ·
`W`/`B`/`E` WORD motions and their `iW`/`aW` objects · sentence (`is`/`as`), paragraph (`ip`/`ap`)
and tag (`it`/`at`) objects · `2i(` walking a nesting level outwards · any `IVimBuffer`, `Desktop`
or settings change.
