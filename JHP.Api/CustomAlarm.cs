namespace JHP.Api;

public class CustomAlarm
{
    public string Name { get; set; } = "";
    public int Tick { get; set; } = 1; // 원본 기본값 1초
    public bool Enabled { get; set; } = false;
}