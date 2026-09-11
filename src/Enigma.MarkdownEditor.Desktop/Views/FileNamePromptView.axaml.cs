using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Enigma.MarkdownEditor.Desktop.ViewModels;

namespace Enigma.MarkdownEditor.Desktop.Views;

/// <summary>
/// The content of the name dialog: one box, and what is wrong with what is in it.
/// </summary>
/// <remarks>
/// The two handlers here are the ones a binding cannot express. Enter has to reach the ViewModel
/// because the dialog's buttons belong to the <c>ContentDialog</c> and no key can press one; and the
/// opening selection has to be applied once the box is on screen, because a control that is not in the
/// visual tree cannot take focus.
/// </remarks>
public partial class FileNamePromptView : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FileNamePromptView"/> class.
    /// </summary>
    public FileNamePromptView() => InitializeComponent();

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Posted, not called here, and this is the difference between a dialog you can type into and
        // one you have to click into first: the ContentDialog takes focus for itself as it opens, and
        // it does so after its content has been attached — so a Focus() from inside this pass is
        // simply undone. Background priority is the first point that runs after the dialog has
        // settled.
        Dispatcher.UIThread.Post(FocusName, DispatcherPriority.Background);
    }

    private void FocusName()
    {
        NameBox.Focus();

        // Select the stem and leave the extension alone: renaming 'chapter-1.md' is almost always
        // about the 'chapter-1' half, and a user who did mean the extension only has to press End.
        string name = NameBox.Text ?? string.Empty;
        int stemLength = name.Length - Path.GetExtension(name).Length;

        // With no stem there is nothing to replace, only somewhere to start typing — which is exactly
        // the New file case, where the box opens on a bare '.md'. Selecting the whole thing there
        // would mean the first keystroke threw the extension away, undoing the point of pre-filling
        // it. The same goes for renaming a dotfile.
        NameBox.SelectionStart = 0;
        NameBox.SelectionEnd = stemLength > 0 ? stemLength : 0;
    }

    private void OnNameBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if ((e.Key != Key.Enter) || (DataContext is not FileNamePromptViewModel viewModel))
        {
            return;
        }

        // Handled either way: an Enter that the ViewModel refuses because the name is unusable must
        // not fall through to the dialog underneath.
        e.Handled = true;
        viewModel.Commit();
    }
}
