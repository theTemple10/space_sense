# Manual Windows checks

These are checks to perform on Windows, not claims that they have already passed.

1. Start the app without administrator rights. Confirm the empty state and readable layout at 100% and 150% display scaling.
2. Create a small test folder containing nested folders and files of known sizes. Scan it and compare logical sizes with file properties. Empty folders should display zero.
3. Select table rows and chart segments. Verify the explanation matches the selected path. Expand the folder tree, double-click a folder row, and use Up. Double-clicking a header or empty table area should not navigate.
4. Scan Documents/Downloads. No personal item should be called disposable solely because of its extension or age.
5. Scan the user Temp folder. Individual files may receive a conditional assessment; the whole temporary folder must not be called safe to remove.
6. Inspect an accessible Windows folder and Program Files folder. Assessments should recommend supported cleanup/uninstall, with no direct removal controls.
7. Scan a folder containing an accessible junction/symbolic link and a restricted subfolder. Confirm links are not followed, missing data is disclosed, and parent totals show incompleteness.
8. Scan a recognizable OneDrive folder. Confirm sync risks are explained where entries are readable; placeholders skipped by the scanner should remain unknown. No file should be downloaded by reading its contents.
9. Start a large scan and cancel, then scan again. Confirm the window remains usable and no cancelled result appears as a completed scan.
10. Move/delete a test file during scanning. Check the scan finishes with disclosed omissions or a readable error.
11. Download and fully extract the workflow artifact on an x64 Windows PC without the SDK. Check that the executable starts and scans successfully.
12. Close the app during a scan. Confirm the process exits and no files were modified by the application.
