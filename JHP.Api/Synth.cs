using NAudio.Wave;
using System.Speech.Synthesis;

namespace JHP.Api;

public class Synth
{
    private static Synth? _instance;
    private static readonly object _lock = new();

    public static Synth Instance
    {
        get { lock (_lock) { return _instance ??= new Synth(); } }
    }

    private readonly SpeechSynthesizer _synthesizer = new();
    private IWavePlayer? _wavePlayer;
    private AudioFileReader? _audioFileReader;

    private Synth()
    {
        _synthesizer.SetOutputToDefaultAudioDevice();
    }

    // NAudio: WaveStream은 재생 후 반드시 Dispose 필요
    public void Ring(string alarmName, int volume)
    {
        try
        {
            Stop();
            string path = Path.Combine("alarm", alarmName);
            if (!File.Exists(path)) return;

            _audioFileReader = new AudioFileReader(path) { Volume = volume / 100f };
            _wavePlayer = new WaveOutEvent();
            _wavePlayer.Init(_audioFileReader);
            _wavePlayer.Play();
        }
        catch { }
    }

    public void TTS(string text, int volume, int rate)
    {
        try
        {
            _synthesizer.SpeakAsyncCancelAll();
            _synthesizer.Volume = Math.Clamp(volume, 0, 100);
            _synthesizer.Rate = Math.Clamp(rate, -10, 10);
            _synthesizer.SpeakAsync(text);
        }
        catch { }
    }

    public void SetVolume(int volume)
    {
        try
        {
            if (_audioFileReader != null)
                _audioFileReader.Volume = volume / 100f;
            _synthesizer.Volume = Math.Clamp(volume, 0, 100);
        }
        catch { }
    }

    public void SetRate(int rate)
    {
        try { _synthesizer.Rate = Math.Clamp(rate, -10, 10); }
        catch { }
    }

    public void Stop()
    {
        try
        {
            _wavePlayer?.Stop();
            _wavePlayer?.Dispose();
            _audioFileReader?.Dispose();
            _wavePlayer = null;
            _audioFileReader = null;
            _synthesizer.SpeakAsyncCancelAll();
        }
        catch { }
    }
}