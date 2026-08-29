using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using SharkiDesktopGuardian.Models;
using SharkiDesktopGuardian.Services;

namespace SharkiDesktopGuardian;

public partial class MainWindow : Window
{
    private static readonly (PetState State, bool RedEyes, string? Caption, string? AlertText, bool AlertCritical)[] DemoSteps =
    [
        (PetState.Idle, false, "Modo demostración: las alertas compactas duran 5 segundos y reaparecen al pasar el cursor.", null, false),
        (PetState.Greeting, false, "Un saludo: así reacciona Sharki cuando interactúas con él.", null, false),
        (PetState.HighLoad, false, null, "CPU · 94%", false),
        (PetState.HighLoad, false, null, "CPU · 94%  ·  GPU · 96%  ·  RAM · 91%", false),
        (PetState.HighMemory, false, null, "RAM · 91%", false),
        (PetState.LowDisk, false, null, "Disco C: · 8% libre", false),
        (PetState.ThermalAlert, true, null, "CPU · 92 °C", true),
    ];

    private readonly SettingsService _settingsService;
    private readonly HardwareMonitorService _monitor;
    private readonly AlertEvaluator _alertEvaluator;
    private readonly SafeCommandRouter _commandRouter;
    private readonly LocalVoiceService _voice;
    private readonly DispatcherTimer _bubbleTimer;
    private readonly DispatcherTimer _demoTimer;
    private readonly DashboardWindow _dashboard;
    private readonly TrayIconService _trayIcon;
    private readonly GlobalClickWatcher _clickWatcher = new();
    private readonly GlobalHotkey _listenHotkey = new();
    private AppSettings _settings;
    private HardwareSnapshot _snapshot = HardwareSnapshot.Empty;
    private AlertStatus _alert;
    private PetState _lastAlertState = PetState.Idle;
    private bool _allowClose;
    private bool _demoActive;
    private bool _bubbleShowsAlert;
    private string? _demoAlertText;
    private bool _demoAlertCritical;
    private int _demoIndex;

    public MainWindow(
        AppSettings settings,
        SettingsService settingsService,
        HardwareMonitorService monitor,
        AlertEvaluator alertEvaluator,
        SafeCommandRouter commandRouter,
        LocalVoiceService voice)
    {
        InitializeComponent();
        _settings = settings;
        _settingsService = settingsService;
        _monitor = monitor;
        _alertEvaluator = alertEvaluator;
        _commandRouter = commandRouter;
        _voice = voice;
        _alert = AlertStatus.Normal(settings.PetName);
        _dashboard = new DashboardWindow();
        _trayIcon = new TrayIconService(settings.PetName);

        _bubbleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(7) };
        _bubbleTimer.Tick += (_, _) =>
        {
            _bubbleTimer.Stop();
            SpeechBubble.Visibility = Visibility.Collapsed;
            _bubbleShowsAlert = false;
        };

        _demoTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
        _demoTimer.Tick += DemoTimer_Tick;

        _dashboard.SettingsRequested += (_, _) => ShowSettings();
        _dashboard.ListenRequested += (_, _) => StartListening();
        _dashboard.SpeechToggleRequested += async (_, _) => await ExecuteCommandAsync(
            _settings.SpeechEnabled ? SafeCommand.DisableSpeech : SafeCommand.EnableSpeech);
        _dashboard.TutorialRequested += (_, _) => ShowTutorial();
        _dashboard.AdvancedSensorsRequested += async (_, _) => await ExecuteCommandAsync(SafeCommand.RequestAdvancedSensors);
        _dashboard.MoveCompleted += (_, _) =>
        {
            _settings.DashboardLeft = _dashboard.Left;
            _settings.DashboardTop = _dashboard.Top;
            _ = SaveSettingsAsync();
        };
        _trayIcon.OpenRequested += (_, _) => Dispatcher.BeginInvoke(ShowDashboard);
        _trayIcon.HideRequested += (_, _) => Dispatcher.BeginInvoke(HideDashboard);
        _trayIcon.ExitRequested += (_, _) => Dispatcher.BeginInvoke(
            new Action(async () => await ExecuteCommandAsync(SafeCommand.ExitApplication)));

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        LocationChanged += (_, _) =>
        {
            _settings.WindowLeft = Left;
            _settings.WindowTop = Top;
        };
        _monitor.SnapshotAvailable += Monitor_SnapshotAvailable;
        _voice.Recognized += Voice_Recognized;
        _voice.StatusChanged += Voice_StatusChanged;
        _clickWatcher.WindowClicked += ClickWatcher_WindowClicked;
        _listenHotkey.Pressed += () => StartListening();
    }

    private void ClickWatcher_WindowClicked(IntPtr clickedRootHwnd)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (!_dashboard.IsVisible)
            {
                return;
            }

            var dashboardHandle = new WindowInteropHelper(_dashboard).Handle;
            var petHandle = new WindowInteropHelper(this).Handle;
            if (clickedRootHwnd != dashboardHandle && clickedRootHwnd != petHandle)
            {
                HideDashboard();
            }
        });
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs eventArgs)
    {
        ApplySettings();
        PlaceOnScreen();
        _voice.Initialize(_commandRouter.SpokenPhrases);
        _listenHotkey.Register(this);
        ShowBubble($"{_settings.PetName} está preparado. Haz doble clic para ver el panel. Ctrl+Alt+S para hablarle.", false);

        if (!_settings.TutorialCompleted)
        {
            Dispatcher.BeginInvoke(ShowTutorial, DispatcherPriority.Background);
        }
    }

    private void PlaceOnScreen()
    {
        var workArea = SystemParameters.WorkArea;
        Left = _settings.WindowLeft.HasValue && double.IsFinite(_settings.WindowLeft.Value)
            ? Math.Clamp(_settings.WindowLeft.Value, workArea.Left, Math.Max(workArea.Left, workArea.Right - Width))
            : workArea.Right - Width - 24;
        Top = _settings.WindowTop.HasValue && double.IsFinite(_settings.WindowTop.Value)
            ? Math.Clamp(_settings.WindowTop.Value, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - Height))
            : workArea.Bottom - Height - 18;
    }

    private void Monitor_SnapshotAvailable(object? sender, HardwareSnapshot snapshot)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (_demoActive)
            {
                return;
            }

            _snapshot = snapshot;
            _alert = _alertEvaluator.Evaluate(snapshot, _settings);
            _dashboard.UpdateSnapshot(snapshot, _alert, _settings);
            ApplyPetState();
        });
    }

    private void ApplyPetState()
    {
        RedEyeOverlay.Visibility = _alert.RedEyes ? Visibility.Visible : Visibility.Collapsed;
        PetAnimator.AnimationsEnabled = _settings.AnimationsEnabled;

        if (!_settings.MonitoringEnabled)
        {
            HideAlertBubbleIfVisible();
            PetAnimator.State = PetState.Paused;
            return;
        }

        if (_voice.IsListening)
        {
            PetAnimator.State = PetState.Waiting;
            return;
        }

        if (_alert.State != PetState.Idle)
        {
            PetAnimator.State = _alert.State;
            if (_alert.State != _lastAlertState)
            {
                ShowActiveAlertBubble();
                _voice.Speak(_alert.Message, _settings.SpeechEnabled);
            }

            _lastAlertState = _alert.State;
            return;
        }

        HideAlertBubbleIfVisible();
        _lastAlertState = PetState.Idle;
        PetAnimator.State = PetState.Idle;
    }

    private void PetArea_MouseEnter(object sender, MouseEventArgs eventArgs)
    {
        if (_demoActive && !string.IsNullOrWhiteSpace(_demoAlertText))
        {
            ShowAlertBubble(_demoAlertText, _demoAlertCritical);
            return;
        }

        if (_settings.MonitoringEnabled && _alert.State != PetState.Idle)
        {
            ShowActiveAlertBubble();
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs eventArgs)
    {
        if (eventArgs.ClickCount == 2)
        {
            ToggleDashboard();
            eventArgs.Handled = true;
            return;
        }

        if (eventArgs.LeftButton == MouseButtonState.Pressed)
        {
            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
            }

            _ = SaveSettingsAsync();
        }
    }

    private void Window_MouseRightButtonUp(object sender, MouseButtonEventArgs eventArgs)
    {
        PauseMenuItem.Header = _settings.MonitoringEnabled ? "Pausar monitorización" : "Reanudar monitorización";
        DemoMenuItem.IsChecked = _demoActive;
        PetMenu.IsOpen = true;
        eventArgs.Handled = true;
    }

    private void OpenDashboard_Click(object sender, RoutedEventArgs eventArgs) => ShowDashboard();
    private void Listen_Click(object sender, RoutedEventArgs eventArgs) => StartListening();
    private void Settings_Click(object sender, RoutedEventArgs eventArgs) => ShowSettings();
    private void Tutorial_Click(object sender, RoutedEventArgs eventArgs) => ShowTutorial();

    private void ToggleDemo_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (_demoActive)
        {
            StopDemo();
        }
        else
        {
            StartDemo();
        }
    }

    private async void PauseMonitoring_Click(object sender, RoutedEventArgs eventArgs)
    {
        await ExecuteCommandAsync(_settings.MonitoringEnabled ? SafeCommand.PauseMonitoring : SafeCommand.ResumeMonitoring);
    }

    private async void Exit_Click(object sender, RoutedEventArgs eventArgs) => await ExecuteCommandAsync(SafeCommand.ExitApplication);

    private void StartListening()
    {
        if (!_settings.VoiceEnabled)
        {
            ShowBubble("La voz local está desactivada en ajustes.", false);
            return;
        }

        if (!_voice.ListenOnce())
        {
            ShowBubble("El reconocimiento local no está disponible o ya está escuchando.", false);
        }
    }

    private void Voice_Recognized(object? sender, VoiceRecognition recognition)
    {
        Dispatcher.BeginInvoke(async () =>
        {
            if (_commandRouter.TryRoute(recognition.Text, out var command))
            {
                await ExecuteCommandAsync(command);
            }
            else
            {
                ShowBubble($"Escuché: «{recognition.Text}», pero no pertenece a la lista segura. Di: Sharki, qué puedes hacer.", false);
            }
        });
    }

    private void Voice_StatusChanged(object? sender, string status) => Dispatcher.BeginInvoke(() =>
    {
        MicrophoneButton.IsEnabled = !_voice.IsListening;
        ApplyPetState();
        ShowBubble(status, false);
    });

    private async Task ExecuteCommandAsync(SafeCommand command)
    {
        var policy = _commandRouter.GetPolicy(command);
        if (policy.RequiresConfirmation && MessageBox.Show(
                this,
                policy.ConfirmationText,
                "Confirmación explícita",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No) != MessageBoxResult.Yes)
        {
            ShowBubble("Acción cancelada.", false);
            return;
        }

        switch (command)
        {
            case SafeCommand.ShowSystemStatus:
                ShowDashboard();
                Respond(BuildStatusSummary());
                break;
            case SafeCommand.OpenDashboard:
                ShowDashboard();
                Respond("Panel de rendimiento abierto.");
                break;
            case SafeCommand.CloseDashboard:
                HideDashboard();
                Respond("Panel cerrado.");
                break;
            case SafeCommand.PauseMonitoring:
                _settings.MonitoringEnabled = false;
                _monitor.UpdateSettings(_settings);
                ApplyPetState();
                Respond("Monitorización pausada.");
                break;
            case SafeCommand.ResumeMonitoring:
                _settings.MonitoringEnabled = true;
                _monitor.UpdateSettings(_settings);
                _ = _monitor.ReadNowAsync();
                Respond("Monitorización reanudada.");
                break;
            case SafeCommand.ShowTemperatures:
                Respond($"CPU {FormatTemperature(_snapshot.CpuTemperatureC)}. GPU {FormatTemperature(_snapshot.GpuTemperatureC)}.");
                break;
            case SafeCommand.ShowMemory:
                Respond(_snapshot.MemoryPercent.HasValue
                    ? $"La memoria está al {_snapshot.MemoryPercent:0} por ciento."
                    : "El uso de memoria no está disponible.");
                break;
            case SafeCommand.ShowDisks:
                Respond(_snapshot.Disks.Count == 0
                    ? "No hay datos de discos disponibles."
                    : string.Join(". ", _snapshot.Disks.Select(disk => disk.FreePercent.HasValue
                        ? $"{disk.Name} tiene {disk.FreePercent:0} por ciento libre"
                        : $"{disk.Name} no tiene dato de espacio")) + ".");
                break;
            case SafeCommand.EnableSpeech:
                _settings.SpeechEnabled = true;
                _dashboard.SetSpeechEnabled(true);
                Respond("Respuesta de voz activada.");
                break;
            case SafeCommand.DisableSpeech:
                _settings.SpeechEnabled = false;
                _voice.StopSpeaking();
                _dashboard.SetSpeechEnabled(false);
                ShowBubble("Respuesta de voz desactivada.", false);
                break;
            case SafeCommand.ShowVoiceHelp:
                ShowDashboard();
                Respond("Puedo mostrar estado, temperaturas, memoria y discos; abrir el panel; pausar o reanudar la monitorización, decirte la hora o la fecha, abrir el explorador, la calculadora, el bloc de notas, el administrador de tareas, la configuración o la papelera, mostrar el escritorio, bloquear el equipo y controlar mi voz.");
                break;
            case SafeCommand.ShowTime:
                Respond($"Son las {DateTime.Now:HH:mm}.");
                break;
            case SafeCommand.ShowDate:
                Respond($"Hoy es {DateTime.Now.ToString("dddd, d 'de' MMMM", new CultureInfo("es-ES"))}.");
                break;
            case SafeCommand.OpenFileExplorer:
                RespondWithLaunchResult(SystemShortcutLauncher.OpenFileExplorer(), "Abriendo el explorador de archivos.");
                break;
            case SafeCommand.OpenCalculator:
                RespondWithLaunchResult(SystemShortcutLauncher.OpenCalculator(), "Abriendo la calculadora.");
                break;
            case SafeCommand.OpenNotepad:
                RespondWithLaunchResult(SystemShortcutLauncher.OpenNotepad(), "Abriendo el bloc de notas.");
                break;
            case SafeCommand.OpenTaskManager:
                RespondWithLaunchResult(SystemShortcutLauncher.OpenTaskManager(), "Abriendo el administrador de tareas.");
                break;
            case SafeCommand.OpenSettings:
                RespondWithLaunchResult(SystemShortcutLauncher.OpenSettings(), "Abriendo la configuración de Windows.");
                break;
            case SafeCommand.OpenRecycleBin:
                RespondWithLaunchResult(SystemShortcutLauncher.OpenRecycleBin(), "Abriendo la papelera de reciclaje.");
                break;
            case SafeCommand.ShowDesktop:
                RespondWithLaunchResult(SystemShortcutLauncher.ShowDesktop(), "Mostrando el escritorio.");
                break;
            case SafeCommand.LockComputer:
                RespondWithLaunchResult(SystemShortcutLauncher.LockComputer(), "Bloqueando el equipo.");
                break;
            case SafeCommand.RequestAdvancedSensors:
                RestartElevated();
                return;
            case SafeCommand.ExitApplication:
                await CloseApplicationAsync();
                return;
        }

        await SaveSettingsAsync();
    }

    private void RestartElevated()
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            ShowBubble("No se ha podido determinar el ejecutable actual.", true);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = processPath,
                UseShellExecute = true,
                Verb = "runas"
            });
            _ = CloseApplicationAsync();
        }
        catch (Win32Exception)
        {
            ShowBubble("Elevación cancelada o no disponible.", false);
        }
    }

    private void ShowDashboard()
    {
        PositionDashboard();
        _dashboard.UpdateSnapshot(_snapshot, _alert, _settings);
        _dashboard.Show();
        _settings.DashboardVisible = true;
        _clickWatcher.Start();
    }

    private void PositionDashboard()
    {
        if (!_settings.DashboardLeft.HasValue || !_settings.DashboardTop.HasValue ||
            !double.IsFinite(_settings.DashboardLeft.Value) || !double.IsFinite(_settings.DashboardTop.Value))
        {
            PositionDashboardNearPet();
            return;
        }

        var workArea = SystemParameters.WorkArea;
        _dashboard.Left = Math.Clamp(
            _settings.DashboardLeft.Value,
            workArea.Left,
            Math.Max(workArea.Left, workArea.Right - _dashboard.Width));
        _dashboard.Top = Math.Clamp(
            _settings.DashboardTop.Value,
            workArea.Top,
            Math.Max(workArea.Top, workArea.Bottom - _dashboard.Height));
    }

    private void HideDashboard()
    {
        _dashboard.Hide();
        _settings.DashboardVisible = false;
        _clickWatcher.Stop();
    }

    private void PositionDashboardNearPet()
    {
        var workArea = SystemParameters.WorkArea;
        var left = Left + Width / 2 - _dashboard.Width / 2;
        left = Math.Clamp(left, workArea.Left, Math.Max(workArea.Left, workArea.Right - _dashboard.Width));

        var top = Top - _dashboard.Height - 14;
        if (top < workArea.Top)
        {
            top = Math.Min(Top + Height + 14, workArea.Bottom - _dashboard.Height);
        }

        _dashboard.Left = left;
        _dashboard.Top = top;
    }

    private void ToggleDashboard()
    {
        if (_dashboard.IsVisible)
        {
            HideDashboard();
        }
        else
        {
            ShowDashboard();
        }
    }

    private void ShowSettings()
    {
        var window = new SettingsWindow(_settings) { Owner = _dashboard.IsVisible ? _dashboard : this };
        if (window.ShowDialog() == true)
        {
            _settings = window.Result;
            _monitor.UpdateSettings(_settings);
            ApplySettings();
            _ = SaveSettingsAsync();
            ShowBubble("Ajustes guardados localmente.", false);
        }
    }

    private void ShowTutorial()
    {
        var tutorial = new TutorialWindow(_settings.PetName) { Owner = this };
        tutorial.ShowDialog();
        if (!_settings.TutorialCompleted)
        {
            _settings.TutorialCompleted = true;
            _ = SaveSettingsAsync();
        }
    }

    private void StartDemo()
    {
        if (_demoActive)
        {
            return;
        }

        _demoActive = true;
        _demoIndex = -1;
        DemoMenuItem.IsChecked = true;
        DemoTimer_Tick(this, EventArgs.Empty);
        _demoTimer.Start();
    }

    private void StopDemo() => FinishDemo(true);

    private void FinishDemo(bool announce)
    {
        if (!_demoActive)
        {
            return;
        }

        _demoActive = false;
        _demoTimer.Stop();
        _demoAlertText = null;
        _demoAlertCritical = false;
        DemoMenuItem.IsChecked = false;
        HideBubble();
        _lastAlertState = PetState.Idle;
        ApplyPetState();
        if (announce && _alert.State == PetState.Idle)
        {
            ShowBubble("Modo demostración finalizado.", false);
        }
    }

    private void DemoTimer_Tick(object? sender, EventArgs eventArgs)
    {
        _demoIndex++;
        if (_demoIndex >= DemoSteps.Length)
        {
            FinishDemo(false);
            return;
        }

        var step = DemoSteps[_demoIndex];
        PetAnimator.AnimationsEnabled = _settings.AnimationsEnabled;
        PetAnimator.State = step.State;
        RedEyeOverlay.Visibility = step.RedEyes ? Visibility.Visible : Visibility.Collapsed;
        _demoAlertText = step.AlertText;
        _demoAlertCritical = step.AlertCritical;
        if (!string.IsNullOrWhiteSpace(step.AlertText))
        {
            ShowAlertBubble(step.AlertText, step.AlertCritical);
        }
        else
        {
            ShowBubble(step.Caption ?? string.Empty, step.RedEyes);
        }
    }

    private void ApplySettings()
    {
        Title = _settings.PetName;
        PetScaleTransform.ScaleX = _settings.PetScale;
        PetScaleTransform.ScaleY = _settings.PetScale;
        PetAnimator.AnimationsEnabled = _settings.AnimationsEnabled;
        _voice.SetVoiceName(_settings.VoiceName);
        _dashboard.SetSpeechEnabled(_settings.SpeechEnabled);
        _trayIcon.UpdateName(_settings.PetName);
        try
        {
            var alertColor = (Color)ColorConverter.ConvertFromString(_settings.AlertColor);
            var brush = new SolidColorBrush(alertColor);
            brush.Freeze();
            RedEyeLeft.Fill = brush;
            RedEyeRight.Fill = brush;
        }
        catch (FormatException)
        {
            // Ajuste inválido: se conserva el color rojo por defecto definido en XAML.
        }

        ApplyPetState();
    }

    private string BuildStatusSummary()
    {
        var cpu = _snapshot.CpuLoadPercent.HasValue ? $"CPU {_snapshot.CpuLoadPercent:0}%" : "CPU sin dato";
        var gpu = _snapshot.GpuLoadPercent.HasValue ? $"GPU {_snapshot.GpuLoadPercent:0}%" : "GPU sin dato";
        var ram = _snapshot.MemoryPercent.HasValue ? $"RAM {_snapshot.MemoryPercent:0}%" : "RAM sin dato";
        return $"{cpu}. {gpu}. {ram}. {_alert.Title}.";
    }

    private void Respond(string message)
    {
        ShowBubble(message, false);
        _voice.Speak(message, _settings.SpeechEnabled);
    }

    private void RespondWithLaunchResult(bool launched, string successMessage) =>
        Respond(launched ? successMessage : "No he podido abrirlo en este equipo.");

    private void ShowBubble(string message, bool critical)
    {
        _bubbleShowsAlert = false;
        SpeechText.Text = message;
        SpeechBubble.BorderBrush = new SolidColorBrush(critical ? Colors.OrangeRed : Colors.DeepSkyBlue);
        SpeechBubble.Visibility = Visibility.Visible;
        _bubbleTimer.Stop();
        _bubbleTimer.Interval = TimeSpan.FromSeconds(7);
        _bubbleTimer.Start();
    }

    private void ShowActiveAlertBubble()
    {
        var text = BuildActiveAlertText();
        if (!string.IsNullOrWhiteSpace(text))
        {
            ShowAlertBubble(text, _alert.IsCritical);
        }
    }

    private void ShowAlertBubble(string message, bool critical)
    {
        _bubbleShowsAlert = true;
        SpeechText.Text = message;
        SpeechBubble.BorderBrush = new SolidColorBrush(critical ? Colors.OrangeRed : Colors.Gold);
        SpeechBubble.Visibility = Visibility.Visible;
        _bubbleTimer.Stop();
        _bubbleTimer.Interval = TimeSpan.FromSeconds(5);
        _bubbleTimer.Start();
    }

    private void HideAlertBubbleIfVisible()
    {
        if (!_bubbleShowsAlert)
        {
            return;
        }

        HideBubble();
    }

    private string? BuildActiveAlertText()
    {
        var factors = new List<string>();
        AddHighFactor(factors, "CPU temp.", _snapshot.CpuTemperatureC, _settings.CpuTemperatureWarning, " °C");
        AddHighFactor(factors, "GPU temp.", _snapshot.GpuTemperatureC, _settings.GpuTemperatureWarning, " °C");
        AddHighFactor(factors, "CPU", _snapshot.CpuLoadPercent, _settings.CpuLoadWarning, "%");
        AddHighFactor(factors, "GPU", _snapshot.GpuLoadPercent, _settings.GpuLoadWarning, "%");
        AddHighFactor(factors, "RAM", _snapshot.MemoryPercent, _settings.MemoryWarning, "%");

        foreach (var disk in _snapshot.Disks.Where(item =>
                     item.FreePercent.HasValue && item.FreePercent.Value <= _settings.DiskFreeWarning))
        {
            factors.Add($"Disco {disk.Name} · {disk.FreePercent:0}% libre");
        }

        if (factors.Count > 0)
        {
            return string.Join("  ·  ", factors);
        }

        // Si la histéresis mantiene la alarma unos instantes, conserva al menos el factor principal visible.
        return _alert.State switch
        {
            PetState.ThermalAlert => FormatHighestMetric(
                "CPU temp.", _snapshot.CpuTemperatureC, _settings.CpuTemperatureWarning,
                "GPU temp.", _snapshot.GpuTemperatureC, _settings.GpuTemperatureWarning,
                " °C"),
            PetState.HighMemory when _snapshot.MemoryPercent.HasValue => $"RAM · {_snapshot.MemoryPercent:0}%",
            PetState.HighLoad => FormatHighestMetric(
                "CPU", _snapshot.CpuLoadPercent, _settings.CpuLoadWarning,
                "GPU", _snapshot.GpuLoadPercent, _settings.GpuLoadWarning,
                "%"),
            PetState.LowDisk => FormatLowestDisk(),
            _ => null
        };
    }

    private static void AddHighFactor(
        ICollection<string> factors,
        string name,
        double? value,
        double threshold,
        string suffix)
    {
        if (value.HasValue && value.Value >= threshold)
        {
            factors.Add($"{name} · {value:0}{suffix}");
        }
    }

    private void HideBubble()
    {
        _bubbleTimer.Stop();
        SpeechBubble.Visibility = Visibility.Collapsed;
        _bubbleShowsAlert = false;
    }

    private string? FormatLowestDisk()
    {
        var disk = _snapshot.Disks
            .Where(item => item.FreePercent.HasValue)
            .OrderBy(item => item.FreePercent)
            .FirstOrDefault();
        return disk?.FreePercent is double freePercent
            ? $"Disco {disk.Name} · {freePercent:0}% libre"
            : null;
    }

    private static string? FormatHighestMetric(
        string firstName,
        double? firstValue,
        double firstThreshold,
        string secondName,
        double? secondValue,
        double secondThreshold,
        string suffix)
    {
        if (!firstValue.HasValue && !secondValue.HasValue)
        {
            return null;
        }

        var useFirst = firstValue.HasValue &&
                       (!secondValue.HasValue || firstValue.Value / firstThreshold >= secondValue.Value / secondThreshold);
        var name = useFirst ? firstName : secondName;
        var value = useFirst ? firstValue!.Value : secondValue!.Value;
        return $"{name} · {value:0}{suffix}";
    }

    private async Task SaveSettingsAsync()
    {
        try
        {
            await _settingsService.SaveAsync(_settings);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ShowBubble("No se pudieron guardar los ajustes: " + exception.Message, true);
        }
    }

    private async Task CloseApplicationAsync()
    {
        _allowClose = true;
        _settings.WindowLeft = Left;
        _settings.WindowTop = Top;
        _settings.DashboardVisible = _dashboard.IsVisible;
        await SaveSettingsAsync();
        _clickWatcher.Dispose();
        _listenHotkey.Unregister(this);
        _listenHotkey.Dispose();
        _trayIcon.Dispose();
        _dashboard.AllowClose = true;
        _dashboard.Close();
        Close();
        if (Application.Current is App app)
        {
            await app.ShutdownSafelyAsync();
        }
    }

    private async void MainWindow_Closing(object? sender, CancelEventArgs eventArgs)
    {
        if (_allowClose)
        {
            return;
        }

        eventArgs.Cancel = true;
        await ExecuteCommandAsync(SafeCommand.ExitApplication);
    }

    private static string FormatTemperature(double? value) => value.HasValue ? $"{value:0} grados" : "no disponible";
}
