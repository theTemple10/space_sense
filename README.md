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
