using System.Management;

namespace SharkiDesktopGuardian.Services;

public sealed record PhysicalDiskMap(string DeviceId, string Model, long? SizeBytes, IReadOnlyList<string> LogicalRoots);

public static class WmiDriveMapper
{
    public static IReadOnlyList<PhysicalDiskMap> TryRead(out string? error)
    {
        var maps = new List<PhysicalDiskMap>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "root\\CIMV2",
                "SELECT DeviceID, Model, Size FROM Win32_DiskDrive");
            using var drives = searcher.Get();
            foreach (ManagementObject drive in drives)
            {
                var deviceId = Convert.ToString(drive["DeviceID"]) ?? string.Empty;
                var model = Convert.ToString(drive["Model"])?.Trim() ?? deviceId;
                long? size = long.TryParse(Convert.ToString(drive["Size"]), out var parsedSize) ? parsedSize : null;
                maps.Add(new PhysicalDiskMap(deviceId, model, size, FindLogicalRoots(deviceId)));
            }

            error = null;
            return maps;
        }
        catch (Exception exception) when (exception is ManagementException or UnauthorizedAccessException)
        {
            error = exception.Message;
            return maps;
        }
    }

    private static IReadOnlyList<string> FindLogicalRoots(string diskDeviceId)
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var escapedDisk = EscapeWqlPath(diskDeviceId);
        using var partitionSearcher = new ManagementObjectSearcher(
            "root\\CIMV2",
            $"ASSOCIATORS OF {{Win32_DiskDrive.DeviceID='{escapedDisk}'}} WHERE AssocClass=Win32_DiskDriveToDiskPartition");
        using var partitions = partitionSearcher.Get();
        foreach (ManagementObject partition in partitions)
        {
            var partitionId = Convert.ToString(partition["DeviceID"]);
            if (string.IsNullOrWhiteSpace(partitionId))
            {
                continue;
            }

            using var logicalSearcher = new ManagementObjectSearcher(
                "root\\CIMV2",
                $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{EscapeWqlPath(partitionId)}'}} WHERE AssocClass=Win32_LogicalDiskToPartition");
            using var logicalDisks = logicalSearcher.Get();
            foreach (ManagementObject logicalDisk in logicalDisks)
            {
                var root = Convert.ToString(logicalDisk["DeviceID"]);
                if (!string.IsNullOrWhiteSpace(root))
                {
                    roots.Add(root.EndsWith('\\') ? root : root + "\\");
                }
            }
        }

        return roots.OrderBy(root => root, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string EscapeWqlPath(string value) => value.Replace("\\", "\\\\").Replace("'", "\\'");
}
