using System.Globalization;
using System.Windows;
using SharkiDesktopGuardian.Models;
using SharkiDesktopGuardian.Services;

namespace SharkiDesktopGuardian;

public partial class SettingsWindow : Window
{
    public SettingsWindow(AppSettings current)
    {
        InitializeComponent();
        Result = current.Clone();
        Populate(Result);
    }

    public AppSettings Result { get; private set; }

    private void Populate(AppSettings settings)
    {
        PetNameBox.Text = settings.PetName;
        AccentColorBox.Text = settings.AccentColor;
        AlertColorBox.Text = settings.AlertColor;
        PetScaleBox.Text = settings.PetScale.ToString("0.##", CultureInfo.CurrentCulture);
        AnimationsCheck.IsChecked = settings.AnimationsEnabled;
        MonitoringCheck.IsChecked = settings.MonitoringEnabled;
        AlwaysElevatedCheck.IsChecked = settings.AlwaysRunElevated;
        PollingBox.Text = settings.PollingSeconds.ToString(CultureInfo.CurrentCulture);
        CpuTemperatureBox.Text = settings.CpuTemperatureWarning.ToString("0.#", CultureInfo.CurrentCulture);
        GpuTemperatureBox.Text = settings.GpuTemperatureWarning.ToString("0.#", CultureInfo.CurrentCulture);
        CpuLoadBox.Text = settings.CpuLoadWarning.ToString("0.#", CultureInfo.CurrentCulture);
        GpuLoadBox.Text = settings.GpuLoadWarning.ToString("0.#", CultureInfo.CurrentCulture);
        MemoryBox.Text = settings.MemoryWarning.ToString("0.#", CultureInfo.CurrentCulture);
        DiskFreeBox.Text = settings.DiskFreeWarning.ToString("0.#", CultureInfo.CurrentCulture);
        VoiceCheck.IsChecked = settings.VoiceEnabled;
        SpeechCheck.IsChecked = settings.SpeechEnabled;

        VoiceNameBox.Items.Add("(voz por defecto de Windows)");
        foreach (var voiceName in LocalVoiceService.GetInstalledVoiceNames())
        {
            VoiceNameBox.Items.Add(voiceName);
        }

        VoiceNameBox.SelectedIndex = string.IsNullOrWhiteSpace(settings.VoiceName)
            ? 0
            : Math.Max(0, VoiceNameBox.Items.IndexOf(settings.VoiceName));
    }

    private void Save_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (!TryReadDouble(PetScaleBox.Text, 0.55, 2.25, out var scale) ||
            !int.TryParse(PollingBox.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var polling) || polling is < 1 or > 15 ||
            !TryReadDouble(CpuTemperatureBox.Text, 50, 110, out var cpuTemperature) ||
            !TryReadDouble(GpuTemperatureBox.Text, 45, 100, out var gpuTemperature) ||
            !TryReadDouble(CpuLoadBox.Text, 40, 100, out var cpuLoad) ||
            !TryReadDouble(GpuLoadBox.Text, 40, 100, out var gpuLoad) ||
            !TryReadDouble(MemoryBox.Text, 40, 100, out var memory) ||
            !TryReadDouble(DiskFreeBox.Text, 2, 40, out var diskFree))
        {
            ValidationText.Text = "Revisa los valores numéricos y sus límites.";
            return;
        }

        Result.PetName = PetNameBox.Text;
        Result.AccentColor = AccentColorBox.Text;
        Result.AlertColor = AlertColorBox.Text;
        Result.PetScale = scale;
        Result.AnimationsEnabled = AnimationsCheck.IsChecked == true;
        Result.MonitoringEnabled = MonitoringCheck.IsChecked == true;
        Result.AlwaysRunElevated = AlwaysElevatedCheck.IsChecked == true;
        Result.PollingSeconds = polling;
        Result.CpuTemperatureWarning = cpuTemperature;
        Result.GpuTemperatureWarning = gpuTemperature;
        Result.CpuLoadWarning = cpuLoad;
        Result.GpuLoadWarning = gpuLoad;
        Result.MemoryWarning = memory;
        Result.DiskFreeWarning = diskFree;
        Result.VoiceEnabled = VoiceCheck.IsChecked == true;
        Result.SpeechEnabled = SpeechCheck.IsChecked == true;
        Result.VoiceName = VoiceNameBox.SelectedIndex <= 0 ? null : (string)VoiceNameBox.SelectedItem;
        Result.Normalize();

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs eventArgs)
    {
        DialogResult = false;
        Close();
    }

    private static bool TryReadDouble(string text, double minimum, double maximum, out double value)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) && value >= minimum && value <= maximum;
    }
}
