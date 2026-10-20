namespace SpaceSense.Core;

public sealed class StorageNode(string fullPath, bool isDirectory)
{
    public string FullPath { get; } = fullPath;
    public string Name => Path.GetFileName(FullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) is { Length: > 0 } name ? name : FullPath;
    public bool IsDirectory { get; } = isDirectory;
    public long Bytes { get; internal set; }
    public bool IsIncomplete { get; internal set; }
    public bool IsLink { get; internal set; }
    public List<StorageNode> Children { get; } = [];
    public string SizeLabel => FormatBytes(Bytes) + (IsIncomplete ? " + unread items" : "");
    public string Kind => IsLink ? "Link (not followed)" : IsDirectory ? "Folder" : Path.GetExtension(FullPath).ToLowerInvariant() is { Length: > 0 } ext ? ext + " file" : "File";

    public static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KiB", "MiB", "GiB", "TiB"];
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        return $"{value:0.##} {units[unit]}";
    }
}

public sealed record ScanProgress(long Items, string CurrentPath);
public sealed record ScanResult(StorageNode Root, long Items, long UnreadItems, bool LimitReached,
    IReadOnlyList<string> Warnings);
