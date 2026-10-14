using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Enigma.Avalonia.Desktop.Controls.ContentDialog;
using Enigma.MarkdownEditor.Desktop.Services;
using Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Services;

/// <summary>
/// Tests for the delete confirmations: what each one says, and which answer counts as a yes.
/// </summary>
/// <remarks>
/// The tree's tests decide when a question is asked; these decide what the user reads before
/// answering it. The dialogs are configured on real <see cref="ContentDialog"/> instances by a host
/// that shows nothing, so they run under <c>[AvaloniaFact]</c> without a window.
/// </remarks>
public sealed class FileOperationPromptsTests
{
    private static readonly string Trash = OperatingSystem.IsWindows() ? "recycle bin" : "trash";

    [AvaloniaFact]
    public async Task MovingAFileToTheTrash_AsksAboutItByName()
    {
        FakeContentDialogService dialogs = new();

        bool confirmed = await new FileOperationPrompts(dialogs).ConfirmMoveToTrashAsync("a.md", isDirectory: false);

        ContentDialog dialog = Assert.Single(dialogs.Shown);
        Assert.True(confirmed);
        Assert.Equal("Delete 'a.md'?", dialog.Title);
        Assert.Equal($"The file will be moved to the {Trash}, where it can be restored from.", MessageOf(dialog));
        Assert.Equal("Delete", dialog.PrimaryButtonText);
        Assert.Equal("Cancel", dialog.CloseButtonText);
        Assert.True(string.IsNullOrEmpty(dialog.SecondaryButtonText));
    }

    [AvaloniaFact]
    public async Task MovingAFolderToTheTrash_SaysWhatIsInsideItGoesToo()
    {
        FakeContentDialogService dialogs = new();

        await new FileOperationPrompts(dialogs).ConfirmMoveToTrashAsync("drafts", isDirectory: true);

        ContentDialog dialog = Assert.Single(dialogs.Shown);
        Assert.Equal("Delete 'drafts'?", dialog.Title);
        Assert.Contains("The folder and everything in it will be moved", MessageOf(dialog), StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task MovingSeveralToTheTrash_CountsThemInTheTitleAndNamesEachOne()
    {
        FakeContentDialogService dialogs = new();

        await new FileOperationPrompts(dialogs).ConfirmMoveToTrashAsync(["c.md", "a.md", "drafts"]);

        ContentDialog dialog = Assert.Single(dialogs.Shown);
        Assert.Equal("Delete 3 items?", dialog.Title);
        Assert.Equal(
            $"These will be moved to the {Trash}, where they can be restored from — a folder with " +
            "everything in it.\n\n• c.md\n• a.md\n• drafts",
            MessageOf(dialog));
        Assert.Equal("Delete", dialog.PrimaryButtonText);
        Assert.Equal("Cancel", dialog.CloseButtonText);
    }

    [AvaloniaFact]
    public async Task MovingALongSelectionToTheTrash_NamesTheFirstFiveAndCountsTheRest()
    {
        FakeContentDialogService dialogs = new();

        await new FileOperationPrompts(dialogs).ConfirmMoveToTrashAsync(
            ["1.md", "2.md", "3.md", "4.md", "5.md", "6.md", "7.md"]);

        string? message = MessageOf(Assert.Single(dialogs.Shown));
        Assert.EndsWith("• 1.md\n• 2.md\n• 3.md\n• 4.md\n• 5.md\n…and 2 more.", message, StringComparison.Ordinal);
        Assert.DoesNotContain("6.md", message, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task MovingAListOfOneToTheTrash_TakesTheSingleEntryTitle()
    {
        FakeContentDialogService dialogs = new();

        await new FileOperationPrompts(dialogs).ConfirmMoveToTrashAsync(["a.md"]);

        Assert.Equal("Delete 'a.md'?", Assert.Single(dialogs.Shown).Title);
    }

    [AvaloniaFact]
    public async Task DeletingPermanently_AsksADifferentQuestionFromTheTrashOne()
    {
        FakeContentDialogService dialogs = new();
        FileOperationPrompts prompts = new(dialogs);

        await prompts.ConfirmPermanentDeleteAsync("a.md", isDirectory: false);
        await prompts.ConfirmPermanentDeleteAsync(["a.md", "b.md"]);

        // Asked after the trash question when the trash declines an entry, so it must not say there is
        // no trash: it says this entry cannot go there, and that what follows cannot be undone.
        ContentDialog single = dialogs.Shown[0];
        Assert.Equal("Delete 'a.md' permanently?", single.Title);
        Assert.Equal(
            $"It cannot be moved to the {Trash}, so the file would be deleted outright. This cannot be undone.",
            MessageOf(single));
        Assert.Equal("Delete permanently", single.PrimaryButtonText);

        ContentDialog several = dialogs.Shown[1];
        Assert.Equal("Delete 2 items permanently?", several.Title);
        Assert.EndsWith("This cannot be undone.\n\n• a.md\n• b.md", MessageOf(several), StringComparison.Ordinal);
        Assert.Equal("Delete permanently", several.PrimaryButtonText);
    }

    [AvaloniaTheory]
    [InlineData(DialogResult.Primary, true)]
    [InlineData(DialogResult.Close, false)]
    [InlineData(DialogResult.Secondary, false)]
    [InlineData(DialogResult.None, false)]
    public async Task OnlyThePrimaryButton_Confirms(DialogResult result, bool confirms)
    {
        // None is what Escape and a click on the scrim resolve as: no answer is a no.
        FileOperationPrompts prompts = new(new FakeContentDialogService { Result = result });

        Assert.Equal(confirms, await prompts.ConfirmMoveToTrashAsync("a.md", isDirectory: false));
        Assert.Equal(confirms, await prompts.ConfirmMoveToTrashAsync(["a.md", "b.md"]));
        Assert.Equal(confirms, await prompts.ConfirmPermanentDeleteAsync("a.md", isDirectory: false));
        Assert.Equal(confirms, await prompts.ConfirmPermanentDeleteAsync(["a.md", "b.md"]));
    }

    [Fact]
    public async Task EveryConfirmation_NeedsSomethingToName()
    {
        FakeContentDialogService dialogs = new();
        FileOperationPrompts prompts = new(dialogs);

        await Assert.ThrowsAnyAsync<ArgumentException>(() => prompts.ConfirmMoveToTrashAsync(null!, isDirectory: false));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => prompts.ConfirmMoveToTrashAsync("  ", isDirectory: false));
        await Assert.ThrowsAsync<ArgumentNullException>(() => prompts.ConfirmMoveToTrashAsync((IReadOnlyList<string>)null!));
        await Assert.ThrowsAsync<ArgumentException>(() => prompts.ConfirmMoveToTrashAsync([]));
        await Assert.ThrowsAsync<ArgumentException>(() => prompts.ConfirmMoveToTrashAsync(["a.md", " "]));
        await Assert.ThrowsAsync<ArgumentException>(() => prompts.ConfirmPermanentDeleteAsync([]));

        // Refused before a dialog was ever configured.
        Assert.Empty(dialogs.Shown);
    }

    [Fact]
    public void TheDialogHost_IsRequired()
        => Assert.Throws<ArgumentNullException>(() => new FileOperationPrompts(null!));

    private static string? MessageOf(ContentDialog dialog) => Assert.IsType<TextBlock>(dialog.Content).Text;
}
