using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace SharkiDesktopGuardian.Services;

public sealed record NvidiaMetrics(
    string Name,
    double? TemperatureC,
    double? LoadPercent,
    long? MemoryUsedBytes,
    long? MemoryTotalBytes);

public sealed class NvidiaSmiProvider
{
    private const string Query = "name,temperature.gpu,utilization.gpu,memory.used,memory.total";

    public async Task<NvidiaMetrics?> TryReadAsync(CancellationToken cancellationToken)
    {
        var systemPath = Path.Combine(Environment.SystemDirectory, "nvidia-smi.exe");
        var executable = File.Exists(systemPath) ? systemPath : "nvidia-smi.exe";
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = $"--query-gpu={Query} --format=csv,noheader,nounits",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8
        };

        try
        {
            using var process = new Process { StartInfo = startInfo };
            if (!process.Start())
            {
                return null;
            }

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            await process.WaitForExitAsync(timeout.Token);
            var output = await outputTask;

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
            {
                return null;
            }

            var line = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)[0];
            var values = line.Split(',').Select(value => value.Trim()).ToArray();
            if (values.Length < 5)
            {
                return null;
            }

            return new NvidiaMetrics(
                values[0],
                ParseDouble(values[1]),
                ParseDouble(values[2]),
                MegabytesToBytes(ParseDouble(values[3])),
                MegabytesToBytes(ParseDouble(values[4])));
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or IOException or OperationCanceledException)
        {
            return null;
        }
    }

    private static double? ParseDouble(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : null;

    private static long? MegabytesToBytes(double? value) =>
        value.HasValue ? checked((long)(value.Value * 1024 * 1024)) : null;
}
