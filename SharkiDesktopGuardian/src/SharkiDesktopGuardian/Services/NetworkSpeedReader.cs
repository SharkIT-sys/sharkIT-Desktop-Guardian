using System.Net.NetworkInformation;

namespace SharkiDesktopGuardian.Services;

public sealed record NetworkSpeed(double DownloadBytesPerSecond, double UploadBytesPerSecond);

/// <summary>
/// Lee contadores locales de interfaz de red (bytes enviados/recibidos acumulados) y calcula
/// una velocidad por delta entre lecturas, igual que el medidor NetIn/NetOut del skin de
/// referencia. Solo lee contadores del sistema operativo; no abre sockets ni realiza tráfico.
/// </summary>
public sealed class NetworkSpeedReader
{
    private readonly object _gate = new();
    private long _previousReceivedBytes;
    private long _previousSentBytes;
    private DateTimeOffset _previousSampleAt;
    private bool _hasPreviousSample;

    public NetworkSpeed? ReadSpeed()
    {
        lock (_gate)
        {
            long received = 0;
            long sent = 0;

            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up ||
                        nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                    {
                        continue;
                    }

                    var stats = nic.GetIPStatistics();
                    received += stats.BytesReceived;
                    sent += stats.BytesSent;
                }
            }
            catch (NetworkInformationException)
            {
                return null;
            }

            var now = DateTimeOffset.Now;
            if (!_hasPreviousSample)
            {
                _previousReceivedBytes = received;
                _previousSentBytes = sent;
                _previousSampleAt = now;
                _hasPreviousSample = true;
                return null;
            }

            var elapsedSeconds = (now - _previousSampleAt).TotalSeconds;
            if (elapsedSeconds <= 0)
            {
                return null;
            }

            var download = Math.Max(0, (received - _previousReceivedBytes) / elapsedSeconds);
            var upload = Math.Max(0, (sent - _previousSentBytes) / elapsedSeconds);

            _previousReceivedBytes = received;
            _previousSentBytes = sent;
            _previousSampleAt = now;

            return new NetworkSpeed(download, upload);
        }
    }
}
