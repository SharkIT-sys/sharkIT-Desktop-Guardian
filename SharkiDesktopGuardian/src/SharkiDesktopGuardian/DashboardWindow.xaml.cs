using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SharkiDesktopGuardian.Models;

namespace SharkiDesktopGuardian;

public partial class DashboardWindow : Window
{
    public DashboardWindow()
    {
        InitializeComponent();
    }

    public event EventHandler? SettingsRequested;
    public event EventHandler? ListenRequested;
    public event EventHandler? SpeechToggleRequested;
    public event EventHandler? TutorialRequested;
    public event EventHandler? AdvancedSensorsRequested;
    public event EventHandler? PointerEntered;
    public event EventHandler? PointerExited;
    public event EventHandler? MoveCompleted;
    public bool AllowClose { get; set; }

    public void UpdateSnapshot(HardwareSnapshot snapshot, AlertStatus alert, AppSettings settings)
    {
        TitleText.Text = $"{settings.PetName} · Estado local";
        UpdatedText.Text = snapshot.CapturedAt == DateTimeOffset.MinValue
            ? "Esperando datos…"
            : $"Actualizado {snapshot.CapturedAt:HH:mm:ss} · cada {settings.PollingSeconds} s";
        StatusText.Text = settings.MonitoringEnabled ? alert.Title : "Monitorización pausada";
        StatusBadge.Background = new SolidColorBrush(alert.IsCritical ? Color.FromRgb(0xFF, 0x50, 0x50) : Color.FromRgb(24, 59, 73));

        SetMetric(CpuValue, CpuBar, snapshot.CpuLoadPercent, settings.CpuLoadWarning);
        CpuTemperature.Text = snapshot.CpuTemperatureC.HasValue ? $"{snapshot.CpuTemperatureC:0.#} °C" : string.Empty;

        SetMetric(GpuValue, GpuBar, snapshot.GpuLoadPercent, settings.GpuLoadWarning);
        GpuTemperature.Text = snapshot.GpuTemperatureC.HasValue ? $"{snapshot.GpuTemperatureC:0.#} °C" : string.Empty;
        GpuMemory.Text = snapshot.GpuMemoryUsedBytes.HasValue && snapshot.GpuMemoryTotalBytes.HasValue
            ? $"VRAM {FormatBytes(snapshot.GpuMemoryUsedBytes.Value)} / {FormatBytes(snapshot.GpuMemoryTotalBytes.Value)}"
            : string.Empty;

        SetMetric(MemoryValue, MemoryBar, snapshot.MemoryPercent, settings.MemoryWarning);
        MemoryDetail.Text = snapshot.MemoryUsedBytes.HasValue && snapshot.MemoryTotalBytes.HasValue
            ? $"{FormatBytes(snapshot.MemoryUsedBytes.Value)} / {FormatBytes(snapshot.MemoryTotalBytes.Value)}"
            : string.Empty;

        NetworkDownloadValue.Text = FormatSpeed(snapshot.NetworkDownloadBytesPerSecond);
        NetworkUploadValue.Text = FormatSpeed(snapshot.NetworkUploadBytesPerSecond);

        BuildDiskRows(snapshot.Disks, settings.DiskFreeWarning);
        BuildDiagnostics(snapshot, settings);
        SetSpeechEnabled(settings.SpeechEnabled);
    }

    public void SetSpeechEnabled(bool enabled)
    {
        SpeechButton.Content = enabled ? "Silenciar" : "Activar voz";
        SpeechButton.ToolTip = enabled
            ? "Silenciar las respuestas habladas"
            : "Activar las respuestas habladas";
    }

    private static string FormatSpeed(double? bytesPerSecond) =>
        bytesPerSecond.HasValue ? $"{FormatBytes((long)bytesPerSecond.Value)}/s" : "—";

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs eventArgs)
    {
        if (eventArgs.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        try
        {
            DragMove();
            MoveCompleted?.Invoke(this, EventArgs.Empty);
        }
        catch (InvalidOperationException)
        {
        }

        eventArgs.Handled = true;
    }

    private void ResizeGrip_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs eventArgs)
    {
        Width = Math.Clamp(Width + eventArgs.HorizontalChange, MinWidth, MaxWidth);
        Height = Math.Clamp(Height + eventArgs.VerticalChange, MinHeight, MaxHeight);
    }

    private void BuildDiskRows(IReadOnlyList<DiskSnapshot> disks, double diskFreeWarning)
    {
        DisksPanel.Children.Clear();
        if (disks.Count == 0)
        {
            DisksPanel.Children.Add(new TextBlock
            {
                Text = "No se encontraron unidades fijas.",
                Style = (Style)FindResource("MetricDetail")
            });
            return;
        }

        var usedWarning = 100 - diskFreeWarning;
        foreach (var disk in disks)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var label = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(disk.Model) || disk.Model == disk.Name ? disk.Name : $"{disk.Name}  ·  {disk.Model}",
                Style = (Style)FindResource("MetricLabel"),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            var usedPercent = disk.FreePercent.HasValue ? 100 - disk.FreePercent.Value : (double?)null;
            var value = new TextBlock
            {
                Text = usedPercent.HasValue ? $"{usedPercent:0}%" : "—",
                Style = (Style)FindResource("MetricValue"),
                FontSize = 16
            };
            Grid.SetColumn(value, 1);

            row.Children.Add(label);
            row.Children.Add(value);

            var bar = new ProgressBar { Style = (Style)FindResource("SkinBar"), Margin = new Thickness(0, 4, 0, 10) };
            ApplyThresholdColor(value, bar, usedPercent, usedWarning);
            bar.Value = Math.Clamp(usedPercent ?? 0, 0, 100);

            DisksPanel.Children.Add(row);
            DisksPanel.Children.Add(bar);
        }
    }

    private void BuildDiagnostics(HardwareSnapshot snapshot, AppSettings settings)
    {
        DiagnosticsPanel.Children.Clear();

        var missingElevatedSensors = !snapshot.CpuTemperatureC.HasValue;
        if (missingElevatedSensors && !settings.AlwaysRunElevated)
        {
            var line = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            line.Children.Add(new TextBlock
            {
                Text = "Temperatura de CPU no disponible sin permisos de administrador.",
                Style = (Style)FindResource("MetricDetail"),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            });
            var activate = new Button { Content = "Activar", Margin = new Thickness(8, 0, 0, 0) };
            activate.Click += (_, _) => AdvancedSensorsRequested?.Invoke(this, EventArgs.Empty);
            Grid.SetColumn(activate, 1);
            line.Children.Add(activate);
            DiagnosticsPanel.Children.Add(line);
        }

        foreach (var diagnostic in snapshot.Diagnostics)
        {
            DiagnosticsPanel.Children.Add(new TextBlock
            {
                Text = diagnostic,
                Style = (Style)FindResource("MetricDetail"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 4)
            });
        }
    }

    private static void SetMetric(TextBlock valueText, ProgressBar bar, double? value, double warningThreshold)
    {
        valueText.Text = value.HasValue ? $"{value:0}%" : "—";
        bar.Value = Math.Clamp(value ?? 0, 0, 100);
        ApplyThresholdColor(valueText, bar, value, warningThreshold);
    }

    private static void ApplyThresholdColor(TextBlock valueText, ProgressBar bar, double? value, double warningThreshold)
    {
        var watchThreshold = Math.Max(0, warningThreshold - 15);
        var brush = value switch
        {
            null => (Brush)Application.Current.FindResource("MutedBrush"),
            var v when v >= warningThreshold => (Brush)Application.Current.FindResource("CriticalBrush"),
            var v when v >= watchThreshold => (Brush)Application.Current.FindResource("WarningBrush"),
            _ => (Brush)Application.Current.FindResource("AccentBrush")
        };
        valueText.Foreground = brush;
        bar.Foreground = brush;
    }

    private static string FormatBytes(long value)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var size = (double)value;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return $"{size:0.#} {units[unit]}";
    }

    private void Settings_Click(object sender, RoutedEventArgs eventArgs) => SettingsRequested?.Invoke(this, EventArgs.Empty);
    private void Listen_Click(object sender, RoutedEventArgs eventArgs) => ListenRequested?.Invoke(this, EventArgs.Empty);
    private void ToggleSpeech_Click(object sender, RoutedEventArgs eventArgs) => SpeechToggleRequested?.Invoke(this, EventArgs.Empty);
    private void Tutorial_Click(object sender, RoutedEventArgs eventArgs) => TutorialRequested?.Invoke(this, EventArgs.Empty);
    private void Hide_Click(object sender, RoutedEventArgs eventArgs) => Hide();

    private void Root_MouseEnter(object sender, System.Windows.Input.MouseEventArgs eventArgs) => PointerEntered?.Invoke(this, EventArgs.Empty);
    private void Root_MouseLeave(object sender, System.Windows.Input.MouseEventArgs eventArgs) => PointerExited?.Invoke(this, EventArgs.Empty);

    private void Window_Closing(object? sender, CancelEventArgs eventArgs)
    {
        if (!AllowClose)
        {
            eventArgs.Cancel = true;
            Hide();
        }
    }
}
