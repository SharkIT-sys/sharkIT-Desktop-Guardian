namespace SharkiDesktopGuardian.Models;

public sealed record DiskSnapshot(
    string Name,
    string Model,
    string Root,
    double? TemperatureC,
    long? FreeBytes,
    long? TotalBytes,
    double? SensorFreePercent = null)
{
    public double? FreePercent => SensorFreePercent ?? (TotalBytes is > 0 && FreeBytes.HasValue
        ? FreeBytes.Value * 100d / TotalBytes.Value
        : null);
}

public sealed record HardwareSnapshot(
    DateTimeOffset CapturedAt,
    double? CpuLoadPercent,
    double? CpuTemperatureC,
    double? GpuLoadPercent,
    double? GpuTemperatureC,
    long? GpuMemoryUsedBytes,
    long? GpuMemoryTotalBytes,
    long? MemoryUsedBytes,
    long? MemoryTotalBytes,
    IReadOnlyList<DiskSnapshot> Disks,
    IReadOnlyList<string> Diagnostics,
    double? NetworkDownloadBytesPerSecond = null,
    double? NetworkUploadBytesPerSecond = null)
{
    public static HardwareSnapshot Empty { get; } = new(
        DateTimeOffset.MinValue,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        [],
        ["Esperando la primera lectura local."]);

    public double? MemoryPercent => MemoryTotalBytes is > 0 && MemoryUsedBytes.HasValue
        ? MemoryUsedBytes.Value * 100d / MemoryTotalBytes.Value
        : null;
}
