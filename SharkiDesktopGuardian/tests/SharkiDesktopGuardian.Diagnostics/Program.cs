using System.Text.Json;
using SharkiDesktopGuardian.Models;
using SharkiDesktopGuardian.Services;

var failures = new List<string>();
var results = new Dictionary<string, object?>();

var router = new SafeCommandRouter();
if (!router.TryRoute("Sharki abre el panel", out var openCommand) || openCommand != SafeCommand.OpenDashboard)
{
    failures.Add("La orden segura para abrir el panel no se enruta.");
}

if (router.TryRoute("Sharki ejecuta powershell", out _))
{
    failures.Add("Una orden arbitraria fue aceptada.");
}

if (!Enum.IsDefined(PetState.CommandNotRecognized))
{
    failures.Add("No existe el estado visual para una orden de voz no válida.");
}

if (!router.GetPolicy(SafeCommand.RequestAdvancedSensors).RequiresConfirmation)
{
    failures.Add("La elevación no exige confirmación.");
}

var settingsRoot = Path.Combine(Path.GetTempPath(), "SharkiDesktopGuardian-Diagnostics", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(settingsRoot);
try
{
    var settingsService = new SettingsService(settingsRoot);
    var settings = new AppSettings { PetId = "mummy", PetName = "Prueba", PollingSeconds = 1, RoboticVoiceEnabled = false };
    await settingsService.SaveAsync(settings);
    var loaded = await settingsService.LoadAsync();
    if (loaded.PetId != "mummy" || loaded.PetName != "Prueba" || loaded.PollingSeconds != 1 || loaded.RoboticVoiceEnabled)
    {
        failures.Add("La persistencia portátil no conserva ajustes.");
    }
    results["settingsPath"] = settingsService.SettingsPath;
}
finally
{
    Directory.Delete(settingsRoot, true);
}

var invalidPetSettings = new AppSettings { PetId = "no-existe" };
invalidPetSettings.Normalize();
if (invalidPetSettings.PetId != PetCatalog.DefaultId || PetCatalog.All.Count != 2)
{
    failures.Add("El catálogo de mascotas no normaliza una selección desconocida.");
}
results["pets"] = PetCatalog.All.Select(pet => new { pet.Id, pet.DisplayName, pet.AtlasResourcePath });

var alertSettings = new AppSettings();
var evaluator = new AlertEvaluator();
var thermalSnapshot = HardwareSnapshot.Empty with
{
    CapturedAt = DateTimeOffset.Now,
    CpuTemperatureC = alertSettings.CpuTemperatureWarning + 1,
    GpuTemperatureC = 50
};
var thermal = evaluator.Evaluate(thermalSnapshot, alertSettings);
if (thermal.State != PetState.ThermalAlert)
{
    failures.Add("La alerta térmica no activa el estado crítico.");
}

try
{
    var noDiskAlert = new AlertEvaluator().Evaluate(HardwareSnapshot.Empty, alertSettings);
    if (noDiskAlert.State == PetState.LowDisk)
    {
        failures.Add("La ausencia de telemetría de discos genera una alerta falsa.");
    }
}
catch (Exception exception)
{
    failures.Add("La evaluación sin discos no es segura: " + exception.Message);
}

var native = new NativeMetricsReader();
_ = native.ReadCpuLoadPercent();
await Task.Delay(250);
results["nativeCpuLoadPercent"] = native.ReadCpuLoadPercent();
var nativeMemory = native.ReadMemory();
results["nativeMemory"] = nativeMemory.HasValue
    ? new { nativeMemory.Value.UsedBytes, nativeMemory.Value.TotalBytes }
    : null;

var nvidia = new NvidiaSmiProvider();
results["nvidia"] = await nvidia.TryReadAsync(CancellationToken.None);

using (var libre = new LibreHardwareProbe())
{
    results["libreHardware"] = libre.Read();
}

results["physicalDisks"] = WmiDriveMapper.TryRead(out var driveMapError);
results["physicalDiskMapError"] = driveMapError;

using (var voice = new LocalVoiceService())
{
    string? voiceStatus = null;
    voice.StatusChanged += (_, status) => voiceStatus = status;
    voice.Initialize(router.SpokenPhrases);
    results["voice"] = new
    {
        voice.RecognitionAvailable,
        voice.RecognitionLanguage,
        Status = voiceStatus
    };
}

var packagedModelPath = Path.Combine(AppContext.BaseDirectory, "Models", "vosk-model-small-es-0.42");
try
{
    Vosk.Vosk.SetLogLevel(-1);
    using var model = new Vosk.Model(packagedModelPath);
    using var recognizer = new Vosk.VoskRecognizer(model, 16_000f, "[\"abre el panel\", \"pausa la monitorización\", \"[unk]\"]");
    results["voskModelLoad"] = new { Ok = true, Path = packagedModelPath };
}
catch (Exception exception)
{
    failures.Add("El modelo Vosk empaquetado no se puede cargar: " + exception.Message);
    results["voskModelLoad"] = new { Ok = false, Path = packagedModelPath, Error = exception.Message };
}

results["safeCommandCount"] = router.SpokenPhrases.Count;
results["networkEnvironment"] = "No se realizan llamadas de red durante este diagnóstico.";
results["failures"] = failures;
results["ok"] = failures.Count == 0;

Console.WriteLine(JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
return failures.Count == 0 ? 0 : 1;
