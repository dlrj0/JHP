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
                return JsonSerializer.Deserialize<Config>(json, JsonOptions) ?? new Config();
            }
        }
        catch { }

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

    public void Save()
    {
        try
        {
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, JsonOptions));
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