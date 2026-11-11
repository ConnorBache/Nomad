using System.Security;

namespace Nomad.Core;

public static class InventoryScanner
{
    private const FileAttributes RecallOnOpen = (FileAttributes)0x00040000;
    private const FileAttributes RecallOnDataAccess = (FileAttributes)0x00400000;

    public static Inventory Scan(IEnumerable<string> roots, IEnumerable<string> exclusions,
        IReadOnlyDictionary<string, string> volumeIds, IEnumerable<string> discoveryIssues,
        CancellationToken cancellationToken)
    {
        string[] selected = roots.Select(WindowsPath.Normalize).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        string[] excluded = exclusions.Select(WindowsPath.Normalize).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var entries = new Dictionary<string, InventoryEntry>(StringComparer.Ordinal);
        var pending = new Stack<string>();

        foreach (string root in selected.Reverse())
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (excluded.Any(exclusion => WindowsPath.IsWithin(root, exclusion)))
                {
                    entries[root] = Entry(root, "unknown", "excluded", note: "Excluded by selection.");
                    continue;
                }

                var ancestors = new Stack<string>();
                for (string? ancestor = Path.GetDirectoryName(root); ancestor is not null; ancestor = Path.GetDirectoryName(ancestor))
                    ancestors.Push(ancestor);

                foreach (string ancestor in ancestors)
                {
                    if (!IsLinkOrOffline(File.GetAttributes(ancestor)))
                        continue;
                    entries[root] = Entry(root, "unknown", "blocked", note: "A parent directory is a link or an offline location.");
                    break;
                }

                if (!entries.ContainsKey(root))
                    pending.Push(root);
            }
            catch (Exception error) when (IsScanError(error))
            {
                entries[root] = Entry(root, "unknown", "blocked", note: DescribeError(error));
            }
        }

        while (pending.TryPop(out string? path))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entries.ContainsKey(path))
                continue;
            if (excluded.Any(exclusion => WindowsPath.IsWithin(path, exclusion)))
            {
                entries.Add(path, Entry(path, "unknown", "excluded", note: "Excluded by selection."));
                continue;
            }

            string kind = "unknown";
            try
            {
                FileAttributes attributes = File.GetAttributes(path);
                bool directory = attributes.HasFlag(FileAttributes.Directory);
                kind = directory ? "directory" : "file";
                if (IsLinkOrOffline(attributes))
                {
                    entries.Add(path, Entry(path, kind, "blocked", note: "Link, mount point, or offline data requires review."));
                    continue;
                }

                if (attributes.HasFlag(FileAttributes.Encrypted))
                {
                    entries.Add(path, Entry(path, kind, "blocked", note: "Windows EFS encryption requires a verified export."));
                    continue;
                }

                if (directory)
                {
                    var options = new EnumerationOptions { AttributesToSkip = 0, IgnoreInaccessible = false };
                    foreach (string child in Directory.EnumerateFileSystemEntries(path, "*", options))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        pending.Push(child);
                    }
                }

                long? size = directory ? null : new FileInfo(path).Length;
                string? note = WindowsPath.ArchivePath(path) is null
                    ? "A path component exceeds the Linux archive name limit."
                    : !volumeIds.ContainsKey(path[..2]) ? "A stable source volume ID was unavailable." : null;
                entries.Add(path, Entry(path, kind, note is null ? "discovered" : "blocked", size, File.GetLastWriteTimeUtc(path), note));
            }
            catch (Exception error) when (IsScanError(error))
            {
                entries.Add(path, Entry(path, kind, "blocked", note: DescribeError(error)));
            }
        }

        return new Inventory(selected, excluded, entries.Values.OrderBy(entry => entry.WindowsPath, StringComparer.Ordinal).ToArray(),
            discoveryIssues.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());

        InventoryEntry Entry(string path, string kind, string status, long? size = null,
            DateTime? modified = null, string? note = null) =>
            new(path, volumeIds.GetValueOrDefault(path[..2]), WindowsPath.ArchivePath(path), kind, status, size, modified, note);
    }

    private static bool IsLinkOrOffline(FileAttributes attributes) =>
        (attributes & (FileAttributes.ReparsePoint | FileAttributes.Offline | RecallOnOpen | RecallOnDataAccess)) != 0;

    private static bool IsScanError(Exception error) => error is IOException or UnauthorizedAccessException or SecurityException;

    private static string DescribeError(Exception error) => error switch
    {
        UnauthorizedAccessException or SecurityException => "Windows denied access.",
        FileNotFoundException or DirectoryNotFoundException => "The selected item could not be found.",
        PathTooLongException => "The path exceeds the current Windows path limit.",
        _ => "Windows could not read the item's metadata."
    };
}
