namespace Nomad.Core;

public static class WindowsPath
{
    public static string Normalize(string path)
    {
        if (!Path.IsPathFullyQualified(path) || path.StartsWith("\\\\?\\", StringComparison.Ordinal) ||
            path.StartsWith("\\\\.\\", StringComparison.Ordinal))
        {
            throw new ArgumentException("Choose a full Windows file, folder, or volume path.", nameof(path));
        }

        string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        if (full.Length < 3 || !char.IsAsciiLetter(full[0]) || full[1] != ':' || full[2] != '\\' ||
            full.AsSpan(2).Contains(':'))
        {
            throw new ArgumentException("Choose a path on a Windows drive with a drive letter.", nameof(path));
        }

        return char.ToUpperInvariant(full[0]) + full[1..];
    }

    public static bool IsWithin(string path, string root) =>
        string.Equals(path, root, StringComparison.Ordinal) ||
        path.StartsWith(Path.EndsInDirectorySeparator(root) ? root : root + '\\', StringComparison.Ordinal);

    public static string? ArchivePath(string path)
    {
        string drive = path[0].ToString();
        if (path.Length == 3)
            return $"Migrated Windows Files/{drive}";

        string[] components = path[3..].Split('\\');
        if (components.Any(component => component is "" or "." or ".." ||
            System.Text.Encoding.UTF8.GetByteCount(component) > 255))
            return null;

        return $"Migrated Windows Files/{drive}/{string.Join('/', components)}";
    }
}
