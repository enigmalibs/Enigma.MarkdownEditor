# FEATURE-E340 — Typography settings

**Status: DONE** · Multi-phase (2) · Roadmap: `docs/roadmap.md`
Branches at build time: `feature/feature-e340-phase01-editor-font`, `feature/feature-e340-phase02-preview-typography`.

## Objective

Make type in this application something you choose rather than something you are given: a real font picker for the editor, a Preview section that matches the Editor section, and defaults that land on a code font at a readable size.

## Context & constraints

- The stored `EditorFontFamily` is a **comma-separated fallback stack** (`"Cascadia Mono, Consolas, DejaVu Sans Mono, Liberation Mono, monospace"`). Avalonia 12.1.1 does not resolve it reliably, which is why the editor renders in the proportional UI font despite a monospace default — this is the actual cause of the "default font has variable character size" report, and the reason the stored value becomes a **single family name**.
- `Core` must stay Avalonia-free: it holds `AppSettings` and its validator, so it cannot probe installed fonts. Any "best available family" resolution happens in `Desktop`.
- `AppSettings`' own documentation states that the property defaults *are* the application defaults, with no second place a default is written down — a runtime-probed default must therefore be expressed as a value the property can hold, not as a second default elsewhere.
- The preview's type scale is a set of `const`s on `MarkdownPreviewTheme` (`BodyFontSize = 14`, `MonospaceFontFamily`, `HeadingFontSize(level)` returning absolute sizes). Making it configurable is a refactor of that class and its callers, not a settings row.
- Settings changes are applied when the settings window closes (`DocumentManagerViewModel` re-raises `FontFamily`/`FontSize`); the preview follows the same seam rather than inventing a second one.
- No new package. Font enumeration and monospace classification use Avalonia's own `FontManager` / `GlyphTypeface`.

## Design

### PHASE01 — Editor font picker, defaults & line numbers — **Status: DONE**

**`IFontCatalog` / `FontCatalog`** (new, `Desktop/Services`, singleton per `dotnet-di-design`):

- Enumerates `FontManager.Current.SystemFonts` **once** and caches — several hundred families on a typical Windows install, and the classification below measures glyphs.
- Classifies monospace by comparing the advance width of `i` against `W` in each family via its `GlyphTypeface`.
- Exposes `AllFamilies`, `MonospaceFamilies`, `IsAvailable(string)`, and `DefaultMonospaceFamily` — the first installed of Cascadia Mono, Consolas, JetBrains Mono, DejaVu Sans Mono, Liberation Mono, Courier New, falling back to the first family classified monospace, then to the system default.

**`AppSettings`:**

- `EditorFontFamily` now stores a **single** family name. The `DefaultEditorFontFamily` comma-stack constant is retired. `[Required(AllowEmptyStrings = false)]` is relaxed so that **empty means "automatic — best available monospace"**, resolved at runtime by the catalog; `[MaxLength(200)]` stays. This is what keeps the probe out of `Core` without inventing a second home for the default.
- `EditorFontSize` default `14` becomes `18`.

**Settings UI** (`Views/SettingsWindow.axaml`): the free-text `editors:TextEditor` becomes a `ComboBox` — the same control the Theme row already uses; `Enigma.Avalonia.Desktop` ships no combo editor.

- Lists **monospace families only**, each row rendered in its own font at a fixed row size, with "Automatic (best available monospace)" first.
- A stored value that is not installed (a comma stack from an older settings file, or a family present on another machine) is inserted at the top as `<value> (not installed)` and **left untouched in the file** until something else is picked. The editor meanwhile renders with the probed fallback.

**Line numbers:** AvaloniaEdit's `LineNumberMargin` gets ~4px of horizontal margin on each side, applied through `EditorAppearance` or an `EditorPaneView` style depending on whether the margin honours `Margin` inside `TextArea.LeftMargins`.

Acceptance criteria:
- The picker lists only installed monospace families; every row previews its own font; "Automatic" is first and is what a fresh install uses.
- A settings file holding the old comma stack opens without error, shows the stack marked `(not installed)`, renders with the probed monospace, and is not rewritten until the user picks a font.
- A fresh settings file yields editor font size 18; an existing file keeps whatever size it holds.
- Line numbers have visible breathing room on both sides.
- `Core.UnitTests`: `AppSettings` defaults, validation of the relaxed `EditorFontFamily` (empty accepted, over-length rejected), round-trip. `Desktop.UnitTests` (headless): `FontCatalog` classification and default probing, and the ViewModel's handling of an unavailable stored value.

### PHASE02 — Preview typography & Settings "Preview" section — **Status: DONE**

**New settings** on `AppSettings`:

| Property                | Type     | Range   | Default | Empty means              |
|-------------------------|----------|---------|---------|--------------------------|
| `PreviewFontFamily`     | `string` | <= 200  | empty   | the default reading font |
| `PreviewFontSize`       | `int`    | 6-72    | `18`    | —                        |
| `PreviewCodeFontFamily` | `string` | <= 200  | empty   | best available monospace |
| `PreviewLineHeight`     | `double` | 1.0-2.5 | `1.5`   | —                        |

**`MarkdownPreviewTheme` refactor:** the `BodyFontSize` and `MonospaceFontFamily` constants become a `MarkdownPreviewTypography` record (`FontFamily`, `CodeFontFamily`, `BodyFontSize`, `LineHeight`) with a `Default` instance. It is threaded through `MarkdownVisualBuilder` and reaches the control as a new `MarkdownPreview.Typography` styled property whose assignment re-renders, exactly like `Markdown`. The brush keys and `BindBrush` are untouched — colour stays a dynamic resource so a theme switch remains free.

**Heading scale becomes proportional:** `HeadingFontSize(level)` becomes `HeadingFontSize(level, bodySize)` returning `body` multiplied by `[1.714, 1.429, 1.214, 1.107, 1.036, 1.0]`. Those ratios are today's absolute sizes divided by 14, so at body 14 the output is identical to the current look; at 18 it gives 31/26/22/20/19/18. Without this the scale inverts — h3 (17) would be smaller than an 18pt body.

**Settings UI:** a new `SettingsCardExpander` "Preview" beside "Editor", with four rows — prose font (all installed families, "Automatic" first), size, code font (monospace list), line height. Applied live on settings-window close, through the same seam the editor uses; `MarkdownPreviewViewModel` exposes the typography record and `MainWindow` binds it.

Acceptance criteria:
- Each of the four settings visibly changes the preview after the settings window closes, with no restart.
- At body size 14 the rendered type scale is unchanged from today (guards the refactor); at 18 headings scale proportionally and no heading is smaller than body text.
- Code spans, fenced blocks and table cells all follow `PreviewCodeFontFamily`.
- Out-of-range values in a hand-edited settings file are reported by validation the same way existing ones are.
- `Core.UnitTests`: defaults, ranges, round-trip of the four new keys; a settings file written before this item loads with the new defaults applied. `Desktop.UnitTests`: `MarkdownPreviewThemeTests` updated for the ratio scale; a render asserts the typography record reaches code blocks and prose.

## Dependencies

PHASE02 depends on PHASE01 only for the shared `IFontCatalog`. Independent of `FEATURE-F3FA`, `FEATURE-C8F1` and `BUG-7675`.

## Out of scope

Font weight/style pickers · per-heading size overrides · a preview max reading width (considered and dropped — it changes every block's measured height, which `MarkdownPreview.ScrollToSourceLine` interpolates against) · editor line height · a preview colour theme separate from the app theme.
