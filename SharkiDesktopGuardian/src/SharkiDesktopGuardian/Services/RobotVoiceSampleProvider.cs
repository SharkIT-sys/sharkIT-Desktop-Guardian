using NAudio.Wave;

namespace SharkiDesktopGuardian.Services;

/// <summary>
/// Aplica un timbre robótico real a una fuente de audio ya sintetizada mediante modulación
/// en anillo (ring modulation): multiplica la señal por una portadora senoidal de baja
/// frecuencia. Se mezcla con una fracción de la señal original para conservar inteligibilidad.
/// </summary>
public sealed class RobotVoiceSampleProvider : ISampleProvider
{
    private const double CarrierFrequencyHz = 45.0;
    private const float WetMix = 0.8f;

    private readonly ISampleProvider _source;
    private double _phase;

    public RobotVoiceSampleProvider(ISampleProvider source)
    {
        _source = source;
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        var samplesRead = _source.Read(buffer, offset, count);
        var channels = Math.Max(1, WaveFormat.Channels);
        var increment = 2 * Math.PI * CarrierFrequencyHz / WaveFormat.SampleRate;

        for (var i = 0; i < samplesRead; i++)
        {
            var dry = buffer[offset + i];
            var carrier = (float)Math.Sin(_phase);
            buffer[offset + i] = (WetMix * (dry * carrier)) + ((1 - WetMix) * dry);

            // La portadora avanza una vez por frame (todos los canales comparten fase).
            if (i % channels == channels - 1)
            {
                _phase += increment;
                if (_phase > 2 * Math.PI)
                {
                    _phase -= 2 * Math.PI;
                }
            }
        }

        return samplesRead;
    }
}
