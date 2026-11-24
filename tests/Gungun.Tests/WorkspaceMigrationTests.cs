using System;
using System.Collections.Generic;
using System.Linq;
using Gungun.Services;
using Xunit;

namespace Gungun.Tests;

public sealed class WorkspaceMigrationTests
{
    private static string? Adopt(params string[] folders)
    {
        var set = new HashSet<string>(folders, StringComparer.OrdinalIgnoreCase);
        return WorkspaceMigration.FolderToAdopt(set.Contains);
    }

    [Fact]
    public void TheFolderFromBeforeTheRenameIsAdopted()
    {
        Assert.Equal("Nectar", Adopt("Nectar"));
    }

    [Fact]
    public void TheOlderNameIsStillFoundWhenTheMiddleOneIsGone()
    {
        Assert.Equal("TopSpace", Adopt("TopSpace"));
    }

    [Fact]
    public void TheNewestLegacyNameWinsWhenBothExist()
    {
        Assert.Equal("Nectar", Adopt("TopSpace", "Nectar"));
    }

    [Fact]
    public void NothingIsAdoptedWhenTheCurrentFolderIsAlreadyThere()
    {
        Assert.Null(Adopt("Gungun", "Nectar", "TopSpace"));
    }

    [Fact]
    public void NothingIsAdoptedOnAFreshMachine()
    {
        Assert.Null(Adopt());
    }

    [Theory]
    [InlineData("nectar.db", "gungun.db")]
    [InlineData("nectar.db-wal", "gungun.db-wal")]
    [InlineData("nectar.db-shm", "gungun.db-shm")]
    [InlineData("nectar.log", "gungun.log")]
    [InlineData("canopy.db", "gungun.db")]
    [InlineData("canopy.log", "gungun.log")]
    public void TheDatabaseAndTheLogTravelWithTheName(string old, string renamed)
    {
        Assert.Equal(new[] { (old, renamed) }, WorkspaceMigration.Renames(new[] { old }));
    }

    [Fact]
    public void TheRestOfTheFolderKeepsItsName()
    {
        var untouched = new[] { "settings.json", "db.key", "licence.json", "profile.png", "clips" };
        Assert.Empty(WorkspaceMigration.Renames(untouched));
    }

    [Fact]
    public void AFileThatMerelyStartsWithTheOldNameIsNotRenamed()
    {
        Assert.Empty(WorkspaceMigration.Renames(new[] { "nectar-notes.txt", "nectar.db.bak" }));
    }

    [Fact]
    public void EveryRenameIsListedOnce()
    {
        var moves = WorkspaceMigration.Renames(new[] { "nectar.db", "nectar.db-wal", "nectar.db-shm" });
        Assert.Equal(3, moves.Count);
        Assert.Equal(3, moves.Select(move => move.To).Distinct().Count());
    }

    private const string Local = @"C:\Users\HP\AppData\Local";

    private static string Retarget(string json) =>
        WorkspaceMigration.RetargetPaths(json, Local + @"\Nectar", Local + @"\Gungun");

    [Fact]
    public void ASavedPhotographFollowsTheFolderRename()
    {
        var json = "{ \"PhotoPath\": \"C:\\\\Users\\\\HP\\\\AppData\\\\Local\\\\Nectar\\\\profile.png\" }";

        var result = Retarget(json);

        Assert.Contains(@"Local\\Gungun\\profile.png", result, StringComparison.Ordinal);
        Assert.DoesNotContain("Nectar", result, StringComparison.Ordinal);
    }

    [Fact]
    public void ACustomFontFollowsItToo()
    {
        var json = "{ \"CustomFontPath\": \"C:\\\\Users\\\\HP\\\\AppData\\\\Local\\\\Nectar\\\\fonts\\\\Inter.ttf\" }";

        Assert.Contains(@"Local\\Gungun\\fonts\\Inter.ttf", Retarget(json), StringComparison.Ordinal);
    }

    [Fact]
    public void APathOutsideTheOldFolderIsLeftAlone()
    {
        var json = "{ \"PhotoPath\": \"C:\\\\Users\\\\HP\\\\Pictures\\\\profile.png\" }";

        Assert.Equal(json, Retarget(json));
    }

    [Fact]
    public void AWordThatMerelyContainsTheNameIsLeftAlone()
    {
        var json = "{ \"DisplayName\": \"Nectar fan\", \"PhotoPath\": \"C:\\\\Users\\\\HP\\\\AppData\\\\Local\\\\Nectar-old\\\\profile.png\" }";

        Assert.Equal(json, Retarget(json));
    }

    [Fact]
    public void RepointingTwiceChangesNothingTheSecondTime()
    {
        var once = Retarget("{ \"PhotoPath\": \"C:\\\\Users\\\\HP\\\\AppData\\\\Local\\\\Nectar\\\\profile.png\" }");

        Assert.Equal(once, Retarget(once));
    }

    [Theory]
    [InlineData("")]
    [InlineData("{ }")]
    public void SettingsThatNameNoFileComeBackUnchanged(string json)
    {
        Assert.Equal(json, Retarget(json));
    }
}
