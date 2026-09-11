using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Enigma.MarkdownEditor.Core.Settings;

namespace Enigma.MarkdownEditor.Core.Session;

/// <summary>
/// Where the editor was left: the notes that were open, and the shape the window was in.
/// </summary>
/// <remarks>
/// <para>
/// Separate from <see cref="AppSettings"/> on purpose. Settings are choices — a theme, a font, whether
/// vim is on — and a user expects them to survive being copied to another machine. This file is a
/// position, rewritten every time a tab is opened or a splitter is dragged, and worth nothing on a
/// machine with different screens and different folders. Two files, two lifetimes, and a session that
/// cannot be read never costs anybody a setting.
/// </para>
/// <para>
/// Every property has a default that is a usable first run, so a missing or quarantined file simply
/// starts the editor the way a fresh install does.
/// </para>
/// </remarks>
public sealed class SessionState
{
    /// <summary>
    /// The file-format version this build writes and understands.
    /// </summary>
    public const int CurrentVersion = 1;

    /// <summary>
    /// The version of the file's format.
    /// </summary>
    /// <remarks>
    /// Rejected rather than read as best it can be when it comes from a future version, exactly as the
    /// workspaces file is: the newer file is quarantined intact instead of being rewritten in this
    /// version's shape, and the cost of that is one session — which is the cheapest thing the editor
    /// persists.
    /// </remarks>
    [Range(1, CurrentVersion)]
    public int Version { get; set; } = CurrentVersion;

    /// <summary>
    /// The workspace that was open when this session was written, or <see langword="null"/> when there
    /// was none.
    /// </summary>
    /// <remarks>
    /// A stamp, never a source. Which workspace opens is decided by
    /// <see cref="Workspaces.WorkspacesConfig.LastActiveWorkspaceId"/> and nothing else — one fact, one
    /// file. What this is for is the question that fact cannot answer: whether the tabs below still
    /// belong to the workspace that actually opened. When they do not, they are dropped, because
    /// reopening one workspace's notes inside another is worse than reopening nothing.
    /// </remarks>
    public Guid? ActiveWorkspaceId { get; set; }

    /// <summary>
    /// The open documents, by absolute path, in tab order.
    /// </summary>
    public List<string> OpenDocuments { get; set; } = [];

    /// <summary>
    /// Which of <see cref="OpenDocuments"/> was on screen, or <c>-1</c> when none was.
    /// </summary>
    public int ActiveDocumentIndex { get; set; } = -1;

    /// <summary>
    /// Which of the editor and the preview the window was showing.
    /// </summary>
    [EnumDataType(typeof(ViewMode))]
    public ViewMode ViewMode { get; set; } = ViewMode.Split;

    /// <summary>
    /// Which of the sidebar's two tabs was showing.
    /// </summary>
    [EnumDataType(typeof(SidebarTab))]
    public SidebarTab SidebarTab { get; set; } = SidebarTab.Explorer;

    /// <summary>
    /// Whether the sidebar was shown at all.
    /// </summary>
    /// <remarks>
    /// The only pane flag here, and deliberately: whether the editor and the preview are shown is
    /// <see cref="ViewMode"/>'s to say, and a second flag saying it again would be a second flag able
    /// to disagree.
    /// </remarks>
    public bool IsSidebarVisible { get; set; } = true;

    /// <summary>
    /// Where the window was, or <see langword="null"/> when nothing has been remembered yet.
    /// </summary>
    public WindowPlacement? Window { get; set; }

    /// <summary>
    /// How the panes were divided, or <see langword="null"/> when nothing has been remembered yet.
    /// </summary>
    public PaneLayout? Panes { get; set; }
}
