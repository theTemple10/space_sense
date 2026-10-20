namespace SpaceSense.Core;

public enum AssessmentLevel { LikelyRemovable, ReviewFirst, KeepOrUseSupportedCleanup, Unknown }

public sealed record Assessment(AssessmentLevel Level, string Purpose, string DeletionImpact,
    string Recommendation, string Evidence, string Confidence)
{
    public string Label => Level switch
    {
        AssessmentLevel.LikelyRemovable => "Likely removable — conditions apply",
        AssessmentLevel.ReviewFirst => "Review first",
        AssessmentLevel.KeepOrUseSupportedCleanup => "Keep / use supported cleanup",
        _ => "Unknown — keep until identified"
    };
}

public sealed record AssessmentContext(string WindowsDirectory, string ProgramFiles,
    string ProgramFilesX86, string ProgramData, string UserProfile, string TempDirectory)
{
    public static AssessmentContext Local() => new(
        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Path.GetTempPath());
}

/// <summary>Conservative location rules. No file content is read or sent anywhere.</summary>
public sealed class AssessmentEngine(AssessmentContext context)
{
    public Assessment Assess(StorageNode node)
    {
        string path = Normalize(node.FullPath);
        if (Under(path, context.WindowsDirectory))
            return Keep("Windows operating system files and components.",
                "Manual removal could break Windows, updates, or recovery.",
                "Use Windows Storage settings for supported cleanup. Do not remove this item directly.", "Inside the Windows directory");
        if (Under(path, context.ProgramFiles) || Under(path, context.ProgramFilesX86))
            return Keep("Installed application files.", "Removing individual files may stop an application working or updating.",
                "Uninstall the application through Settings > Apps if you no longer need it.", "Inside an installed applications directory");
        if (Under(path, context.ProgramData))
            return Keep("Shared application data, settings, or support files.",
                "Removal could lose shared data or disrupt applications for multiple users.",
                "Identify the owning application and use its cleanup or uninstall options.", "Inside ProgramData");

        string windows = Normalize(context.WindowsDirectory);
        int separator = windows.LastIndexOf('/');
        string previousWindows = separator >= 0 ? windows[..separator] + "/windows.old" : "";
        if (Under(path, previousWindows))
            return Keep("Files retained from a previous Windows installation.",
                "Cleanup removes the option to roll back using these files. They may also contain old personal files.",
                "Check for personal files first, then use Windows Storage > Temporary files if rollback is no longer needed.",
                "Inside Windows.old on the Windows volume");

        if (path.Split('/').Any(segment => segment is ".git" or ".svn"))
            return Keep("Version control history and project metadata.",
                "Deleting this can remove local history, branches, and the project's repository connection.",
                "Keep this metadata. Archive or remove the entire project only when you have verified a backup.", "Version control directory segment");
        if (node.IsLink)
            return Unknown("A link, junction, or cloud reparse point; its target was not scanned.",
                "The target and synchronization behavior have not been verified.", "Reparse point detected; target not followed");
        if (node.IsIncomplete)
            return Unknown("This item could not be fully inspected.",
                "Unread or unscanned contents may contain important data. The displayed size is incomplete.",
                "Scanner reported skipped or unread contents");

        string profile = Normalize(context.UserProfile);
        if (profile.Length > 0 && Under(path, profile) && path[(profile.Length)..].Split('/').Any(
                segment => segment.Equals("onedrive", StringComparison.OrdinalIgnoreCase) || segment.StartsWith("onedrive - ", StringComparison.OrdinalIgnoreCase)))
            return new(AssessmentLevel.ReviewFirst, "An item in a recognizable OneDrive folder.",
                "Deleting a synced item may also delete its cloud copy and copies on other devices.",
                "Verify sync status in OneDrive. Use its 'Free up space' option when you only want to remove the local copy.",
                "OneDrive folder name within the user profile; cloud state was not checked", "Limited — sync status unknown");

        if (Under(path, context.TempDirectory))
        {
            if (node.IsDirectory)
                return new(AssessmentLevel.ReviewFirst, "A folder in the current user's temporary storage.",
                    "It may contain files still needed by an application or installer.",
                    "Do not remove the temporary folder itself. Prefer Windows cleanup and close applications first.",
                    "Inside the current process's temporary directory", "Location match only");
            return new(AssessmentLevel.LikelyRemovable, "A file in the current user's temporary storage.",
                "Applications may recreate it, but an active application or installer could still need it.",
                "Use Windows cleanup where possible. Keep it if an installation or task is in progress; active use was not checked.",
                "Inside the temporary directory, outside protected locations", "Conditional — active use unknown");
        }

        if (profile.Length > 0 && Under(path, profile + "/appdata"))
            return Keep("Per-user application data, which may include settings, databases, sessions, and caches.",
                "Removal may reset applications or erase local data. Not everything in AppData is a cache.",
                "Use the owning application's storage or cache controls. Ownership has not been verified.", "Inside the user's AppData directory");

        string extension = Path.GetExtension(path);
        string purpose = node.IsDirectory ? "A folder that may contain a mixture of personal and application files." : extension switch
        {
            ".pdf" => "A PDF document; its contents and personal value have not been inspected.",
            ".docx" or ".xlsx" or ".pptx" or ".txt" => "A document that may contain personal or work information.",
            ".jpg" or ".jpeg" or ".png" or ".heic" or ".mp4" or ".mov" => "An image or video that may be an original personal file.",
            ".zip" or ".7z" or ".rar" => "An archive that may contain backups or the only copy of files.",
            ".exe" or ".msi" => "An executable or installer. Its role and application ownership are unverified.",
            _ => "The role and application ownership of this file have not been identified."
        };
        bool personal = profile.Length > 0 && new[] { "documents", "downloads", "desktop", "pictures", "videos", "music" }
            .Any(folder => Under(path, profile + "/" + folder));
        return new(personal || node.IsDirectory ? AssessmentLevel.ReviewFirst : AssessmentLevel.Unknown,
            purpose, "Removal could lose data or affect something that depends on this item. Age and size do not prove it is expendable.",
            "Open or identify it and verify a backup before considering removal.",
            personal ? "Recognizable personal folder; contents were not read" : "No verified cleanup rule matched; extension descriptions are general", "Limited — importance unknown");
    }

    private static Assessment Keep(string purpose, string impact, string recommendation, string evidence) =>
        new(AssessmentLevel.KeepOrUseSupportedCleanup, purpose, impact, recommendation, evidence, "Location match — exact contents unverified");
    private static Assessment Unknown(string purpose, string impact, string evidence) =>
        new(AssessmentLevel.Unknown, purpose, impact, "Keep this item until its role and contents can be verified.", evidence, "Insufficient evidence");
    private static string Normalize(string path) => path.Replace('\\', '/').TrimEnd('/').ToLowerInvariant();
    private static bool Under(string path, string root)
    {
        root = Normalize(root);
        return root.Length > 0 && (path.Equals(root, StringComparison.OrdinalIgnoreCase) || path.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase));
    }
}
