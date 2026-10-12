namespace JHP.Api;

/// <summary>
/// 알람 남은 시간 계산(UI와 무관한 순수 로직). Form1의 타이머가 경과 초만큼 <see cref="Advance"/>를 호출한다.
/// 기본 알람 8개(원본 순서)와 커스텀 알람 3개를 관리한다.
/// </summary>
public sealed class AlarmScheduler
{
    public static readonly int[] BuiltInSeconds = [7200, 3600, 1800, 1200, 900, 600, 100, 55];

    // TTS 이름은 원본 항목명. 겹치면 목록 위쪽 하나만 울린다.
    public static readonly string[] BuiltInNames =
        ["재획비", "경쿠", "경뿌/경쿠/유니온", "유니온 버프", "경쿠", "유니온 버프", "메소 회수", "파운틴"];

    public const string DefaultCustomName = "커스텀 알람";
    public const int BuiltInCount = 8;
    public const int CustomCount = 3;

    private readonly int[] _remaining = new int[BuiltInCount];
    private readonly int[] _customRemaining = new int[CustomCount];

    public int GetRemaining(int index) => _remaining[index];
    public int GetCustomRemaining(int index) => _customRemaining[index];

    /// <summary>타이머 시작/설정 가져오기 등: 모든 알람을 처음부터 다시 센다.</summary>
    public void ResetAll(IReadOnlyList<CustomAlarm> custom)
    {
        for (int i = 0; i < BuiltInCount; i++) _remaining[i] = BuiltInSeconds[i];
        for (int i = 0; i < CustomCount; i++) _customRemaining[i] = custom[i].Tick;
    }

    /// <summary>
    /// 경과한 초(steps)만큼 진행한다. 같은 순간 여러 알람이 겹치면 목록 위쪽(기본 → 커스텀) 하나의 이름만 돌려주고,
    /// 모든 알람은 각자 정상적으로 갱신/리셋한다. 울릴 알람이 없으면 null.
    /// </summary>
    public string? Advance(int steps, IReadOnlyList<bool> enabled, IReadOnlyList<CustomAlarm> custom)
    {
        string? toFire = null;
        for (int step = 0; step < steps; step++)
        {
            for (int i = 0; i < BuiltInCount; i++)
            {
                if (!enabled[i]) continue;
                if (--_remaining[i] > 0) continue;
                _remaining[i] = BuiltInSeconds[i];
                toFire ??= BuiltInNames[i];
            }

            for (int i = 0; i < CustomCount; i++)
            {
                var alarm = custom[i];
                if (!alarm.Enabled || alarm.Tick <= 0) continue;
                if (--_customRemaining[i] > 0) continue;
                _customRemaining[i] = alarm.Tick;
                toFire ??= string.IsNullOrWhiteSpace(alarm.Name) ? DefaultCustomName : alarm.Name;
            }
        }
        return toFire;
    }

    /// <summary>타이틀바 남은 시간 표기: 1시간 미만은 "mm:ss", 이상은 "h:mm:ss". 음수는 0으로 본다.</summary>
    public static string FormatRemaining(int seconds)
    {
        var t = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return t.TotalHours >= 1
            ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}"
            : $"{t.Minutes:00}:{t.Seconds:00}";
    }

    /// <summary>켜져 있는 알람 중 가장 가까운 남은 초. 켜진 알람이 없으면 null.</summary>
    public int? MinRemaining(IReadOnlyList<bool> enabled, IReadOnlyList<CustomAlarm> custom)
    {
        int? min = null;
        for (int i = 0; i < BuiltInCount; i++)
            if (enabled[i])
                min = min is null ? _remaining[i] : Math.Min(min.Value, _remaining[i]);
        for (int i = 0; i < CustomCount; i++)
            if (custom[i].Enabled && custom[i].Tick > 0)
                min = min is null ? _customRemaining[i] : Math.Min(min.Value, _customRemaining[i]);
        return min;
    }

    /// <summary>
    /// 알람 설정창에서 확인했을 때: 설정창을 열 때(스냅샷)와 달라진 항목 중 켜진 것만 처음부터 다시 센다.
    /// 확인만 눌러도 진행 중인 알람이 리셋되지 않는다. 스냅샷이 없으면(null) 켜진 항목을 모두 리셋한다.
    /// </summary>
    public void ApplyChanges(
        IReadOnlyList<bool>? previousEnabled, IReadOnlyList<(bool Enabled, int Tick)>? previousCustom,
        IReadOnlyList<bool> enabled, IReadOnlyList<CustomAlarm> custom)
    {
        for (int i = 0; i < BuiltInCount; i++)
        {
            bool changed = previousEnabled is null || previousEnabled[i] != enabled[i];
            if (enabled[i] && changed) _remaining[i] = BuiltInSeconds[i];
        }
        for (int i = 0; i < CustomCount; i++)
        {
            var c = custom[i];
            bool changed = previousCustom is null || previousCustom[i] != (c.Enabled, c.Tick);
            if (c.Enabled && changed) _customRemaining[i] = c.Tick;
        }
    }
}
