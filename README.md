# Space Sense

A Windows desktop storage explorer that explains what an item may do and what removing it could affect.

The first prototype is **read-only**: it never deletes, moves, cleans, or uploads your files. Explanations are local rules, not AI guesses.

## For someone new to C# and .NET

- **C#** is the programming language used here.
- **.NET** builds and runs the C# code.
- **WPF** creates the native Windows desktop interface. Its layouts are written in XAML.
- **GitHub** stores the source and commit history. The application runs on your PC.

## Development

Install the .NET 10 SDK on Windows. From the repository folder:

```powershell
dotnet run --project src/SpaceSense.Desktop
```

`src/SpaceSense.Core` contains scanning and assessment logic, separate from the Windows interface in `src/SpaceSense.Desktop`.

This is an early prototype, not a guarantee that any file is safe to remove.

## Try the preview

On a Windows PC, run the command above, select **Choose folder / drive**, and start with a small folder. The app shows a folder tree, largest-first item list, proportional size chart, and an explanation panel. Select an item to see its purpose, deletion impact, recommended next step, evidence, and uncertainty. Double-click a folder in the list to explore it; **Up** returns to the previous folder. **Cancel** stops a scan.

Items are classified as **Likely removable — conditions apply**, **Review first**, **Keep / use supported cleanup**, or **Unknown**. No action in the preview removes anything.

## Download from GitHub

Once this source is pushed and its GitHub Actions run succeeds:

1. Open the repository's **Actions** tab and the latest successful **Build and check Space Sense** run.
2. Download **SpaceSense-Windows-x64-preview** under Artifacts (GitHub sign-in may be required).
3. Extract the entire ZIP into a folder on an x64 Windows PC.
4. Run `SpaceSense.Desktop.exe` from the extracted folder. Keep the accompanying files together.

The download includes the .NET runtime, so you do not need to install the SDK just to try it. This is an unsigned development build, not an installer or a published GitHub Release. Windows may show an unfamiliar-publisher warning. ARM64 packaging is not included yet.

## Checks and building

Core checks run on Windows or Linux with the .NET 10 SDK:

```powershell
dotnet run --project tests/SpaceSense.Checks --configuration Release
```

Build all projects on Windows:

```powershell
dotnet build SpaceSense.slnx --configuration Release
```

The GitHub workflow runs core checks on Linux, builds and checks on Windows, and packages the x64 preview. A cross-build on Linux can check compilation but cannot run WPF; manual Windows checks are still needed.

## Current limits

- Sizes are logical file sizes, not exact disk allocation or promised recoverable space. Hard links may be counted more than once.
- Links, junctions, unreadable entries, and some cloud files are skipped and reported. Large scans stop at 250,000 retained items; incomplete totals are labeled.
- Assessments are conservative heuristics. Application ownership, open-file usage, backups, and actual cloud sync status are not verified.
- The scan reads metadata locally and holds it in memory. It does not request administrator rights or send data to a server.

See [architecture and later milestones](docs/architecture.md) for the design and [manual Windows checks](docs/windows-checks.md) before treating a preview as validated.
