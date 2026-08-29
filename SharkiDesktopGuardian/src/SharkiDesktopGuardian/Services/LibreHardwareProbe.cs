using LibreHardwareMonitor.Hardware;

namespace SharkiDesktopGuardian.Services;

public sealed record StorageSensor(string Model, double? TemperatureC, double? UsedPercent);

public sealed record LibreSensorSnapshot(
    double? CpuTemperatureC,
    double? GpuTemperatureC,
    double? GpuLoadPercent,
    IReadOnlyList<StorageSensor> Storages,
    string? Error);

public sealed class LibreHardwareProbe : IDisposable
{
    private readonly object _gate = new();
    private Computer? _computer;
    private string? _initializationError;

    public LibreHardwareProbe()
    {
        try
        {
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
                IsStorageEnabled = true,
                IsMotherboardEnabled = false,
                IsControllerEnabled = false,
                IsNetworkEnabled = false
            };
            _computer.Open();
        }
        catch (Exception exception)
        {
            _initializationError = exception.Message;
            _computer?.Close();
            _computer = null;
        }
    }

    public LibreSensorSnapshot Read()
    {
        lock (_gate)
        {
            if (_computer is null)
            {
                return new LibreSensorSnapshot(null, null, null, [], _initializationError ?? "Sensores avanzados no disponibles.");
            }

            try
            {
                foreach (var hardware in _computer.Hardware)
                {
                    UpdateRecursively(hardware);
                }

                double? cpuTemperature = null;
                double? gpuTemperature = null;
                double? gpuLoad = null;
                var storages = new List<StorageSensor>();

                foreach (var hardware in EnumerateHardware(_computer.Hardware))
                {
                    switch (hardware.HardwareType)
                    {
                        case HardwareType.Cpu:
                            cpuTemperature = MaxTemperature(hardware.Sensors, "Package", "Core Max", "CPU");
                            break;
                        case HardwareType.GpuNvidia:
                            gpuTemperature = MaxTemperature(hardware.Sensors, "GPU Core", "Hot Spot", "GPU");
                            gpuLoad = SelectSensorValue(hardware.Sensors, SensorType.Load, "GPU Core", "GPU");
                            break;
                        case HardwareType.Storage:
                            storages.Add(new StorageSensor(
                                hardware.Name,
                                MaxTemperature(hardware.Sensors, "Temperature", "Composite", "Drive"),
                                SelectSensorValue(hardware.Sensors, SensorType.Load, "Used Space", "Used")));
                            break;
                    }
                }

                return new LibreSensorSnapshot(cpuTemperature, gpuTemperature, gpuLoad, storages, null);
            }
            catch (Exception exception)
            {
                return new LibreSensorSnapshot(null, null, null, [], exception.Message);
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _computer?.Close();
            _computer = null;
        }
    }

    private static IEnumerable<IHardware> EnumerateHardware(IEnumerable<IHardware> roots)
    {
        foreach (var hardware in roots)
        {
            yield return hardware;
            foreach (var child in EnumerateHardware(hardware.SubHardware))
            {
                yield return child;
            }
        }
    }

    private static void UpdateRecursively(IHardware hardware)
    {
        hardware.Update();
        foreach (var subHardware in hardware.SubHardware)
        {
            UpdateRecursively(subHardware);
        }
    }

    private static double? MaxTemperature(IEnumerable<ISensor> sensors, params string[] preferredNames)
    {
        var values = sensors
            .Where(sensor => sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue)
            .OrderByDescending(sensor => preferredNames.Any(name => sensor.Name.Contains(name, StringComparison.OrdinalIgnoreCase)))
            .Select(sensor => (double?)sensor.Value!.Value)
            .ToArray();
        return values.Length == 0 ? null : values.Max();
    }

    private static double? SelectSensorValue(IEnumerable<ISensor> sensors, SensorType type, params string[] preferredNames)
    {
        var sensor = sensors
            .Where(candidate => candidate.SensorType == type && candidate.Value.HasValue)
            .OrderByDescending(candidate => preferredNames.Any(name => candidate.Name.Contains(name, StringComparison.OrdinalIgnoreCase)))
            .FirstOrDefault();
        return sensor?.Value;
    }
}
