using System.IO;
using SharkiDesktopGuardian.Models;

namespace SharkiDesktopGuardian.Services;

public sealed class HardwareMonitorService : IAsyncDisposable
{
    private readonly NativeMetricsReader _native = new();
    private readonly NvidiaSmiProvider _nvidia = new();
    private readonly LibreHardwareProbe _libre = new();
    private readonly NetworkSpeedReader _network = new();
    private readonly IReadOnlyList<PhysicalDiskMap> _physicalDisks;
    private readonly SemaphoreSlim _readGate = new(1, 1);
    private CancellationTokenSource? _loopCancellation;
    private Task? _loopTask;
    private AppSettings _settings;

    public HardwareMonitorService(AppSettings settings)
    {
        _settings = settings;
        _physicalDisks = WmiDriveMapper.TryRead(out _);
    }

    public event EventHandler<HardwareSnapshot>? SnapshotAvailable;

    public HardwareSnapshot Latest { get; private set; } = HardwareSnapshot.Empty;

    public bool IsRunning => _loopTask is { IsCompleted: false };

    public void UpdateSettings(AppSettings settings) => _settings = settings;

    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        _loopCancellation = new CancellationTokenSource();
        _loopTask = RunLoopAsync(_loopCancellation.Token);
    }

    public async Task StopAsync()
    {
        var cancellation = _loopCancellation;
        var task = _loopTask;
        _loopCancellation = null;
        _loopTask = null;
        cancellation?.Cancel();

        if (task is not null)
        {
            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
            }
        }

        cancellation?.Dispose();
    }

    public async Task<HardwareSnapshot> ReadNowAsync(CancellationToken cancellationToken = default)
    {
        await _readGate.WaitAsync(cancellationToken);
        try
        {
            var diagnostics = new List<string>();
            var cpuLoad = _native.ReadCpuLoadPercent();
            var memory = _native.ReadMemory();
            var libre = await Task.Run(_libre.Read, cancellationToken);
            var nvidia = await _nvidia.TryReadAsync(cancellationToken);
            var network = _network.ReadSpeed();

            if (!string.IsNullOrWhiteSpace(libre.Error))
            {
                diagnostics.Add("Sensores avanzados: " + libre.Error);
            }

            if (nvidia is null)
            {
                diagnostics.Add("nvidia-smi no ha devuelto datos; se usa el proveedor alternativo si está disponible.");
            }

            var disks = BuildDisks(libre.Storages, _physicalDisks);
            var snapshot = new HardwareSnapshot(
                DateTimeOffset.Now,
                cpuLoad,
                libre.CpuTemperatureC,
                nvidia?.LoadPercent ?? libre.GpuLoadPercent,
                nvidia?.TemperatureC ?? libre.GpuTemperatureC,
                nvidia?.MemoryUsedBytes,
                nvidia?.MemoryTotalBytes,
                memory?.UsedBytes,
                memory?.TotalBytes,
                disks,
                diagnostics,
                network?.DownloadBytesPerSecond,
                network?.UploadBytesPerSecond);

            Latest = snapshot;
            SnapshotAvailable?.Invoke(this, snapshot);
            return snapshot;
        }
        finally
        {
            _readGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _libre.Dispose();
        _readGate.Dispose();
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (_settings.MonitoringEnabled)
            {
                try
                {
                    await ReadNowAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    var failure = HardwareSnapshot.Empty with
                    {
                        CapturedAt = DateTimeOffset.Now,
                        Diagnostics = ["Lectura local fallida: " + exception.Message]
                    };
                    Latest = failure;
                    SnapshotAvailable?.Invoke(this, failure);
                }
            }

            await Task.Delay(_settings.PollingInterval, cancellationToken);
        }
    }

    /// <summary>
    /// Enumera cada unidad fija por letra, igual que el skin de referencia (Disco C:, D:, E:...),
    /// en vez de depender de una lista de modelos preferidos. El espacio libre siempre viene de
    /// <see cref="DriveInfo"/> (no requiere permisos); el modelo y la temperatura son un enriquecimiento
    /// opcional cuando WMI/LibreHardwareMonitor los tienen disponibles.
    /// </summary>
    private static IReadOnlyList<DiskSnapshot> BuildDisks(
        IReadOnlyList<StorageSensor> storageSensors,
        IReadOnlyList<PhysicalDiskMap> physicalDisks)
    {
        var fixedDrives = DriveInfo.GetDrives()
            .Where(drive => drive.DriveType == DriveType.Fixed && drive.IsReady)
            .OrderBy(drive => drive.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var results = new List<DiskSnapshot>();
        var usedSensors = new HashSet<StorageSensor>();

        foreach (var drive in fixedDrives)
        {
            var physical = physicalDisks.FirstOrDefault(candidate =>
                candidate.LogicalRoots.Any(root => root.Equals(drive.Name, StringComparison.OrdinalIgnoreCase)));

            var sensor = physical is not null
                ? storageSensors.FirstOrDefault(candidate =>
                    !usedSensors.Contains(candidate) &&
                    candidate.Model.Contains(physical.Model, StringComparison.OrdinalIgnoreCase))
                : null;
            if (sensor is not null)
            {
                usedSensors.Add(sensor);
            }

            var label = drive.Name.TrimEnd('\\');
            results.Add(new DiskSnapshot(
                label,
                physical?.Model ?? sensor?.Model ?? label,
                drive.Name,
                sensor?.TemperatureC,
                drive.AvailableFreeSpace,
                drive.TotalSize));
        }

        return results;
    }
}
