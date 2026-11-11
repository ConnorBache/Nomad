using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nomad.Core;

public sealed record ComputerInventory(
    string WindowsVersion, int WindowsBuild, string Architecture, string Firmware,
    bool? SecureBoot, string? WindowsDrive, HardwareDetails? Hardware, string[] Issues);

public sealed record HardwareDetails(
    string? Edition, long? MemoryBytes, string[]? Processors, string[]? Graphics,
    string[]? StorageControllers, string[]? NetworkAdapters, DiskInfo[]? Disks,
    VolumeInfo[]? Volumes, PartitionInfo[]? Partitions, string? WindowsEncryption, string[]? InstalledApps);

public sealed record DiskInfo(int Number, string? Model, string? Guid, string? BusType,
    string? PartitionStyle, string? HealthStatus, long Size, bool IsBoot, bool IsSystem);

public sealed record PartitionInfo(int DiskNumber, int PartitionNumber, string? Guid,
    string? GptType, string? DriveLetter, long Offset, long Size);

public sealed record VolumeInfo(string? DriveLetter, string? VolumeId, string? FileSystem,
    string? Label, long? Size, long? FreeBytes);

public sealed record InventoryEntry(string WindowsPath, string? VolumeId, string? LinuxHomePath,
    string Kind, string Status, long? SizeBytes, DateTime? ModifiedUtc, string? Note);

public sealed record Inventory(string[] Roots, string[] Exclusions, InventoryEntry[] Entries,
    string[] DiscoveryIssues);

public sealed record PlanPayload(ComputerInventory Computer, Inventory Inventory,
    string Distribution, string Desktop);

public static class PlanExport
{
    public const string Kind = "nomad-read-only-plan";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static byte[] Serialize(ComputerInventory computer, Inventory inventory)
    {
        var payload = new PlanPayload(computer, inventory, "arch", "plasma");
        byte[] payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload, Json);
        string hash = Convert.ToHexStringLower(SHA256.HashData(payloadBytes));

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("schemaVersion", "1.0");
            writer.WriteString("kind", Kind);
            writer.WriteString("planHash", hash);
            writer.WritePropertyName("payload");
            writer.WriteRawValue(payloadBytes);
            writer.WriteEndObject();
        }
        return stream.ToArray();
    }
}
