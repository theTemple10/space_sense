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
    var limited = new StorageScanner(2).Scan(fixture);
    Check(limited.LimitReached && limited.Root.IsIncomplete, "Bounded scans disclose incomplete totals");
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();
    try { new StorageScanner().Scan(fixture, cancellationToken: cancellation.Token); throw new InvalidOperationException("Cancellation ignored"); }
    catch (OperationCanceledException) { Check(true, "Scan cancellation is honored"); }
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

Console.WriteLine($"{checks} checks passed.");
