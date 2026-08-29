using System.Globalization;
using System.Text;

namespace SharkiDesktopGuardian.Services;

public enum SafeCommand
{
    ShowSystemStatus,
    OpenDashboard,
    CloseDashboard,
    PauseMonitoring,
    ResumeMonitoring,
    ShowTemperatures,
    ShowMemory,
    ShowDisks,
    EnableSpeech,
    DisableSpeech,
    ShowVoiceHelp,
    RequestAdvancedSensors,
    ExitApplication,
    ShowTime,
    ShowDate,
    OpenFileExplorer,
    OpenCalculator,
    OpenNotepad,
    OpenTaskManager,
    OpenSettings,
    OpenRecycleBin,
    ShowDesktop,
    LockComputer
}

public sealed record CommandPolicy(bool RequiresConfirmation, string ConfirmationText);

public sealed class SafeCommandRouter
{
    private static readonly IReadOnlyDictionary<string, SafeCommand> Commands =
        new Dictionary<string, SafeCommand>(StringComparer.OrdinalIgnoreCase)
        {
            ["estado del sistema"] = SafeCommand.ShowSystemStatus,
            ["muestra informacion del sistema"] = SafeCommand.ShowSystemStatus,
            ["abre el panel"] = SafeCommand.OpenDashboard,
            ["abre el panel de rendimiento"] = SafeCommand.OpenDashboard,
            ["cierra el panel"] = SafeCommand.CloseDashboard,
            ["pausa la monitorizacion"] = SafeCommand.PauseMonitoring,
            ["reanuda la monitorizacion"] = SafeCommand.ResumeMonitoring,
            ["que temperatura tienes"] = SafeCommand.ShowTemperatures,
            ["muestra las temperaturas"] = SafeCommand.ShowTemperatures,
            ["como esta la memoria"] = SafeCommand.ShowMemory,
            ["como estan los discos"] = SafeCommand.ShowDisks,
            ["habla"] = SafeCommand.EnableSpeech,
            ["silencio"] = SafeCommand.DisableSpeech,
            ["que puedes hacer"] = SafeCommand.ShowVoiceHelp,
            ["activa sensores avanzados"] = SafeCommand.RequestAdvancedSensors,
            ["salir de sharki"] = SafeCommand.ExitApplication,
            ["que hora es"] = SafeCommand.ShowTime,
            ["que horas son"] = SafeCommand.ShowTime,
            ["que dia es hoy"] = SafeCommand.ShowDate,
            ["que fecha es hoy"] = SafeCommand.ShowDate,
            ["que fecha es"] = SafeCommand.ShowDate,
            ["abre el explorador"] = SafeCommand.OpenFileExplorer,
            ["abre el explorador de archivos"] = SafeCommand.OpenFileExplorer,
            ["abre la calculadora"] = SafeCommand.OpenCalculator,
            ["abre el bloc de notas"] = SafeCommand.OpenNotepad,
            ["abre el administrador de tareas"] = SafeCommand.OpenTaskManager,
            ["abre la configuracion"] = SafeCommand.OpenSettings,
            ["abre los ajustes de windows"] = SafeCommand.OpenSettings,
            ["abre la papelera"] = SafeCommand.OpenRecycleBin,
            ["abre la papelera de reciclaje"] = SafeCommand.OpenRecycleBin,
            ["muestra el escritorio"] = SafeCommand.ShowDesktop,
            ["minimiza las ventanas"] = SafeCommand.ShowDesktop,
            ["bloquea el equipo"] = SafeCommand.LockComputer,
            ["bloquea el ordenador"] = SafeCommand.LockComputer
        };

    public IReadOnlyList<string> SpokenPhrases { get; } = Commands.Keys
        .SelectMany(command => new[] { command, "Sharki " + command, "Sharky " + command, "Charqui " + command })
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public bool TryRoute(string recognizedText, out SafeCommand command)
    {
        var normalized = StripWakeWord(Normalize(recognizedText));

        // Coincidencia exacta primero.
        if (Commands.TryGetValue(normalized, out command))
        {
            return true;
        }

        // Coincidencia flexible: la orden reconocida puede traer muletillas de más
        // ("por favor", "eh", "sharki oye") delante o detrás de la frase exacta.
        // Solo se aceptan frases de la lista blanca ya normalizada; nunca texto libre.
        var bestMatch = Commands.Keys
            .Where(phrase => ContainsWholePhrase(normalized, phrase))
            .OrderByDescending(phrase => phrase.Length)
            .FirstOrDefault();

        if (bestMatch is not null)
        {
            command = Commands[bestMatch];
            return true;
        }

        command = default;
        return false;
    }

    private static string StripWakeWord(string normalized)
    {
        foreach (var wakeWord in new[] { "sharki ", "sharky ", "charqui " })
        {
            if (normalized.StartsWith(wakeWord, StringComparison.Ordinal))
            {
                return normalized[wakeWord.Length..].Trim();
            }
        }

        return normalized;
    }

    private static bool ContainsWholePhrase(string haystack, string phrase)
    {
        var words = haystack.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var phraseWords = phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (phraseWords.Length == 0 || phraseWords.Length > words.Length)
        {
            return false;
        }

        for (var start = 0; start <= words.Length - phraseWords.Length; start++)
        {
            var matches = true;
            for (var offset = 0; offset < phraseWords.Length; offset++)
            {
                if (!string.Equals(words[start + offset], phraseWords[offset], StringComparison.OrdinalIgnoreCase))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return true;
            }
        }

        return false;
    }

    public CommandPolicy GetPolicy(SafeCommand command) => command switch
    {
        SafeCommand.RequestAdvancedSensors => new(
            true,
            "Sharki se reiniciará solicitando permisos de administrador. No se cambiarán archivos ni ajustes del sistema. ¿Continuar?"),
        SafeCommand.ExitApplication => new(true, "Se cerrará únicamente Sharki Desktop Guardian. ¿Continuar?"),
        // Bloquear la sesión interrumpe lo que el usuario esté haciendo y el
        // reconocimiento acepta a partir de 0,45 de confianza: un falso positivo
        // no debe dejar el equipo bloqueado sin preguntar antes.
        SafeCommand.LockComputer => new(true, "Se bloqueará la sesión de Windows. ¿Continuar?"),
        _ => new(false, string.Empty)
    };

    public static string Normalize(string text)
    {
        var decomposed = text.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.IsPunctuation(character) ? ' ' : character);
            }
        }

        return string.Join(' ', builder.ToString().Normalize(NormalizationForm.FormC)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
