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

    private static Config Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                string json = File.ReadAllText(ConfigPath);
                return (JsonSerializer.Deserialize<Config>(json, JsonOptions) ?? new Config()).Normalize();
            }
        }
        catch
        {
            // 설정 파일이 손상된 경우: 다음 저장에서 덮어써 사라지지 않도록 사본을 남기고 기본값으로 시작한다.
            try { File.Copy(ConfigPath, ConfigPath + ".corrupt", overwrite: true); } catch { }
        }

        // 설정 파일이 없을 때(첫 실행)만 원본 재획플레이어의 기본 사이트 목록을 채워 준다.
        var config = new Config();
        if (!File.Exists(ConfigPath))
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
        try
        {
            // 임시 파일에 쓴 뒤 교체해, 저장 도중 종료되어도 설정 파일이 반쯤 써진 상태로 남지 않게 한다.
            string tmp = ConfigPath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(this, JsonOptions));
            File.Move(tmp, ConfigPath, overwrite: true);
        }
        catch { }
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
        Sites = other.Sites;
        DefaultSite = other.DefaultSite;
        AlarmEnabled = other.AlarmEnabled;
        AlarmName = other.AlarmName;
        Tts = other.Tts;
        CustomAlarms = other.CustomAlarms;
        LatestUrl = other.LatestUrl;
    }
}