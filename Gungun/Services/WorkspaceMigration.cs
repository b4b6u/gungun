namespace Gungun.Services;

/// <summary>
/// The folder names this app wore before it was called Gungun, and the files inside those folders
/// that still carry an older name.
/// </summary>
/// <remarks>
/// The move happens once, before the repository or the logger opens anything. It is a plain
/// folder rename: the workspace is untouched inside, and only the database, its write-ahead
/// companions and the log file change name with the app. Kept out of the window so the rules can
/// be read and tested without a disk.
/// </remarks>
internal static class WorkspaceMigration
{
    /// <summary>The folder the app reads and writes now.</summary>
    internal const string CurrentFolder = "Gungun";

    /// <summary>The names the same folder has had, newest first.</summary>
    internal static readonly string[] LegacyFolders = { "Nectar", "TopSpace" };

    /// <summary>File endings that belonged to the app's name, narrow on purpose.</summary>
    internal static readonly string[] RenamableEndings = { ".db-wal", ".db-shm", ".db", ".log" };

    /// <summary>Stems that were the app's name in a file name.</summary>
    private static readonly string[] LegacyStems = { "canopy", "nectar" };

    /// <summary>The stem every renamed file ends up with.</summary>
    private const string FileStem = "gungun";

    /// <summary>
    /// The folder to adopt, or null when there is nothing older to adopt, or when the current
    /// folder already exists - in which case it is in use and must not be written over.
    /// </summary>
    /// <param name="folderExists">Answers whether a folder by that name exists.</param>
    internal static string? FolderToAdopt(Func<string, bool> folderExists)
    {
        if (folderExists(CurrentFolder))
        {
            return null;
        }

        foreach (var name in LegacyFolders)
        {
            if (folderExists(name))
            {
                return name;
            }
        }

        return null;
    }

    /// <summary>
    /// Inside an adopted folder, the files whose names still carry an older brand - the database,
    /// its two SQLite companions and the log. Everything else keeps its name: settings, the key
    /// file and the clip folder were never named after the app.
    /// </summary>
    internal static IReadOnlyList<(string From, string To)> Renames(IEnumerable<string> fileNames)
    {
        var moves = new List<(string From, string To)>();

        foreach (var name in fileNames)
        {
            foreach (var ending in RenamableEndings)
            {
                if (!name.EndsWith(ending, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var stem = name[..^ending.Length];
                if (Array.Exists(LegacyStems, old => stem.Equals(old, StringComparison.OrdinalIgnoreCase)))
                {
                    moves.Add((name, FileStem + name[stem.Length..]));
                }

                break;
            }
        }

        return moves;
    }
}
