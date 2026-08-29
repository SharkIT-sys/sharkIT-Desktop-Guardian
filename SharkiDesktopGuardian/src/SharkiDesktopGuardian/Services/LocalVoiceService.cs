using System.Globalization;
using System.IO;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using System.Text.Json;
using NAudio.Wave;

namespace SharkiDesktopGuardian.Services;

public sealed record VoiceRecognition(string Text, float Confidence);

public sealed class LocalVoiceService : IDisposable
{
    private const int SampleRate = 16_000;
    private const float SapiConfidenceThreshold = 0.45f;
    private readonly SpeechSynthesizer? _synthesizer;
    private readonly object _synthesisGate = new();
    private readonly string _voskModelPath = Path.Combine(AppContext.BaseDirectory, "Models", "vosk-model-small-es-0.42");
    private SpeechRecognitionEngine? _recognizer;
    private WaveOutEvent? _playback;
    private string[] _phrases = [];
    private string? _voiceName;
    private int _speechGeneration;
    private bool _disposed;

    public event EventHandler<VoiceRecognition>? Recognized;
    public event EventHandler<string>? StatusChanged;

    public LocalVoiceService()
    {
        try
        {
            _synthesizer = new SpeechSynthesizer();
        }
        catch (Exception exception) when (exception is PlatformNotSupportedException or InvalidOperationException)
        {
            // La voz es opcional: un fallo de SAPI nunca debe impedir que arranque la mascota.
        }
    }

    public bool RecognitionAvailable => _recognizer is not null || Directory.Exists(_voskModelPath);
    public bool IsListening { get; private set; }
    public string RecognitionLanguage { get; private set; } = "No disponible";

    /// <summary>Voces de síntesis instaladas en Windows (SAPI5 clásico) disponibles para elegir.</summary>
    public static IReadOnlyList<string> GetInstalledVoiceNames()
    {
        try
        {
            using var probe = new SpeechSynthesizer();
            return probe.GetInstalledVoices()
                .Where(voice => voice.Enabled)
                .Select(voice => voice.VoiceInfo.Name)
                .ToArray();
        }
        catch (Exception exception) when (exception is PlatformNotSupportedException or InvalidOperationException)
        {
            return [];
        }
    }

    public void SetVoiceName(string? voiceName) => _voiceName = string.IsNullOrWhiteSpace(voiceName) ? null : voiceName;

    public void Initialize(IEnumerable<string> phrases)
    {
        if (_disposed)
        {
            return;
        }

        _phrases = phrases.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        TryInitializeSapi();

        if (_recognizer is null && Directory.Exists(_voskModelPath))
        {
            RecognitionLanguage = "Español · Vosk local";
            StatusChanged?.Invoke(this, "Voz local preparada con el modelo español incluido.");
        }
        else if (_recognizer is null)
        {
            StatusChanged?.Invoke(this, "No hay un reconocedor local ni un modelo Vosk incluido.");
        }
    }

    public bool ListenOnce()
    {
        if (!RecognitionAvailable || IsListening || _disposed)
        {
            return false;
        }

        IsListening = true;
        if (_recognizer is not null)
        {
            try
            {
                StatusChanged?.Invoke(this, "Escuchando una orden segura…");
                _recognizer.RecognizeAsync(RecognizeMode.Single);
                return true;
            }
            catch (InvalidOperationException exception)
            {
                IsListening = false;
                StatusChanged?.Invoke(this, "No se pudo iniciar SAPI: " + exception.Message);
                return false;
            }
        }

        _ = RunVoskSessionAsync();
        return true;
    }

    public void Speak(string text, bool enabled)
    {
        if (!enabled || string.IsNullOrWhiteSpace(text) || _disposed || _synthesizer is null)
        {
            return;
        }

        var generation = Interlocked.Increment(ref _speechGeneration);
        _ = SpeakRoboticAsync(text, generation);
    }

    public void StopSpeaking()
    {
        Interlocked.Increment(ref _speechGeneration);
        var playback = Interlocked.Exchange(ref _playback, null);
        playback?.Stop();
        playback?.Dispose();
    }

    private async Task SpeakRoboticAsync(string text, int generation)
    {
        try
        {
            var wavBytes = await Task.Run(() => SynthesizeToWav(text));
            if (_disposed || wavBytes.Length == 0 || generation != Volatile.Read(ref _speechGeneration))
            {
                return;
            }

            var stream = new MemoryStream(wavBytes);
            var reader = new WaveFileReader(stream);
            var robotic = new RobotVoiceSampleProvider(reader.ToSampleProvider());
            var player = new WaveOutEvent();
            player.Init(robotic);
            player.PlaybackStopped += (_, _) =>
            {
                player.Dispose();
                reader.Dispose();
                stream.Dispose();
            };

            var previous = Interlocked.Exchange(ref _playback, player);
            previous?.Stop();
            previous?.Dispose();
            player.Play();
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or NAudio.MmException or PlatformNotSupportedException)
        {
            StatusChanged?.Invoke(this, "La síntesis de voz local no está disponible.");
        }
    }

    private byte[] SynthesizeToWav(string text)
    {
        var synthesizer = _synthesizer;
        if (synthesizer is null)
        {
            return [];
        }

        lock (_synthesisGate)
        {
            if (_voiceName is not null)
            {
                try
                {
                    synthesizer.SelectVoice(_voiceName);
                }
                catch (ArgumentException)
                {
                    // Voz guardada ya no está instalada: se continúa con la voz por defecto.
                }
            }

            using var buffer = new MemoryStream();
            synthesizer.SetOutputToWaveStream(buffer);
            synthesizer.Speak(text);
            synthesizer.SetOutputToNull();
            return buffer.ToArray();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_recognizer is not null)
        {
            try
            {
                _recognizer.RecognizeAsyncCancel();
            }
            catch (InvalidOperationException)
            {
            }

            _recognizer.SpeechRecognized -= OnSpeechRecognized;
            _recognizer.RecognizeCompleted -= OnRecognizeCompleted;
            _recognizer.Dispose();
            _recognizer = null;
        }

        StopSpeaking();
        _synthesizer?.Dispose();
    }

    private void TryInitializeSapi()
    {
        try
        {
            var installed = SpeechRecognitionEngine.InstalledRecognizers();
            var info = installed.FirstOrDefault(item => item.Culture.Name.Equals("es-ES", StringComparison.OrdinalIgnoreCase))
                       ?? installed.FirstOrDefault(item => item.Culture.TwoLetterISOLanguageName.Equals("es", StringComparison.OrdinalIgnoreCase))
                       ?? installed.FirstOrDefault();
            if (info is null)
            {
                return;
            }

            var recognizer = new SpeechRecognitionEngine(info);
            var choices = new Choices(_phrases);
            var builder = new GrammarBuilder(choices) { Culture = info.Culture };
            recognizer.LoadGrammar(new Grammar(builder) { Name = "SharkiSafeCommands" });
            recognizer.SpeechRecognized += OnSpeechRecognized;
            recognizer.RecognizeCompleted += OnRecognizeCompleted;
            recognizer.SetInputToDefaultAudioDevice();
            _recognizer = recognizer;
            RecognitionLanguage = info.Culture.DisplayName + " · SAPI local";
            StatusChanged?.Invoke(this, $"Voz local preparada ({RecognitionLanguage}).");
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or PlatformNotSupportedException)
        {
            _recognizer?.Dispose();
            _recognizer = null;
            StatusChanged?.Invoke(this, "SAPI no está disponible; se comprobará el modelo Vosk incluido. " + exception.Message);
        }
    }

    private async Task RunVoskSessionAsync()
    {
        try
        {
            StatusChanged?.Invoke(this, "Cargando temporalmente el modelo español local…");
            Vosk.Vosk.SetLogLevel(-1);
            using var model = await Task.Run(() => new Vosk.Model(_voskModelPath));
            var grammar = JsonSerializer.Serialize(_phrases
                .Select(phrase => phrase.ToLowerInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Append("[unk]"));
            using var recognizer = new Vosk.VoskRecognizer(model, SampleRate, grammar);
            using var microphone = new WaveInEvent
            {
                DeviceNumber = -1,
                WaveFormat = new WaveFormat(SampleRate, 16, 1),
                BufferMilliseconds = 100,
                NumberOfBuffers = 3
            };

            var gate = new object();
            var recognizedText = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            microphone.DataAvailable += (_, eventArgs) =>
            {
                lock (gate)
                {
                    if (recognizer.AcceptWaveform(eventArgs.Buffer, eventArgs.BytesRecorded))
                    {
                        var text = ParseVoskText(recognizer.Result());
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            recognizedText.TrySetResult(text);
                        }
                    }
                }
            };
            microphone.RecordingStopped += (_, eventArgs) =>
            {
                if (eventArgs.Exception is not null)
                {
                    recognizedText.TrySetException(eventArgs.Exception);
                }

                stopped.TrySetResult();
            };

            microphone.StartRecording();
            StatusChanged?.Invoke(this, "Escuchando una orden segura durante un máximo de 8 segundos…");
            var completed = await Task.WhenAny(recognizedText.Task, Task.Delay(TimeSpan.FromSeconds(8)));
            microphone.StopRecording();
            await stopped.Task.WaitAsync(TimeSpan.FromSeconds(2));

            string text;
            if (completed == recognizedText.Task)
            {
                text = await recognizedText.Task;
            }
            else
            {
                lock (gate)
                {
                    text = ParseVoskText(recognizer.FinalResult());
                }
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                StatusChanged?.Invoke(this, "No se detectó una orden incluida en la lista segura.");
            }
            else
            {
                Recognized?.Invoke(this, new VoiceRecognition(text, 0.85f));
            }
        }
        catch (Exception exception)
        {
            StatusChanged?.Invoke(this, "La escucha local ha fallado: " + exception.Message);
        }
        finally
        {
            IsListening = false;
            StatusChanged?.Invoke(this, "Voz local preparada.");
        }
    }

    private void OnSpeechRecognized(object? sender, SpeechRecognizedEventArgs eventArgs)
    {
        if (eventArgs.Result.Confidence >= SapiConfidenceThreshold)
        {
            Recognized?.Invoke(this, new VoiceRecognition(eventArgs.Result.Text, eventArgs.Result.Confidence));
        }
        else
        {
            StatusChanged?.Invoke(this, $"No he entendido con suficiente seguridad (creí oír «{eventArgs.Result.Text}»).");
        }
    }

    private void OnRecognizeCompleted(object? sender, RecognizeCompletedEventArgs eventArgs)
    {
        IsListening = false;
        StatusChanged?.Invoke(this, eventArgs.Result is null
            ? "No se detectó una orden válida."
            : "Voz local preparada.");
    }

    private static string ParseVoskText(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty("text", out var text) ? text.GetString() ?? string.Empty : string.Empty;
    }
}
