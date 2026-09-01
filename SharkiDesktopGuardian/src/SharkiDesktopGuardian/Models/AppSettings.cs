using System.Text.Json.Serialization;

namespace SharkiDesktopGuardian.Models;

public sealed class AppSettings
{
    public string PetId { get; set; } = PetCatalog.DefaultId;
    public string PetName { get; set; } = "Sharki";
    public string AccentColor { get; set; } = "#00C8FF";
    public double PetScale { get; set; } = 0.6;
    public bool AnimationsEnabled { get; set; } = true;
    public bool MonitoringEnabled { get; set; } = true;
    public bool VoiceEnabled { get; set; } = true;
    public bool SpeechEnabled { get; set; } = true;
    public bool RoboticVoiceEnabled { get; set; } = true;
    public int PollingSeconds { get; set; } = 2;
    public double CpuTemperatureWarning { get; set; } = 90;
    public double GpuTemperatureWarning { get; set; } = 90;
    public double CpuLoadWarning { get; set; } = 90;
    public double GpuLoadWarning { get; set; } = 92;
    public double MemoryWarning { get; set; } = 88;
    public double DiskFreeWarning { get; set; } = 12;
    public double AlertHysteresis { get; set; } = 3;
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double? DashboardLeft { get; set; }
    public double? DashboardTop { get; set; }
    public bool DashboardVisible { get; set; }
    public bool TutorialCompleted { get; set; }
    public bool AlwaysRunElevated { get; set; } = true;
    public string? VoiceName { get; set; }

    [JsonIgnore]
    public TimeSpan PollingInterval => TimeSpan.FromSeconds(PollingSeconds);

    public void Normalize()
    {
        PetId = PetCatalog.NormalizeId(PetId);
        PetName = string.IsNullOrWhiteSpace(PetName) ? "Sharki" : PetName.Trim()[..Math.Min(PetName.Trim().Length, 30)];
        PetScale = Math.Clamp(PetScale, 0.55, 2.25);
        PollingSeconds = Math.Clamp(PollingSeconds, 1, 15);
        CpuTemperatureWarning = Math.Clamp(CpuTemperatureWarning, 50, 110);
        GpuTemperatureWarning = Math.Clamp(GpuTemperatureWarning, 45, 100);
        CpuLoadWarning = Math.Clamp(CpuLoadWarning, 40, 100);
        GpuLoadWarning = Math.Clamp(GpuLoadWarning, 40, 100);
        MemoryWarning = Math.Clamp(MemoryWarning, 40, 100);
        DiskFreeWarning = Math.Clamp(DiskFreeWarning, 2, 40);
        AlertHysteresis = Math.Clamp(AlertHysteresis, 1, 10);
        AccentColor = NormalizeColor(AccentColor, "#00C8FF");
    }

    public AppSettings Clone() => new()
    {
        PetId = PetId,
        PetName = PetName,
        AccentColor = AccentColor,
        PetScale = PetScale,
        AnimationsEnabled = AnimationsEnabled,
        MonitoringEnabled = MonitoringEnabled,
        VoiceEnabled = VoiceEnabled,
        SpeechEnabled = SpeechEnabled,
        RoboticVoiceEnabled = RoboticVoiceEnabled,
        PollingSeconds = PollingSeconds,
        CpuTemperatureWarning = CpuTemperatureWarning,
        GpuTemperatureWarning = GpuTemperatureWarning,
        CpuLoadWarning = CpuLoadWarning,
        GpuLoadWarning = GpuLoadWarning,
        MemoryWarning = MemoryWarning,
        DiskFreeWarning = DiskFreeWarning,
        AlertHysteresis = AlertHysteresis,
        WindowLeft = WindowLeft,
        WindowTop = WindowTop,
        DashboardLeft = DashboardLeft,
        DashboardTop = DashboardTop,
        DashboardVisible = DashboardVisible,
        TutorialCompleted = TutorialCompleted,
        AlwaysRunElevated = AlwaysRunElevated,
        VoiceName = VoiceName
    };

    private static string NormalizeColor(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var candidate = value.Trim();
        if (candidate.Length == 7 && candidate[0] == '#' && candidate[1..].All(Uri.IsHexDigit))
        {
            return candidate.ToUpperInvariant();
        }

        return fallback;
    }
}
