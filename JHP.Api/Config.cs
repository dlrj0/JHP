using System.Text.Json;
using System.Text.Json.Serialization;

namespace JHP.Api;

public class Config
{
    private static Config? _instance;
    private static readonly object _lock = new();
    private static readonly string ConfigPath = Path.Combine(AppContext.BaseDirectory, "config.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static Config Instance
    {
        get
        {
            lock (_lock)
            {
                _instance ??= Load();
                return _instance;
            }
        }
    }

    public int Width { get; set; } = 800;
    public int Height { get; set; } = 600;
    public int X { get; set; } = 100;
    public int Y { get; set; } = 100;
    public double Opacity { get; set; } = 1.0;
    public int Volume { get; set; } = 50;
    public int Rate { get; set; } = 0;
    public bool IsMaximize { get; set; } = false;
    public bool TopMost { get; set; } = false;
    public bool IsHideWindowBorderOnFocusOut { get; set; } = false;
    public bool MuteVideoOnAlarm { get; set; } = false;
    public bool HotkeyEnabled { get; set; } = false;   // 전역 단축키 Ctrl+Alt+J = 타이머 시작/정지 (기본 꺼짐)
    public List<Site> Sites { get; set; } = [];
    public string DefaultSite { get; set; } = "";
    public bool[] AlarmEnabled { get; set; } = new bool[8];
    public string AlarmName { get; set; } = "경험치업.mp3";
    public bool Tts { get; set; } = false;
    public List<CustomAlarm> CustomAlarms { get; set; } =
    [
        new CustomAlarm(), new CustomAlarm(), new CustomAlarm()
    ];
    public string LatestUrl { get; set; } = "";

    // 읽기 실패(잠김 등) 때문에 기본값으로 시작한 경우 true — 이때 Save()가 정상 파일을 덮어쓰지 않게 한다
    private static bool _loadFailed;

    private static string ReadAllTextWithRetry(string path)
    {
        for (int attempt = 0; ; attempt++)
        {
            try { return File.ReadAllText(path); }
            catch (IOException) when (attempt < 4) { Thread.Sleep(150); }
        }
    }

    // 손상된 설정의 사본을 남긴다. 이미 사본이 있으면 덮어쓰지 않고 시각이 붙은 이름으로 따로 보관한다.
    private static void PreserveCorrupt()
    {
        try
        {
            string target = ConfigPath + ".corrupt";
            if (File.Exists(target)) target = ConfigPath + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            File.Copy(ConfigPath, target, overwrite: true);
        }
        catch { }
    }

    private static Config Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                string json = ReadAllTextWithRetry(ConfigPath);
                var loaded = JsonSerializer.Deserialize<Config>(json, JsonOptions);
                if (loaded is not null) return loaded.Normalize();
                // 내용이 null 등 쓸 수 없는 값이면 손상된 것으로 취급
                PreserveCorrupt();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 백신/동기화 등이 파일을 잠근 경우: 파일은 멀쩡하므로 기본값으로 시작하되 이번 실행에서는 덮어쓰지 않는다.
            _loadFailed = true;
        }
        catch
        {
            // 설정 파일이 손상된 경우: 다음 저장에서 덮어써 사라지지 않도록 사본을 남기고 기본값으로 시작한다.
            PreserveCorrupt();
        }

        // 설정 파일이 없거나(첫 실행) 읽을 수 없을 때 원본 재획플레이어의 기본 사이트 목록을 채워 준다.
        var config = new Config();
        {
            config.Sites =
            [
                new Site { Name = "넷플릭스", Url = "https://www.netflix.com" },
                new Site { Name = "라프텔", Url = "https://laftel.net" },
                new Site { Name = "웨이브", Url = "https://www.wavve.com" },
                new Site { Name = "티빙", Url = "https://www.tving.com" },
                new Site { Name = "쿠팡플레이", Url = "https://www.coupangplay.com" },
                new Site { Name = "디즈니플러스", Url = "https://www.disneyplus.com" },
                new Site { Name = "유튜브", Url = "https://www.youtube.com" },
            ];
            config.DefaultSite = "유튜브";
        }
        return config;
    }

    // 손으로 고치거나 구버전/손상된 파일에서 온 값을 앱이 쓰는 범위로 보정한다 (배열 길이, null, 범위).
    // 이 값들이 그대로 들어가면 알람 설정창/타이머에서 예외가 나 앱이 종료된다.
    private Config Normalize()
    {
        Width = Width > 0 ? Width : 800;
        Height = Height > 0 ? Height : 600;
        Opacity = double.IsNaN(Opacity) ? 1.0 : Math.Clamp(Opacity, 0.3, 1.0);
        Volume = Math.Clamp(Volume, 0, 100);
        Rate = Math.Clamp(Rate, -10, 10);

        Sites = (Sites ?? []).Where(s => s is not null && !string.IsNullOrWhiteSpace(s.Url)).ToList();
        foreach (var s in Sites) s.Name ??= s.Url;
        DefaultSite ??= "";
        if (!Sites.Any(s => s.Name == DefaultSite)) DefaultSite = ""; // 목록에 없는 이름이 남지 않게
        AlarmName ??= "";
        LatestUrl ??= "";

        var enabled = new bool[8];
        if (AlarmEnabled is not null)
            Array.Copy(AlarmEnabled, enabled, Math.Min(AlarmEnabled.Length, enabled.Length));
        AlarmEnabled = enabled;

        var custom = (CustomAlarms ?? []).Where(c => c is not null).Take(3).ToList();
        while (custom.Count < 3) custom.Add(new CustomAlarm());
        foreach (var c in custom)
        {
            c.Name ??= "";
            c.Tick = Math.Clamp(c.Tick, 0, 99999);
        }
        CustomAlarms = custom;
        return this;
    }

    public void Save()
    {
        if (_loadFailed) return;
        try
        {
            // 임시 파일에 쓴 뒤 교체해, 저장 도중 종료되어도 설정 파일이 반쯤 써진 상태로 남지 않게 한다.
            string tmp = ConfigPath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(this, JsonOptions));
            File.Move(tmp, ConfigPath, overwrite: true);
        }
        catch { }
    }

    // 설정을 다른 위치의 파일로 내보낸다. 실패하면 false.
    public bool ExportTo(string path)
    {
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
            return true;
        }
        catch { return false; }
    }

    // 내보낸 설정 파일을 읽어 보정까지 마친 Config를 돌려준다. 읽을 수 없거나 형식이 틀리면 null.
    public static Config? ReadFrom(string path)
    {
        try
        {
            var loaded = JsonSerializer.Deserialize<Config>(File.ReadAllText(path), JsonOptions);
            return loaded?.Normalize();
        }
        catch { return null; }
    }

    public void Replace(Config other)
    {
        Width = other.Width;
        Height = other.Height;
        X = other.X;
        Y = other.Y;
        Opacity = other.Opacity;
        Volume = other.Volume;
        Rate = other.Rate;
        IsMaximize = other.IsMaximize;
        TopMost = other.TopMost;
        IsHideWindowBorderOnFocusOut = other.IsHideWindowBorderOnFocusOut;
        MuteVideoOnAlarm = other.MuteVideoOnAlarm;
        HotkeyEnabled = other.HotkeyEnabled;
        Sites = other.Sites;
        DefaultSite = other.DefaultSite;
        AlarmEnabled = other.AlarmEnabled;
        AlarmName = other.AlarmName;
        Tts = other.Tts;
        CustomAlarms = other.CustomAlarms;
        LatestUrl = other.LatestUrl;
    }
}