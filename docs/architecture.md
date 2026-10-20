# How the prototype works

`SpaceSense.Desktop` is the Windows WPF interface. `SpaceSense.Core` contains plain C# code that can be checked on Linux as well as Windows. There are no third-party runtime packages or external services.

## Scan

1. The user chooses a folder or drive through the Windows folder picker.
2. A background task enumerates file metadata. It reads file names, attributes, locations, and logical sizes, never file contents.
3. An explicit stack avoids recursive traversal. Cancellation is checked between entries. A 250,000-item cap bounds retained metadata; scan a smaller folder if reached.
4. Reparse points (including links, junctions, and some cloud entries) are skipped. Read failures propagate an incomplete flag to parent folders. No privilege escalation is attempted.
5. Sizes aggregate from children to parents. The UI sorts by size, creates folder tree branches on demand, and virtualizes table rows.

This is an ordinary directory scan, not a privileged NTFS/MFT scanner. It may be slower than TreeSize's optimized scan modes. Files may change during a scan: this is not a filesystem snapshot.

Logical file size differs from allocated size. Compression, sparse files, hard links, cloud placeholders, permissions, and filesystem metadata mean these totals are **not** drive usage or recoverable space. Hard-linked file paths can be counted more than once. Moving files to a Recycle Bin would not immediately reclaim their space.

## Assessment

The engine uses location rules with path-component boundaries, prioritizing protected Windows, installed application, shared data, and repository metadata paths. Skipped/incomplete ordinary items remain unknown. Recognizable OneDrive paths explain sync risks; actual sync state is not queried. Only files within the current temporary directory, outside protected locations, get a conditional likely-removable rating. Even there, active use is not verified.

Personal folders, arbitrary directories, and unfamiliar files receive review-first or unknown assessments. Extensions give general descriptions only. No rule claims to know a document's personal value or every dependency. Mixed folders never receive a blanket likely-removable label. Per-user AppData rules currently cover the conventional path; redirected/custom locations may remain unknown.

All assessment text is bundled and processed locally. No telemetry or network calls are made by the application. Scan data is held in memory and not saved. No deletion API or cleanup command is implemented.

## Later milestones

- Windows usability and performance testing on larger real-world folders.
- Better application ownership detection and narrowly scoped cache rules with evidence.
- Correct allocated-size and hard-link accounting.
- A reviewed removal flow using the appropriate Windows/app cleanup facility or recycling. Recheck identity and state immediately before any action; handle cloud propagation and non-recyclable targets explicitly.
- Optional AI phrasing only after deterministic assessments and privacy choices are established.

## Reference material

- [WPF](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/)
- [OpenFolderDialog](https://learn.microsoft.com/en-us/dotnet/api/microsoft.win32.openfolderdialog)
- [Storage Sense](https://learn.microsoft.com/en-us/windows/configuration/storage/storage-sense)
- [Windows.old and rollback](https://support.microsoft.com/en-us/windows/deployment/install-upgrade/delete-your-previous-version-of-windows)
- [OneDrive local and cloud files](https://support.microsoft.com/en-us/office/save-disk-space-with-onedrive-files-on-demand-for-windows-0e6860d3-d9f3-4971-b321-7092438fb38e)
