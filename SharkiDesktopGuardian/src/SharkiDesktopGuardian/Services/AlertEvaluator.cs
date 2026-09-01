using SharkiDesktopGuardian.Models;

namespace SharkiDesktopGuardian.Services;

public sealed class AlertEvaluator
{
    private bool _thermal;
    private bool _highLoad;
    private bool _highMemory;
    private bool _lowDisk;

    public AlertStatus Evaluate(HardwareSnapshot snapshot, AppSettings settings)
    {
        var cpuThermal = IsHigh(snapshot.CpuTemperatureC, settings.CpuTemperatureWarning, settings.AlertHysteresis, _thermal);
        var gpuThermal = IsHigh(snapshot.GpuTemperatureC, settings.GpuTemperatureWarning, settings.AlertHysteresis, _thermal);
        _thermal = cpuThermal || gpuThermal;

        var cpuBusy = IsHigh(snapshot.CpuLoadPercent, settings.CpuLoadWarning, settings.AlertHysteresis, _highLoad);
        var gpuBusy = IsHigh(snapshot.GpuLoadPercent, settings.GpuLoadWarning, settings.AlertHysteresis, _highLoad);
        _highLoad = cpuBusy || gpuBusy;

        _highMemory = UpdateHighFlag(
            _highMemory,
            snapshot.MemoryPercent,
            settings.MemoryWarning,
            settings.AlertHysteresis);

        var freePercentages = snapshot.Disks
            .Select(disk => disk.FreePercent)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToArray();
        double? lowestFree = freePercentages.Length == 0 ? null : freePercentages.Min();
        _lowDisk = UpdateLowFlag(_lowDisk, lowestFree, settings.DiskFreeWarning, settings.AlertHysteresis);

        if (_thermal)
        {
            var reasons = new List<string>();
            if (snapshot.CpuTemperatureC >= settings.CpuTemperatureWarning)
            {
                reasons.Add($"CPU a {snapshot.CpuTemperatureC:0} °C");
            }

            if (snapshot.GpuTemperatureC >= settings.GpuTemperatureWarning)
            {
                reasons.Add($"GPU a {snapshot.GpuTemperatureC:0} °C");
            }

            return new AlertStatus(
                PetState.ThermalAlert,
                "Temperatura elevada",
                reasons.Count > 0 ? string.Join(" · ", reasons) : "Temperatura por encima del umbral.",
                true,
                reasons);
        }

        if (_lowDisk)
        {
            var reasons = snapshot.Disks
                .Where(disk => disk.FreePercent <= settings.DiskFreeWarning)
                .Select(disk => $"{disk.Name}: {disk.FreePercent:0.#}% libre")
                .ToArray();
            return new AlertStatus(PetState.LowDisk, "Poco espacio", string.Join(" · ", reasons), true, reasons);
        }

        if (_highMemory)
        {
            var reason = $"RAM al {snapshot.MemoryPercent:0}%";
            return new AlertStatus(PetState.HighMemory, "Memoria elevada", reason, false, [reason]);
        }

        if (_highLoad)
        {
            var reason = $"CPU {snapshot.CpuLoadPercent:0}% · GPU {snapshot.GpuLoadPercent:0}%";
            return new AlertStatus(PetState.HighLoad, "Carga elevada", reason, false, [reason]);
        }

        return AlertStatus.Normal(settings.PetName);
    }

    private static bool UpdateHighFlag(bool current, double? value, double threshold, double hysteresis)
    {
        if (!value.HasValue)
        {
            return false;
        }

        return current ? value.Value >= threshold - hysteresis : value.Value >= threshold;
    }

    private static bool IsHigh(double? value, double threshold, double hysteresis, bool current) =>
        UpdateHighFlag(current, value, threshold, hysteresis);

    private static bool UpdateLowFlag(bool current, double? value, double threshold, double hysteresis)
    {
        if (!value.HasValue)
        {
            return false;
        }

        return current ? value.Value <= threshold + hysteresis : value.Value <= threshold;
    }
}
