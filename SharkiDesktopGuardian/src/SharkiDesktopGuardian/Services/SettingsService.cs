using System.IO;
using System.Text.Json;
using SharkiDesktopGuardian.Models;

namespace SharkiDesktopGuardian.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _dataDirectory;
    private readonly string _settingsPath;

    public SettingsService(string? baseDirectory = null)
    {
        var root = baseDirectory ?? AppContext.BaseDirectory;
        _dataDirectory = Path.Combine(root, "Data");
        _settingsPath = Path.Combine(_dataDirectory, "settings.json");
    }

    public string SettingsPath => _settingsPath;

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_settingsPath))
        {
            var defaults = new AppSettings();
            defaults.Normalize();
            return defaults;
        }

        try
        {
            await using var stream = File.OpenRead(_settingsPath);
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, cancellationToken)
                ?? new AppSettings();
            settings.Normalize();
            return settings;
        }
        catch (JsonException)
        {
            var fallback = new AppSettings();
            fallback.Normalize();
            return fallback;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            var fallback = new AppSettings();
            fallback.Normalize();
            return fallback;
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        settings.Normalize();
        Directory.CreateDirectory(_dataDirectory);

        var temporaryPath = _settingsPath + ".new";
        await using (var stream = new FileStream(
                         temporaryPath,
                         FileMode.Create,
                         FileAccess.Write,
                         FileShare.None,
                         4096,
                         FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        File.Move(temporaryPath, _settingsPath, true);
    }
}
