using System.ComponentModel.DataAnnotations;

namespace Enigma.MarkdownEditor.Core.Settings;

/// <summary>
/// Everything the application persists between runs, as one hand-editable JSON file.
/// </summary>
/// <remarks>
/// <para>
/// The property defaults below are the application's defaults: a missing, empty or quarantined
/// settings file falls back to exactly this object, so there is no second place where a default is
/// written down.
/// </para>
/// <para>
/// The DataAnnotations attributes state what a single property must look like; the rules that span a
/// property's shape rather than its range live in <see cref="AppSettingsValidator"/>. Both halves run
/// after every load and before every write, and report together.
/// </para>
/// </remarks>
public sealed class AppSettings
{
    /// <summary>
    /// Which theme variant the window uses. Applied live — no restart.
    /// </summary>
    [EnumDataType(typeof(AppTheme))]
    public AppTheme Theme { get; set; } = AppTheme.System;

    /// <summary>
    /// Whether the editor starts in vim emulation. Consumed from FEATURE-11AA on.
    /// </summary>
    public bool VimEnabled { get; set; } = true;

    /// <summary>
    /// Whether a modified document is written back automatically. Consumed from FEATURE-3C44 on.
    /// </summary>
    public bool AutosaveEnabled { get; set; }

    /// <summary>
    /// How long the editor stays idle before an autosave runs. Consumed from FEATURE-3C44 on.
    /// </summary>
    [Range(1, 300)]
    public int AutosaveDelaySeconds { get; set; } = 2;

    /// <summary>
    /// Whether the workspace tree lists every file rather than markdown alone. Hidden and dot-prefixed
    /// entries stay out either way.
    /// </summary>
    public bool ShowAllFiles { get; set; }

    /// <summary>
    /// The single font family the editor renders with, or empty for "automatic".
    /// </summary>
    /// <remarks>
    /// <para>
    /// One family name, not a comma-separated fallback stack: Avalonia resolves only the first name
    /// of a stack, so the rest were never fallbacks at all — they were a string the editor silently
    /// failed to make sense of and rendered the proportional UI font instead of.
    /// </para>
    /// <para>
    /// Empty is the default, and it means "the best monospace family this machine has". It has to be
    /// expressed as a value rather than as a name written down here, because which family that is can
    /// only be answered by probing the installed fonts — and this assembly holds no reference to the
    /// UI framework that could. Empty is therefore accepted while <see langword="null"/> is not: the
    /// application resolves the former, and nothing sensible can be done with the latter.
    /// </para>
    /// </remarks>
    [Required(AllowEmptyStrings = true)]
    [MaxLength(200)]
    public string EditorFontFamily { get; set; } = string.Empty;

    /// <summary>
    /// The editor's font size in points. Consumed from FEATURE-3C44 on.
    /// </summary>
    /// <remarks>
    /// 16 as of FEATURE-414B, down from the 18 this shipped with. Nothing migrates: a settings file
    /// that already holds 18 keeps 18, because a default governs a fresh profile and a file that does
    /// not mention the key — not a value the user can already see and edit in the settings dialog. So
    /// there is no migration to go looking for.
    /// </remarks>
    [Range(6, 72)]
    public int EditorFontSize { get; set; } = 16;

    /// <summary>
    /// Whether the editor wraps long lines. Consumed from FEATURE-3C44 on.
    /// </summary>
    public bool WordWrap { get; set; } = true;

    /// <summary>
    /// The single font family the preview sets prose in, or empty for "the default reading font".
    /// </summary>
    /// <remarks>
    /// One family name rather than a fallback stack, and empty rather than a name written down here,
    /// for the same two reasons as <see cref="EditorFontFamily"/>: a stack is a string the renderer
    /// reads the first name out of, and which family is the right one to read a document in depends on
    /// the machine. Unlike the editor's, this one is not required to be fixed-width — prose is prose.
    /// </remarks>
    [Required(AllowEmptyStrings = true)]
    [MaxLength(200)]
    public string PreviewFontFamily { get; set; } = string.Empty;

    /// <summary>
    /// The size the preview sets prose at, in points. The headings scale in proportion with it, and
    /// so do the preview's own asides; code is sized by <see cref="PreviewCodeFontSize"/> instead.
    /// </summary>
    [Range(6, 72)]
    public int PreviewFontSize { get; set; } = 18;

    /// <summary>
    /// The single font family the preview sets code in — code spans and fenced blocks — or empty for
    /// "the best monospace family this machine has".
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="PreviewFontFamily"/> because a document is read in one font and its
    /// code is read in another; separate from <see cref="EditorFontFamily"/> because the pane the
    /// document is written in and the pane it is read in are two different readings.
    /// </remarks>
    [Required(AllowEmptyStrings = true)]
    [MaxLength(200)]
    public string PreviewCodeFontFamily { get; set; } = string.Empty;

    /// <summary>
    /// The size the preview sets code at — code spans and fenced blocks alike — in points.
    /// </summary>
    /// <remarks>
    /// Absolute, like <see cref="EditorFontSize"/> and <see cref="PreviewFontSize"/>, rather than an
    /// offset from the prose size it used to be derived as. A fixed-width face at the same nominal
    /// size reads larger than the prose beside it, and by how much depends on the two families the
    /// reader picked — which is a judgement no formula makes for them. Absolute also keeps the file
    /// hand-editable in the obvious way: the number in it is the number of points.
    /// </remarks>
    [Range(6, 72)]
    public int PreviewCodeFontSize { get; set; } = 14;

    /// <summary>
    /// How far apart the preview's lines sit, as a multiple of the font size they are set at.
    /// </summary>
    /// <remarks>
    /// A multiplier rather than an absolute measurement, because it has to hold for a heading as well
    /// as for a paragraph. Below 1 the lines would overlap; above about 2.5 a paragraph stops reading
    /// as one.
    /// </remarks>
    [Range(1.0, 2.5)]
    public double PreviewLineHeight { get; set; } = 1.5;

    /// <summary>
    /// The folder, relative to the document, that pasted or dropped images are written into.
    /// Consumed from FEATURE-3C44 on.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    [MaxLength(64)]
    public string ImageFolderName { get; set; } = "assets";

    /// <summary>
    /// Whether the log file records <c>Debug</c> entries as well as <c>Information</c> and above.
    /// Read once while the container is composed, so a change takes effect on the next start.
    /// </summary>
    public bool DebugLogging { get; set; }
}
