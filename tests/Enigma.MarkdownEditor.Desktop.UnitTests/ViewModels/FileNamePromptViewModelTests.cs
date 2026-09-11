using System;
using System.IO;
using Enigma.MarkdownEditor.Desktop.ViewModels;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.ViewModels;

/// <summary>
/// Tests for the box the tree asks a name into, over a real folder.
/// </summary>
/// <remarks>
/// Real, because the clash half of the check is the half worth having: what a name may consist of is
/// pinned in <c>FileNameValidationTests</c>, and what is left here is that this ViewModel asks the
/// right question about the right folder, on every keystroke.
/// </remarks>
public sealed class FileNamePromptViewModelTests : IDisposable
{
    private readonly string _directory;

    public FileNamePromptViewModelTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "Enigma.MarkdownEditor.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public void AnEmptyBox_CannotBeConfirmed()
    {
        FileNamePromptViewModel prompt = new(_directory, isDirectory: false, string.Empty);

        Assert.False(prompt.IsValid);
        Assert.True(prompt.HasError);
        Assert.NotNull(prompt.Error);
    }

    [Fact]
    public void AUsableName_HasNothingToSayAboutItself()
    {
        FileNamePromptViewModel prompt = new(_directory, isDirectory: false, ".md")
        {
            Name = "idea.md",
        };

        Assert.True(prompt.IsValid);
        Assert.False(prompt.HasError);
        Assert.Null(prompt.Error);
    }

    [Fact]
    public void TheCheckFollowsEveryKeystroke()
    {
        FileNamePromptViewModel prompt = new(_directory, isDirectory: false, "idea.md");
        Assert.True(prompt.IsValid);

        prompt.Name = "idea/md";
        Assert.False(prompt.IsValid);

        prompt.Name = "idea.md";
        Assert.True(prompt.IsValid);
    }

    [Fact]
    public void ANameTheFolderIsAlreadyUsing_IsRefused()
    {
        File.WriteAllText(Path.Combine(_directory, "taken.md"), "# note");

        FileNamePromptViewModel prompt = new(_directory, isDirectory: false, "taken.md");

        Assert.False(prompt.IsValid);
        Assert.Contains("taken.md", prompt.Error);
    }

    [Fact]
    public void TheNameAnEntryAlreadyHas_IsNotAClashWithItself()
    {
        File.WriteAllText(Path.Combine(_directory, "notes.md"), "# note");

        // A rename dialog opens on the current name, and it has to open valid — otherwise every
        // rename starts with the confirm button disabled and an error under the box.
        FileNamePromptViewModel prompt = new(_directory, isDirectory: false, "notes.md", currentName: "notes.md");

        Assert.True(prompt.IsValid);
    }

    [Fact]
    public void ARenameOntoADifferentEntry_IsStillARefusal()
    {
        File.WriteAllText(Path.Combine(_directory, "notes.md"), "# note");
        File.WriteAllText(Path.Combine(_directory, "other.md"), "# note");

        FileNamePromptViewModel prompt = new(_directory, isDirectory: false, "notes.md", currentName: "notes.md")
        {
            Name = "other.md",
        };

        Assert.False(prompt.IsValid);
    }

    [Fact]
    public void TheNameHandedBack_IsTrimmed()
    {
        FileNamePromptViewModel prompt = new(_directory, isDirectory: true, "  drafts  ");

        Assert.Equal("drafts", prompt.TrimmedName);
    }

    [Fact]
    public void ANullName_IsReadAsAnEmptyOne()
    {
        FileNamePromptViewModel prompt = new(_directory, isDirectory: false, "idea.md")
        {
            Name = null!,
        };

        Assert.Equal(string.Empty, prompt.Name);
        Assert.False(prompt.IsValid);
    }

    [Fact]
    public void Committing_ReportsOnlyWhenTheNameCanBeUsed()
    {
        FileNamePromptViewModel prompt = new(_directory, isDirectory: false, string.Empty);

        int committed = 0;
        prompt.Committed += (_, _) => committed++;

        // Enter on an unusable name must not do what the disabled button refuses to.
        prompt.Commit();
        Assert.Equal(0, committed);

        prompt.Name = "idea.md";
        prompt.Commit();
        Assert.Equal(1, committed);
    }

    [Fact]
    public void EveryArgument_IsChecked()
    {
        Assert.Throws<ArgumentNullException>(() => new FileNamePromptViewModel(null!, false, "a.md"));
        Assert.Throws<ArgumentException>(() => new FileNamePromptViewModel("  ", false, "a.md"));
        Assert.Throws<ArgumentNullException>(() => new FileNamePromptViewModel(_directory, false, null!));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A leftover temp folder is not worth failing a green test over.
        }
    }
}
