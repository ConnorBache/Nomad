using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Nomad.Core;

namespace Nomad.Windows.App;

internal static partial class WindowsDiscovery
{
    private static readonly (string Name, Guid Id)[] KnownFolders =
    [
        ("Desktop", new("B4BFCC3A-DB2C-424C-B029-7FE99A87C641")),
        ("Documents", new("FDD39AD0-238F-46AF-ADB4-6C85480369C7")),
        ("Downloads", new("374DE290-123F-4565-9164-39C4925E467B")),
        ("Pictures", new("33E28130-4E1E-4676-835A-98395C3BC3BB")),
        ("Music", new("4BD8D571-6D19-48D3-BE97-422220080E43")),
        ("Videos", new("18989B1D-99B5-455B-841C-AB7C74E4DDFC")),
        ("Favorites", new("1777F761-68AD-4D8A-87BD-30B759FA33DD")),
        ("Saved Games", new("4C5C32FF-BB9D-43B0-B5B4-2D72E54EAAA4")),
        ("Contacts", new("56784854-C6CB-462B-8169-88E350ACB882")),
        ("Roaming AppData", new("3EB685DB-65F9-4CF6-A03A-E3EF65729F3D")),
        ("Local AppData", new("F1B32785-6FBA-4FCF-9D55-7B8E7F157091")),
        ("LocalLow AppData", new("A520A1A4-1780-4FF6-BD18-167343C5AF16"))
    ];

    // Query the Windows storage provider without elevation or writable cmdlets.
    private const string HardwareScript = """
        $ErrorActionPreference = 'Stop'
        [Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
        $issues = [System.Collections.Generic.List[string]]::new()
        function Read-Inventory($label, $query) {
            try { & $query } catch { $issues.Add("Windows did not provide $label.") }
        }
        $os = Read-Inventory 'the Windows edition' { Get-CimInstance Win32_OperatingSystem }
        $computer = Read-Inventory 'installed memory' { Get-CimInstance Win32_ComputerSystem }
        $processors = @(Read-Inventory 'processor information' { Get-CimInstance Win32_Processor | ForEach-Object Name })
        $graphics = @(Read-Inventory 'graphics adapters' { Get-CimInstance Win32_VideoController | ForEach-Object Name })
        $controllers = @(Read-Inventory 'storage controllers' {
            Get-CimInstance Win32_SCSIController | ForEach-Object Name
            Get-CimInstance Win32_IDEController | ForEach-Object Name
        })
        $network = @(Read-Inventory 'physical network adapters' {
            Get-CimInstance Win32_NetworkAdapter -Filter 'PhysicalAdapter=True' | ForEach-Object Name
        })
        $disks = @(Read-Inventory 'physical disks' {
            Get-Disk | Sort-Object Number | ForEach-Object {
                @{number=$_.Number; model=$_.FriendlyName; guid=$_.Guid; busType=[string]$_.BusType;
                  partitionStyle=[string]$_.PartitionStyle; healthStatus=[string]$_.HealthStatus;
                  size=$_.Size; isBoot=$_.IsBoot; isSystem=$_.IsSystem}
            }
        })
        $volumes = @(Read-Inventory 'volumes' {
            Get-Volume | Sort-Object Path | ForEach-Object {
                @{driveLetter=if ($_.DriveLetter) { "$($_.DriveLetter):" } else { $null };
                  volumeId=$_.Path; fileSystem=$_.FileSystem; label=$_.FileSystemLabel;
                  size=$_.Size; freeBytes=$_.SizeRemaining}
            }
        })
        $partitions = @(Read-Inventory 'partition layout' {
            Get-Partition | Sort-Object DiskNumber, PartitionNumber | ForEach-Object {
                @{diskNumber=$_.DiskNumber; partitionNumber=$_.PartitionNumber; guid=$_.Guid;
                  gptType=$_.GptType; driveLetter=if ($_.DriveLetter) { "$($_.DriveLetter):" } else { $null };
                  offset=$_.Offset; size=$_.Size}
            }
        })
        $encryption = Read-Inventory 'Windows volume encryption status' {
            Get-BitLockerVolume -MountPoint $env:SystemDrive | ForEach-Object { [string]$_.VolumeStatus }
        }
        $apps = @(Read-Inventory 'installed application names' {
            $roots = @(
                'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall',
                'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall',
                'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall'
            )
            foreach ($root in $roots) {
                if (Test-Path -LiteralPath $root) {
                    Get-ChildItem -LiteralPath $root | ForEach-Object { Get-ItemProperty -LiteralPath $_.PSPath } | Where-Object {
                        $_.DisplayName -and $_.SystemComponent -ne 1 -and -not $_.ParentKeyName
                    } | ForEach-Object DisplayName
                }
            }
        })
        @{hardware=@{edition=$os.Caption; memoryBytes=$computer.TotalPhysicalMemory;
          processors=@($processors | Sort-Object); graphics=@($graphics | Sort-Object);
          storageControllers=@($controllers | Sort-Object); networkAdapters=@($network | Sort-Object);
          disks=$disks; volumes=$volumes; partitions=$partitions; windowsEncryption=$encryption;
          installedApps=@($apps | Sort-Object -CaseSensitive -Unique)};
          issues=@($issues | Sort-Object)} | ConvertTo-Json -Depth 6 -Compress
        """;

    public static (string[] Roots, string[] Issues) ProfileSelection()
    {
        var roots = new List<string>();
        var issues = new List<string>();
        AddPath("Windows profile", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

        foreach (var (name, id) in KnownFolders)
        {
            int result = SHGetKnownFolderPath(id, 0x4000, IntPtr.Zero, out IntPtr value);
            try
            {
                if (result < 0)
                    issues.Add($"Windows did not provide the location of {name}.");
                else
                    AddPath(name, Marshal.PtrToStringUni(value));
            }
            finally
            {
                Marshal.FreeCoTaskMem(value);
            }
        }
        return (roots.Distinct(StringComparer.Ordinal).ToArray(), issues.ToArray());

        void AddPath(string name, string? path)
        {
            try
            {
                if (string.IsNullOrEmpty(path))
                    throw new ArgumentException();
                roots.Add(WindowsPath.Normalize(path));
            }
            catch (ArgumentException)
            {
                issues.Add($"{name} could not be represented as a local drive path.");
            }
        }
    }

    public static async Task<ComputerInventory> InspectAsync(CancellationToken cancellationToken)
    {
        var issues = new List<string>();
        string firmware = GetFirmwareType(out int firmwareType) ? firmwareType switch
        {
            1 => "BIOS",
            2 => "UEFI",
            _ => "Unknown"
        } : "Unknown";
        if (firmware != "UEFI")
            issues.Add("This preview could not establish that Windows booted in UEFI mode.");

        bool? secureBoot = null;
        try
        {
            secureBoot = Microsoft.Win32.Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\SecureBoot\State",
                "UEFISecureBootEnabled", null) switch { 0 => false, 1 => true, _ => null };
        }
        catch (Exception error) when (error is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            // An unreadable value is displayed as unknown.
        }
        if (secureBoot != false)
            issues.Add(secureBoot == true ? "Secure Boot is enabled." : "Secure Boot state could not be verified.");

        string architecture = RuntimeInformation.OSArchitecture.ToString();
        if (architecture != "X64")
            issues.Add("The proposed target requires an x64 computer.");

        HardwareDetails? hardware = null;
        try
        {
            var result = await QueryHardwareAsync(cancellationToken);
            hardware = result?.Hardware;
            issues.AddRange(result?.Issues ?? []);
        }
        catch (Exception error) when (error is Win32Exception or IOException or InvalidOperationException or JsonException)
        {
            // The unavailable provider is reported below without exposing raw system output.
        }
        string? windowsDrive = Path.GetPathRoot(Environment.SystemDirectory)?.TrimEnd('\\');
        if (hardware is null)
            issues.Add("Detailed Windows hardware and storage information was unavailable.");
        else
            CheckSource(hardware, windowsDrive, issues);

        return new ComputerInventory(Environment.OSVersion.VersionString, Environment.OSVersion.Version.Build,
            architecture, firmware, secureBoot, windowsDrive, hardware, issues.Order(StringComparer.Ordinal).ToArray());
    }

    private static void CheckSource(HardwareDetails hardware, string? windowsDrive, List<string> issues)
    {
        PartitionInfo[] partitions = (hardware.Partitions ?? []).Where(partition =>
            windowsDrive is not null && string.Equals(partition.DriveLetter, windowsDrive, StringComparison.OrdinalIgnoreCase)).ToArray();
        DiskInfo[] disks = (hardware.Disks ?? []).Where(disk =>
            partitions.Length == 1 && partitions[0].DiskNumber == disk.Number).ToArray();
        if (disks.Length != 1 || !Guid.TryParse(disks[0].Guid, out Guid diskId) || diskId == Guid.Empty)
            issues.Add("The Windows volume could not be matched to one disk with a stable GPT identity.");
        else if (disks[0].PartitionStyle != "GPT" || disks[0].HealthStatus != "Healthy")
            issues.Add("The Windows disk does not report a healthy GPT configuration.");

        if (!(hardware.Volumes ?? []).Any(volume => windowsDrive is not null &&
                string.Equals(volume.DriveLetter, windowsDrive, StringComparison.OrdinalIgnoreCase) && volume.FileSystem == "NTFS"))
            issues.Add("The Windows volume did not report an NTFS filesystem.");
        if (hardware.WindowsEncryption != "FullyDecrypted")
            issues.Add("Windows did not confirm that its volume is fully decrypted.");
    }

    private static async Task<ProbeResult?> QueryHardwareAsync(CancellationToken cancellationToken)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8
        };
        foreach (string argument in new[] { "-NoProfile", "-NonInteractive", "-EncodedCommand",
                     Convert.ToBase64String(Encoding.Unicode.GetBytes(HardwareScript)) })
            process.StartInfo.ArgumentList.Add(argument);

        process.Start();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        Task<string> stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
        Task<string> stderr = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await Task.WhenAll(stdout, stderr, process.WaitForExitAsync(timeout.Token));
            return process.ExitCode == 0
                ? JsonSerializer.Deserialize<ProbeResult>(await stdout, new JsonSerializerOptions(JsonSerializerDefaults.Web))
                : null;
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            if (!process.HasExited)
            {
                try { process.Kill(); }
                catch (InvalidOperationException) when (process.HasExited) { }
            }
            cancellationToken.ThrowIfCancellationRequested();
            return null;
        }
    }

    private sealed record ProbeResult(HardwareDetails? Hardware, string[]? Issues);

    [DllImport("shell32.dll", ExactSpelling = true)]
    private static extern int SHGetKnownFolderPath(in Guid folderId, uint flags, IntPtr token, out IntPtr path);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFirmwareType(out int firmwareType);
}
