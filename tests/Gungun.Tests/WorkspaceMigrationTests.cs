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
}
