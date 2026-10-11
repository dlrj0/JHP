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

    // 오디오 장치/TTS 음성이 없는 PC에서도 앱이 죽지 않도록 생성 실패를 허용한다 (이 경우 TTS만 동작하지 않음).
    private readonly SpeechSynthesizer? _synthesizer;
    private IWavePlayer? _wavePlayer;
    private AudioFileReader? _audioFileReader;

    /// <summary>
    /// 알람 소리(mp3)/TTS 재생이 끝났을 때(또는 재생할 소리가 없어 끝난 것으로 볼 때) 발생한다.
    /// UI 스레드가 아닐 수 있다. 다른 재생으로 교체되거나 Stop()으로 중단된 경우에는 발생하지 않는다.
    /// </summary>
    public event Action? PlaybackFinished;

    private Synth()
    {
        try
        {
            _synthesizer = new SpeechSynthesizer();
            _synthesizer.SetOutputToDefaultAudioDevice();
            // 새 TTS로 교체되며 취소된 이전 발화의 완료 이벤트는 무시한다 (새 발화가 끝났을 때만 알림)
            _synthesizer.SpeakCompleted += (_, e) =>
            {
                if (!e.Cancelled) RaiseFinished();
            };
        }
        catch { }
    }

    private void RaiseFinished()
    {
        try { PlaybackFinished?.Invoke(); }
        catch { }
    }

    // 재생할 소리가 없거나 짧은 시스템 소리로 대체된 경우: 잠시 뒤 끝난 것으로 알린다
    private void RaiseFinishedLater()
    {
        Task.Run(async () =>
        {
            await Task.Delay(1000).ConfigureAwait(false);
            RaiseFinished();
        });
    }

    // NAudio: WaveStream은 재생 후 반드시 Dispose 필요
    public void Ring(string alarmName, int volume)
    {
        try
        {
            Stop();
            // 작업 디렉터리가 아니라 exe 위치 기준 (바로가기 실행 시에도 alarm 폴더를 찾도록).
            // 파일명만 사용해 alarm 폴더 밖의 파일을 가리키지 못하게 한다.
            string path = Path.Combine(AppContext.BaseDirectory, "alarm", Path.GetFileName(alarmName ?? ""));
            if (!File.Exists(path))
            {
                // 알람 파일이 없어도 알람이 울렸다는 것은 알 수 있게 시스템 소리로 대체 (볼륨 0이면 조용히)
                if (volume > 0) System.Media.SystemSounds.Asterisk.Play();
                RaiseFinishedLater();
                return;
            }

            _audioFileReader = new AudioFileReader(path) { Volume = Math.Clamp(volume, 0, 100) / 100f };
            _wavePlayer = new WaveOutEvent();
            _wavePlayer.PlaybackStopped += OnPlaybackStopped; // 재생이 끝나면 파일 핸들을 바로 해제
            _wavePlayer.Init(_audioFileReader);
            _wavePlayer.Play();
        }
        catch { Stop(); RaiseFinishedLater(); }
    }

    public void TTS(string text, int volume, int rate)
    {
        try
        {
            if (_synthesizer is null)
            {
                RaiseFinishedLater();
                return;
            }
            _synthesizer.SpeakAsyncCancelAll();
            _synthesizer.Volume = Math.Clamp(volume, 0, 100);
            _synthesizer.Rate = Math.Clamp(rate, -10, 10);
            _synthesizer.SpeakAsync(text);
        }
        catch { RaiseFinishedLater(); }
    }

    public void SetVolume(int volume)
    {
        try
        {
            if (_audioFileReader != null)
                _audioFileReader.Volume = Math.Clamp(volume, 0, 100) / 100f;
            if (_synthesizer != null)
                _synthesizer.Volume = Math.Clamp(volume, 0, 100);
        }
        catch { }
    }

    public void SetRate(int rate)
    {
        try { if (_synthesizer != null) _synthesizer.Rate = Math.Clamp(rate, -10, 10); }
        catch { }
    }

    public void Stop()
    {
        try
        {
            var player = _wavePlayer;
            var reader = _audioFileReader;
            _wavePlayer = null;
            _audioFileReader = null;
            if (player != null)
            {
                player.PlaybackStopped -= OnPlaybackStopped;
                player.Stop();
                player.Dispose();
            }
            reader?.Dispose();
            _synthesizer?.SpeakAsyncCancelAll();
        }
        catch { }
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        // 이미 교체된 이전 플레이어의 지연된 이벤트가 새로 시작한 재생을 끊지 않도록 현재 플레이어일 때만 정리
        if (!ReferenceEquals(sender, _wavePlayer)) return;
        Stop();
        RaiseFinished();
    }
}