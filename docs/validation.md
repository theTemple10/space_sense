# Prototype validation — 20 October 2026

Verified in the Linux development workspace using Microsoft's .NET SDK 10.0.402:

```text
dotnet run --project tests/SpaceSense.Checks --configuration Release --no-restore
25 checks passed.

dotnet build SpaceSense.slnx --configuration Release
Build succeeded.
0 Warning(s)
0 Error(s)
```

The checks exercise nested totals, sorting, cancellation, the item cap, root validation, symbolic link loops, conservative assessment boundaries, cloud warnings, unknown files, and Windows paging/hibernation explanations. The symbolic link fixture runs on Linux; Windows requires the manual junction/link check.

The full build compiles C# and WPF markup against the Windows desktop reference assemblies. It does **not** execute the Windows user interface. The [manual Windows checklist](windows-checks.md) remains outstanding, including display scaling, folder picker interactions, cloud placeholders, access restrictions, and starting the packaged preview on a separate Windows PC.

Workflow YAML and project/XAML XML also passed structural parsing. The GitHub Actions workflow has not yet run in the repository. Code compilation does not validate a download artifact, signing, an installer, or all filesystem scenarios.
