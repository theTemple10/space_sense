using SpaceSense.Core;

int checks = 0;
void Check(bool condition, string description)
{
    if (!condition) throw new InvalidOperationException(description);
    Console.WriteLine($"PASS {description}");
    checks++;
}

var fixture = Path.Combine(Path.GetTempPath(), "SpaceSenseChecks-" + Guid.NewGuid());
Directory.CreateDirectory(Path.Combine(fixture, "nested"));
try
{
    File.WriteAllBytes(Path.Combine(fixture, "a.bin"), new byte[17]);
    File.WriteAllBytes(Path.Combine(fixture, "nested", "b.bin"), new byte[31]);
    var result = new StorageScanner().Scan(fixture);
    Check(result.Root.Bytes == 48 && result.Items == 4, "Nested file sizes aggregate correctly");
    Check(!result.Root.IsIncomplete && result.UnreadItems == 0, "Complete scan is marked complete");
    Check(result.Root.Children[0].Name == "nested", "Largest child appears first");
    Check(File.ReadAllBytes(Path.Combine(fixture, "a.bin")).Length == 17 &&
        File.ReadAllBytes(Path.Combine(fixture, "nested", "b.bin")).Length == 31,
        "Scanning leaves fixture files intact");
    var limited = new StorageScanner(2).Scan(fixture);
    Check(limited.LimitReached && limited.Root.IsIncomplete, "Bounded scans disclose incomplete totals");
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();
    try { new StorageScanner().Scan(fixture, cancellationToken: cancellation.Token); throw new InvalidOperationException("Cancellation ignored"); }
    catch (OperationCanceledException) { Check(true, "Scan cancellation is honored"); }
    try { new StorageScanner().Scan(Path.Combine(fixture, "a.bin")); throw new InvalidOperationException("File accepted as scan root"); }
    catch (ArgumentException) { Check(true, "A file cannot be used as a folder scan root"); }
    var partialEngine = new AssessmentEngine(new AssessmentContext("", "", "", "", "", fixture));
    Check(partialEngine.Assess(limited.Root).Level == AssessmentLevel.Unknown,
        "An incomplete temporary folder remains unknown");
    if (!OperatingSystem.IsWindows())
    {
        Directory.CreateSymbolicLink(Path.Combine(fixture, "loop"), fixture);
        var linked = new StorageScanner().Scan(fixture);
        Check(linked.Root.Bytes == 48 && linked.UnreadItems == 1 && linked.Root.IsIncomplete,
            "Directory link loops are skipped and disclosed");
        Directory.Delete(Path.Combine(fixture, "loop"));
    }
}
finally { Directory.Delete(fixture, recursive: true); }

var engine = new AssessmentEngine(new AssessmentContext(@"C:\Windows", @"C:\Program Files", @"C:\Program Files (x86)",
    @"C:\ProgramData", @"C:\Users\Ada", @"C:\Users\Ada\AppData\Local\Temp"));
Assessment Evaluate(string path, bool folder = false) => engine.Assess(new StorageNode(path, folder));
Check(Evaluate(@"C:\Windows\Temp\a.tmp").Level == AssessmentLevel.KeepOrUseSupportedCleanup,
    "Protected system paths override temporary-looking names");
Check(Evaluate(@"C:\WindowsExtra\a.tmp").Level == AssessmentLevel.Unknown,
    "Protected path comparisons respect directory boundaries");
Check(Evaluate(@"C:\Users\Ada\Downloads\a.tmp").Level == AssessmentLevel.ReviewFirst,
    "Temporary extension does not make a personal file disposable");
Check(Evaluate(@"C:\Users\Ada\AppData\Local\Temp\a.tmp").Level == AssessmentLevel.LikelyRemovable,
    "Recognized user temp files receive a conditional assessment");
Check(Evaluate(@"C:\Users\Ada\AppData\Local\Temp", true).Level == AssessmentLevel.ReviewFirst,
    "Temporary folders are not blanket cleanup candidates");
Check(Evaluate(@"C:\Users\Ada\AppData\Local\App\data.db").Level == AssessmentLevel.KeepOrUseSupportedCleanup,
    "Application databases are protected by context");
Check(Evaluate(@"C:\Users\Ada\OneDrive - Company\report.pdf").DeletionImpact.Contains("cloud"),
    "Recognizable cloud folders explain propagation risk");
Check(Evaluate(@"C:\Windows.old", true).DeletionImpact.Contains("roll back"),
    "Previous Windows installation explains loss of rollback");
Check(Evaluate(@"D:\Projects\demo\.git\objects", true).Level == AssessmentLevel.KeepOrUseSupportedCleanup,
    "Version control history is protected");
Check(Evaluate(@"D:\unknown.db").Level == AssessmentLevel.Unknown, "Unrecognized database remains unknown");
Check(Evaluate(@"C:\Users\Ada\AppData\Local\TempExtra\a.tmp").Level == AssessmentLevel.KeepOrUseSupportedCleanup,
    "A temporary directory prefix cannot match a neighboring application folder");
Check(Evaluate(@"c:\PROGRAM FILES\App\file.bin").Level == AssessmentLevel.KeepOrUseSupportedCleanup,
    "Windows location comparisons ignore case");
Check(Evaluate(@"D:\demo\.git-copy\file.bin").Level == AssessmentLevel.Unknown,
    "Repository metadata rules match whole path segments");
Check(Evaluate(@"C:\hiberfil.sys").Purpose.Contains("hibernation"), "System hibernation file has a specific explanation");
Check(Evaluate(@"D:\pagefile.sys").Level == AssessmentLevel.KeepOrUseSupportedCleanup,
    "Paging files on another drive remain protected");
Check(Evaluate(@"C:\Users\Ada\Downloads\pagefile.sys").Level == AssessmentLevel.ReviewFirst,
    "A system-like file name in Downloads is assessed in its actual context");
Console.WriteLine($"{checks} checks passed.");
