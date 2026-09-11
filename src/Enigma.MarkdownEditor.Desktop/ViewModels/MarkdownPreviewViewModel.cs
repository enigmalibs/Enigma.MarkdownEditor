using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using Enigma.Configuration;
using Enigma.MarkdownEditor.Core.Markdown;
using Enigma.MarkdownEditor.Core.Settings;
using Enigma.MarkdownEditor.Desktop.Controls;
using Enigma.MarkdownEditor.Desktop.Services;
using Microsoft.Extensions.Logging;

namespace Enigma.MarkdownEditor.Desktop.ViewModels;

/// <summary>
/// State for the preview pane: the markdown the active document holds and the folder its images are
/// resolved against.
/// </summary>
/// <remarks>
/// <para>
/// The seam between the editor pane and the renderer. It reads two things off whichever tab is
/// active — the text and the folder — and owns nothing about how either is drawn. The control it
/// feeds knows no more than those two strings, which is what keeps the whole render assertable
/// without a window.
/// </para>
/// <para>
/// It reads a third thing off the settings — the type the document is set in — which the renderer
/// takes as one record rather than as four properties, because every one of them is a re-render and
/// four assignments would be four of them.
/// </para>
/// <para>
/// It refreshes at once when the active tab changes, when a document is reloaded from disk under it,
/// and when a save moves the file to a different folder. Typing is the one thing it follows on a
/// delay: the text is republished <see cref="LiveUpdateDelay"/> after the last keystroke, because a
/// render per keystroke would rebuild the whole visual tree on every character and the reader would
/// be watching the pane flicker rather than the note appear. The timer lives here rather than in the
/// control for the reason above — only this side knows what a keystroke is.
/// </para>
/// </remarks>
public sealed class MarkdownPreviewViewModel : ObservableObject, IDisposable
{
    private readonly DocumentManagerViewModel _documents;
    private readonly IConfig<AppSettings> _settings;
    private readonly IFontCatalog _fonts;
    private readonly IDocumentOpener _opener;
    private readonly IFileLauncher _launcher;
    private readonly IClipboardWriter _clipboard;
    private readonly IIdleScheduler _idle;
    private readonly ILogger<MarkdownPreviewViewModel> _logger;

    private DocumentViewModel? _tracked;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarkdownPreviewViewModel"/> class.
    /// </summary>
    /// <param name="documents">The editor pane, whose active tab is what the preview shows.</param>
    /// <param name="settings">The live settings, which the type the document is set in comes from.</param>
    /// <param name="fonts">
    /// Turns the stored families into ones this machine actually has (see <see cref="Typography"/>).
    /// </param>
    /// <param name="opener">
    /// Opens a file a link in the preview points at — the same service the file tree activates a file
    /// through, so a link to a note and a double-click on it in the tree do the same thing.
    /// </param>
    /// <param name="launcher">Hands an address that is not a file to the platform.</param>
    /// <param name="clipboard">Where a copy out of the pane is written.</param>
    /// <param name="idle">The timer that lets typing settle before the preview is rebuilt.</param>
    /// <param name="logger">Records the failures a render survived.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public MarkdownPreviewViewModel(
        DocumentManagerViewModel documents,
        IConfig<AppSettings> settings,
        IFontCatalog fonts,
        IDocumentOpener opener,
        IFileLauncher launcher,
        IClipboardWriter clipboard,
        IIdleScheduler idle,
        ILogger<MarkdownPreviewViewModel> logger)
    {
        _documents = documents ?? throw new ArgumentNullException(nameof(documents));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _fonts = fonts ?? throw new ArgumentNullException(nameof(fonts));
        _opener = opener ?? throw new ArgumentNullException(nameof(opener));
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        _idle = idle ?? throw new ArgumentNullException(nameof(idle));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _documents.PropertyChanged += OnDocumentsPropertyChanged;
        _documents.DocumentReloaded += OnDocumentReloaded;

        Track(_documents.ActiveDocument);
    }

    /// <summary>
    /// How long the editor stays idle before the preview is rebuilt around what was typed.
    /// </summary>
    /// <remarks>
    /// Short enough that the preview reads as live and long enough that a sentence typed at speed is
    /// one render rather than forty. Not a setting: nothing about a quarter of a second is a matter of
    /// taste, and a value the user could get wrong would only ever be a way to make the pane stutter.
    /// </remarks>
    public static TimeSpan LiveUpdateDelay { get; } = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// The markdown to render, or <see langword="null"/> when no document is open.
    /// </summary>
    public string? Markdown { get; private set => SetProperty(ref field, value); }

    /// <summary>
    /// The full path of the document being previewed, or <see langword="null"/> when none is open.
    /// </summary>
    /// <remarks>
    /// Identity rather than content: the renderer uses it to tell a note being retyped — whose scroll
    /// position is worth keeping across the re-render — from a different note, which starts at the
    /// top. <see cref="BasePath"/> cannot answer that, because two notes in one folder share it.
    /// </remarks>
    public string? SourcePath { get; private set => SetProperty(ref field, value); }

    /// <summary>
    /// The folder the active document lives in, which its relative image links are resolved against.
    /// <see langword="null"/> when no document is open.
    /// </summary>
    public string? BasePath { get; private set => SetProperty(ref field, value); }

    /// <summary>
    /// Whether there is a document to preview at all. The pane shows its empty state when there is
    /// not — an empty renderer would be an unexplained blank half of the window.
    /// </summary>
    public bool HasDocument { get; private set => SetProperty(ref field, value); }

    /// <summary>
    /// Whether there is anything for a copy to put on the clipboard, which is what the header button
    /// and the pane's context menu are enabled by.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Disabled rather than hidden when there is not: a control that vanishes when the last tab
    /// closes is a control the reader has to find again, and this one says plainly that there is
    /// nothing to take. White space only counts as nothing — a note holding two blank lines has text
    /// in it and nothing worth carrying to another application.
    /// </para>
    /// <para>
    /// Announced from the end of a refresh rather than from the two setters it is derived from, so
    /// that opening a document says it once instead of once per property. Nothing renders on it —
    /// unlike every other property here, this one only decides whether a button is enabled — but a
    /// derived property that announces itself twice for one change is still noise on a channel this
    /// pane keeps deliberately quiet.
    /// </para>
    /// </remarks>
    public bool CanCopy => HasDocument && !string.IsNullOrWhiteSpace(Markdown);

    /// <summary>
    /// The type the preview sets the document in, built from the settings and re-read when the
    /// settings window closes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Computed on every read rather than cached, like the editor pane's own font properties: the
    /// settings window edits the very object this reads, so the value is whatever the file says at the
    /// moment it is asked for, and <see cref="OnSettingsChanged"/> only has to say that it changed.
    /// </para>
    /// <para>
    /// Both families are resolved here rather than in the renderer. A control has no business probing
    /// installed fonts, and the two questions are not the same one: a missing prose family falls back
    /// to the font the interface is drawn in, a missing code family to the best fixed-width font this
    /// machine has. The stored value is left exactly as the file holds it either way, so a settings
    /// file carried between two machines keeps naming the font the other one has.
    /// </para>
    /// </remarks>
    public MarkdownPreviewTypography Typography => new(
        Resolve(_settings.Value.PreviewFontFamily, _fonts.SystemDefaultFamily),
        Resolve(_settings.Value.PreviewCodeFontFamily, _fonts.DefaultMonospaceFamily),
        _settings.Value.PreviewFontSize,
        _settings.Value.PreviewCodeFontSize,
        _settings.Value.PreviewLineHeight);

    /// <summary>
    /// Records a failure the renderer survived: a block it could not build, a parse that threw, an
    /// image it could not decode.
    /// </summary>
    /// <param name="failure">What went wrong.</param>
    /// <remarks>
    /// Logged at warning rather than error, and nothing is shown: the reader already has the bordered
    /// error block where the content would have been, and an info bar per malformed image would be
    /// noise on a document that is otherwise readable.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is <see langword="null"/>.</exception>
    public void OnRenderFailed(Exception failure)
    {
        ArgumentNullException.ThrowIfNull(failure);

        _logger.LogWarning(
            failure,
            "A part of the preview of {FilePath} could not be rendered.",
            _tracked?.FilePath ?? "(no document)");
    }

    /// <summary>
    /// Writes a copy the reader asked for in the preview to the clipboard.
    /// </summary>
    /// <param name="request">
    /// Which copy it is: the text they had selected, or — when
    /// <see cref="MarkdownCopyRequest.IsSelection"/> is <see langword="false"/> — the whole note.
    /// </param>
    /// <returns>A task that completes once the clipboard has been written, or the failure recorded.</returns>
    /// <remarks>
    /// <para>
    /// The renderer resolves which of the two it is, because only it knows what is selected; this
    /// side turns the whole-note case into flavours and reaches the clipboard. A selection is text off
    /// the screen already and goes as plain text alone. The whole note goes as both
    /// <see cref="MarkdownPlainText"/> and <see cref="MarkdownHtml"/> in one write, so the receiving
    /// application chooses: Notepad takes the text, Word keeps the headings, bold, links and tables.
    /// Both projections read the same AST the renderer was given.
    /// </para>
    /// <para>
    /// A write that the platform declines, or that throws, is logged at warning and nothing is shown
    /// — the same treatment as a render failure the reader can still read around. The one thing that
    /// must not happen is an unhandled exception, because this is reached from a key handler and from
    /// a button.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    public async Task CopyAsync(MarkdownCopyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            (string text, string? html) = FlavoursOf(request);

            if (text.Length == 0)
            {
                _logger.LogDebug("A copy was asked for in the preview with nothing to copy.");

                return;
            }

            // One write either way, because a second would replace the first rather than sit beside
            // it. Which flavours it carries is the only thing that differs.
            bool written = html is null
                ? await _clipboard.WriteTextAsync(text)
                : await _clipboard.WriteTextAndHtmlAsync(text, html);

            if (!written)
            {
                _logger.LogWarning("This platform has no clipboard to copy the preview to.");
            }
        }
        catch (Exception failure)
        {
            // Caught broadly and on purpose: the clipboard is platform code whose failures are not a
            // documented set — another application holding it open is the common one — and a copy
            // the reader can simply repeat is not worth the window.
            _logger.LogWarning(failure, "The preview could not be copied to the clipboard.");
        }
    }

    /// <summary>
    /// Follows a link the reader clicked in the preview.
    /// </summary>
    /// <param name="url">The link, exactly as the document wrote it.</param>
    /// <returns>A task that completes once the link has been followed, or its refusal recorded.</returns>
    /// <remarks>
    /// Three destinations, decided by <see cref="MarkdownLinkTarget"/>: an address goes to the
    /// platform, a file goes to <see cref="IDocumentOpener"/> — which opens a note in a tab and hands
    /// anything else to the operating system, exactly as the file tree does — and a link into the
    /// document itself still does nothing. The scroll sync indexes source lines, not heading anchors,
    /// so resolving <c>#a-heading</c> to a block is its own piece of work rather than a use of it.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="url"/> is <see langword="null"/>.</exception>
    public async Task ActivateLinkAsync(string url)
    {
        ArgumentNullException.ThrowIfNull(url);

        MarkdownLinkTarget target = MarkdownLinkTarget.Resolve(BasePath, url);

        switch (target.Kind)
        {
            case MarkdownLinkKind.External when target.Address is { } address:
                await LaunchAsync(address);
                break;

            case MarkdownLinkKind.Local when target.LocalPath is { } path:
                // Not awaited for a result: the opener reports what it could not open, in the info bar
                // and in the log, and there is nothing left for this side to say about it.
                await _opener.OpenAsync(path);
                break;

            case MarkdownLinkKind.Fragment:
                _logger.LogDebug("The preview link {Url} points inside the document; nothing to do yet.", url);
                break;

            default:
                _logger.LogWarning("The preview link {Url} could not be resolved to anything openable.", url);
                break;
        }
    }

    /// <summary>
    /// Ticks or unticks a task box the reader clicked in the preview, by rewriting the one character
    /// between its brackets in the document being edited.
    /// </summary>
    /// <param name="sourceOffset">
    /// The offset of that character in the markdown the preview was rendered from.
    /// </param>
    /// <param name="isChecked">The state the reader asked for.</param>
    /// <returns>
    /// <see langword="true"/> when the character was written; <see langword="false"/> when the
    /// position no longer holds a task box, which leaves the document untouched.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Into the live <see cref="AvaloniaEdit.Document.TextDocument"/>, not the file: a replacement of
    /// one character is one entry on the undo stack, so <c>Ctrl+Z</c> in the editor takes the tick
    /// back in a single step, the tab goes dirty on its own, and autosave persists it through the
    /// path every other edit goes through. There is no second save to keep consistent.
    /// </para>
    /// <para>
    /// The offset is checked before it is written to, and that is not a formality. The preview is
    /// rebuilt <see cref="LiveUpdateDelay"/> after the last keystroke, so a box on screen can be
    /// describing text that has since been typed over — and an offset trusted blindly would put an
    /// <c>x</c> in the middle of an unrelated sentence. A position that does not still read as
    /// <c>[ ]</c>, <c>[x]</c> or <c>[X]</c> is one this no longer understands, so it does nothing at
    /// all and lets the imminent re-render show the reader what the document really says.
    /// </para>
    /// </remarks>
    public bool ToggleTask(int sourceOffset, bool isChecked)
    {
        if (_tracked is not { } document)
        {
            _logger.LogDebug("A task box was toggled with no document open; nothing to write to.");

            return false;
        }

        TextDocument text = document.Document;

        if (!HoldsTaskBox(text, sourceOffset))
        {
            _logger.LogDebug(
                "Offset {Offset} in {FilePath} no longer holds a task box; the toggle was dropped.",
                sourceOffset,
                document.FilePath);

            return false;
        }

        text.Replace(sourceOffset, 1, isChecked ? "x" : " ");

        return true;
    }

    /// <summary>
    /// Re-reads the type the document is set in, after the settings window has been closed.
    /// </summary>
    /// <remarks>
    /// The same seam the editor pane re-reads its own font through, and for the same reason: the
    /// settings window edits the live settings object and nothing tells anyone it did. One property
    /// change rather than four, because each one the renderer heard would be a walk of the document.
    /// </remarks>
    public void OnSettingsChanged() => OnPropertyChanged(nameof(Typography));

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _documents.PropertyChanged -= OnDocumentsPropertyChanged;
        _documents.DocumentReloaded -= OnDocumentReloaded;

        // Track(null) cancels the pending render on its way through Refresh; the scheduler itself is
        // the container's, shared with autosave, and is not this pane's to shut down.
        Track(null);
    }

    // The three characters a task box is written as, read back off the buffer rather than off the
    // render: `[`, the state, `]`. Both neighbours are checked, so an offset that has drifted into
    // ordinary prose — where a lone `x` is common enough — is refused rather than overwritten.
    private static bool HoldsTaskBox(TextDocument text, int offset)
        => (offset >= 1)
            && (offset + 1 < text.TextLength)
            && (text.GetCharAt(offset - 1) == '[')
            && (text.GetCharAt(offset + 1) == ']')
            && text.GetCharAt(offset) is ' ' or 'x' or 'X';

    // The selection as it came off the screen, or the whole note projected out of the markdown it was
    // rendered from. Inside the try in CopyAsync, because both projections parse.
    //
    // A selection gets no HTML flavour: producing one means mapping an arbitrary character range back
    // to the markdown that produced it, and the pane renders in one direction only. The whole note
    // gets both, so a paste into Word keeps its headings and a paste into Notepad still reads.
    private (string Text, string? Html) FlavoursOf(MarkdownCopyRequest request)
    {
        if (request.SelectedText is { } selected)
        {
            return (selected, null);
        }

        return Markdown is { Length: > 0 } markdown
            ? (MarkdownPlainText.Render(markdown), MarkdownHtml.Render(markdown, BasePath))
            : (string.Empty, null);
    }

    // Empty means "automatic", and a family this machine does not have means the same thing in
    // practice — but only for what is rendered. What the file holds is not touched either way.
    private string Resolve(string? storedFamily, string fallbackFamily)
        => _fonts.IsAvailable(storedFamily) ? storedFamily! : fallbackFamily;

    private async Task LaunchAsync(Uri address)
    {
        try
        {
            if (!await _launcher.LaunchUriAsync(address))
            {
                _logger.LogWarning("Nothing on this machine is registered to open {Address}.", address);
            }
        }
        catch (Exception failure)
        {
            // Caught broadly and on purpose: the launcher goes through platform code whose failures
            // are not a documented set, and this is called from a pointer handler — an exception out
            // of here is an unhandled one on the UI thread, over a link the reader merely clicked.
            _logger.LogError(failure, "{Address} could not be handed to the operating system.", address);
        }
    }

    private void OnDocumentsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DocumentManagerViewModel.ActiveDocument))
        {
            Track(_documents.ActiveDocument);
        }
    }

    private void OnDocumentReloaded(object? sender, DocumentViewModel document)
    {
        // A tab that is not on screen will be read again when it is activated, so re-reading it now
        // would only be work nobody can see.
        if (ReferenceEquals(document, _tracked))
        {
            Refresh();
        }
    }

    private void OnTrackedPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Save As moves the file, and with it the folder every relative image link means.
        if (e.PropertyName == nameof(DocumentViewModel.FilePath))
        {
            Refresh();
        }
    }

    private void OnTrackedTextChanged(object? sender, EventArgs e)
    {
        // Keyed on this pane rather than on the document: the key is what the timer is restarted
        // under, and the pane previews one document at a time, so a keystroke in the active tab must
        // push back the pending render whichever tab the previous one was scheduled for.
        _idle.Schedule(this, LiveUpdateDelay, () =>
        {
            PublishText();

            return Task.CompletedTask;
        });
    }

    private void PublishText()
    {
        if (_disposed || (_tracked is null))
        {
            return;
        }

        // Only the text. The folder and the path have not moved — a save that moves the file refreshes
        // through FilePath instead — so re-assigning them would cost a second render for nothing.
        Markdown = _tracked.Document.Text;

        OnPropertyChanged(nameof(CanCopy));
    }

    private void Track(DocumentViewModel? document)
    {
        if (_tracked is not null)
        {
            _tracked.PropertyChanged -= OnTrackedPropertyChanged;
            _tracked.TextChanged -= OnTrackedTextChanged;
        }

        _tracked = document;

        if (_tracked is not null)
        {
            _tracked.PropertyChanged += OnTrackedPropertyChanged;
            _tracked.TextChanged += OnTrackedTextChanged;
        }

        Refresh();
    }

    private void Refresh()
    {
        // Whatever was pending described the state this refresh is about to replace, and firing it
        // afterwards would publish the outgoing document's text over the incoming one's.
        _idle.Cancel(this);

        if (_tracked is null)
        {
            Markdown = null;
            BasePath = null;
            SourcePath = null;
            HasDocument = false;

            OnPropertyChanged(nameof(CanCopy));

            return;
        }

        // Path first, then folder, then text. Each of the last two is a render, so switching tabs
        // costs two — and this order is the one where the second, and therefore final, render is the
        // correct pairing. Coalescing them would mean making the control's render asynchronous; a note
        // is small enough that the wasted pass is cheaper than that, and the debounce above this
        // means typing never pays it at all. The path is not a render at all, and comes first so that
        // both of them know which document they are drawing.
        SourcePath = _tracked.FilePath;
        BasePath = Path.GetDirectoryName(_tracked.FilePath);
        Markdown = _tracked.Document.Text;
        HasDocument = true;

        // Last, and once: it is derived from the two above, and the surfaces bound to it only need
        // to hear the answer the whole refresh settled on.
        OnPropertyChanged(nameof(CanCopy));
    }
}
