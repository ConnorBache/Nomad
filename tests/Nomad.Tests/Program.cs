using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Nomad.Core;

TestWindowsPaths();
TestInventoryAndExport();
TestDocumentedFixture();
Console.WriteLine("All Nomad preview tests passed.");

static void TestWindowsPaths()
{
    Check(WindowsPath.Normalize(@"c:\Users\Example\..\Example\") == @"C:\Users\Example", "Full path normalization");
    Check(WindowsPath.IsWithin(@"C:\Users\Example\file", @"C:\Users\Example"), "Child path");
    Check(!WindowsPath.IsWithin(@"C:\Users\Example2\file", @"C:\Users\Example"), "Sibling path");
    Check(WindowsPath.ArchivePath(@"D:\Program Files\Café\file") == "Migrated Windows Files/D/Program Files/Café/file", "Archive path");
    Check(WindowsPath.ArchivePath(@"D:\") == "Migrated Windows Files/D", "Volume archive path");
    Check(WindowsPath.ArchivePath(@"D:\" + new string('é', 128)) is null, "UTF-8 component limit");
    Expect<ArgumentException>(() => WindowsPath.Normalize(@"C:relative.txt"), "Drive-relative path");
    Expect<ArgumentException>(() => WindowsPath.Normalize(@"\\?\C:\Windows"), "Device path");
    Expect<ArgumentException>(() => WindowsPath.Normalize(@"C:\file:stream"), "Stream selection");
}

static void TestInventoryAndExport()
{
    DirectoryInfo fixture = Directory.CreateTempSubdirectory("nomad-tests-");
    try
    {
        string root = fixture.FullName;
        string data = Directory.CreateDirectory(Path.Combine(root, "AppData")).FullName;
        string hidden = Path.Combine(data, "settings.json");
        File.WriteAllText(hidden, "{}");
        File.SetAttributes(hidden, FileAttributes.Hidden);
        string excluded = Directory.CreateDirectory(Path.Combine(root, "Excluded")).FullName;
        File.WriteAllText(Path.Combine(excluded, "omitted.txt"), "excluded");
        string extra = Path.Combine(root, "extra.txt");
        File.WriteAllText(extra, "extra");
        string missing = Path.Combine(root, "missing.txt");
        var volumeIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [root[..2]] = "fixture-volume" };

        Inventory result = InventoryScanner.Scan([root, extra, missing], [excluded], volumeIds, [], default);
        var entries = result.Entries.ToDictionary(entry => entry.WindowsPath, StringComparer.Ordinal);
        Check(entries[hidden] is { Kind: "file", Status: "discovered", VolumeId: "fixture-volume", SizeBytes: 2 }, "Hidden metadata");
        Check(entries[excluded].Status == "excluded", "Explicit exclusion");
        Check(!entries.ContainsKey(Path.Combine(excluded, "omitted.txt")), "Excluded subtree");
        Check(entries[extra].Status == "discovered" && result.Entries.Length == 6, "Overlapping roots yield one entry");
        Check(entries[missing].Status == "blocked", "Missing root is visible");
        Check(InventoryScanner.Scan([extra], [], new Dictionary<string, string>(), [], default).Entries.Single().Status == "blocked", "Missing volume identity");

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Expect<OperationCanceledException>(() => InventoryScanner.Scan([root], [], volumeIds, [], cancelled.Token), "Cancelled scan");

        var computer = new ComputerInventory("Windows fixture", 26100, "X64", "UEFI", false, root[..2], null, []);
        Inventory rescanned = InventoryScanner.Scan([missing, extra, root], [excluded], volumeIds, [], default);
        byte[] first = PlanExport.Serialize(computer, result);
        byte[] second = PlanExport.Serialize(computer, rescanned);
        Check(first.AsSpan().SequenceEqual(second), "Plan is deterministic for the same input");
        using var document = JsonDocument.Parse(first);
        CheckPlanHash(document.RootElement);
        Check(!document.RootElement.TryGetProperty("approval", out _), "Preview carries no approval field");
    }
    finally
    {
        fixture.Delete(recursive: true);
    }
}

static void TestDocumentedFixture()
{
    using var fixture = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "example.nomad-plan.json")));
    CheckPlanHash(fixture.RootElement);
}

static void CheckPlanHash(JsonElement envelope)
{
    Check(envelope.GetProperty("schemaVersion").GetString() == "1.0", "Schema version");
    Check(envelope.GetProperty("kind").GetString() == PlanExport.Kind, "Preview document type");
    string payload = envelope.GetProperty("payload").GetRawText();
    string expected = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    Check(envelope.GetProperty("planHash").GetString() == expected, "SHA-256 covers the exact JSON payload");
}

static void Check(bool condition, string description)
{
    if (!condition)
        throw new InvalidOperationException($"Failed: {description}");
}

static void Expect<T>(Action action, string description) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new InvalidOperationException($"Expected {typeof(T).Name}: {description}");
}
