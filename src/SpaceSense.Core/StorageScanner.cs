using System.Security;

namespace SpaceSense.Core;

/// <summary>Metadata-only scan. Never follows reparse points or requests elevation.</summary>
public sealed class StorageScanner(int maxItems = 250_000)
{
    public ScanResult Scan(string path, IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxItems, 1);
        path = Path.GetFullPath(path);
        var attributes = File.GetAttributes(path); // Fail clearly for an invalid or inaccessible root.
        if (!attributes.HasFlag(FileAttributes.Directory))
            throw new ArgumentException("Choose a folder or drive to scan.", nameof(path));
        if (attributes.HasFlag(FileAttributes.ReparsePoint))
            throw new ArgumentException("Choose the original folder rather than a link.", nameof(path));

        var root = new StorageNode(path, true);
        var nodes = new List<StorageNode> { root };
        var pending = new Stack<StorageNode>();
        var warnings = new List<string>();
        long unread = 0;
        bool limitReached = false;
        pending.Push(root);

        void RecordUnread(StorageNode node, string message)
        {
            node.IsIncomplete = true;
            unread++;
            if (warnings.Count < 100) warnings.Add(message);
        }

        while (pending.TryPop(out var folder))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                // Enumeration failures are reported rather than silently ignored.
                using var entries = new DirectoryInfo(folder.FullPath).EnumerateFileSystemInfos().GetEnumerator();
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!entries.MoveNext()) break;
                    if (nodes.Count >= maxItems)
                    {
                        limitReached = true;
                        RecordUnread(folder, "Scan size limit reached; choose a smaller folder for a complete scan.");
                        break;
                    }
                    var entry = entries.Current;
                    StorageNode? child = null;
                    try
                    {
                        var entryAttributes = entry.Attributes;
                        child = new StorageNode(entry.FullName, entryAttributes.HasFlag(FileAttributes.Directory));
                        folder.Children.Add(child);
                        nodes.Add(child);
                        if (entryAttributes.HasFlag(FileAttributes.ReparsePoint))
                        {
                            child.IsLink = true;
                            RecordUnread(child, $"Skipped link or cloud reparse point: {entry.FullName}");
                        }
                        else if (child.IsDirectory) pending.Push(child);
                        else child.Bytes = new FileInfo(entry.FullName).Length;
                    }
                    catch (Exception ex) when (IsReadFailure(ex))
                    {
                        RecordUnread(child ?? folder, $"Could not read {entry.FullName}: {ex.Message}");
                    }
                    if (nodes.Count % 128 == 0)
                        progress?.Report(new ScanProgress(nodes.Count, folder.FullPath));
                }
            }
            catch (Exception ex) when (IsReadFailure(ex))
            {
                RecordUnread(folder, $"Could not finish {folder.FullPath}: {ex.Message}");
            }
            if (limitReached)
            {
                foreach (var unscanned in pending) unscanned.IsIncomplete = true;
                break;
            }
        }

        // Children always occur after parents: aggregate without recursive calls.
        for (int i = nodes.Count - 1; i >= 0; i--)
        {
            var node = nodes[i];
            if (!node.IsDirectory) continue;
            foreach (var child in node.Children)
            {
                node.Bytes += child.Bytes;
                node.IsIncomplete |= child.IsIncomplete;
            }
            node.Children.Sort((a, b) => b.Bytes.CompareTo(a.Bytes));
        }
        cancellationToken.ThrowIfCancellationRequested();
        return new ScanResult(root, nodes.Count, unread, limitReached, warnings);
    }

    private static bool IsReadFailure(Exception ex) => ex is IOException or UnauthorizedAccessException or SecurityException;
}
