using JHP.Api;
using Xunit;

namespace JHP.Tests;

public class AlarmSchedulerTests
{
    private static bool[] Enabled(params int[] on)
    {
        var a = new bool[AlarmScheduler.BuiltInCount];
        foreach (int i in on) a[i] = true;
        return a;
    }

    private static CustomAlarm[] NoCustom() =>
        [new CustomAlarm(), new CustomAlarm(), new CustomAlarm()];

    [Fact]
    public void ResetAll_SetsBuiltInAndCustomSeconds()
    {
        var s = new AlarmScheduler();
        var custom = NoCustom();
        custom[1].Tick = 42;
        s.ResetAll(custom);
        Assert.Equal(7200, s.GetRemaining(0));
        Assert.Equal(55, s.GetRemaining(7));
        Assert.Equal(42, s.GetCustomRemaining(1));
    }

    [Fact]
    public void Advance_FiresAtExactBoundaryAndReloads()
    {
        var s = new AlarmScheduler();
        var custom = NoCustom();
        s.ResetAll(custom);
        var en = Enabled(7);                                  // 파운틴(55초)
        Assert.Null(s.Advance(54, en, custom));
        Assert.Equal(1, s.GetRemaining(7));
        Assert.Equal("파운틴", s.Advance(1, en, custom));
        Assert.Equal(55, s.GetRemaining(7));
    }

    [Fact]
    public void Advance_DisabledAlarmsDoNotFire()
    {
        var s = new AlarmScheduler();
        var custom = NoCustom();
        s.ResetAll(custom);
        Assert.Null(s.Advance(7200, Enabled(), custom));
    }

    [Fact]
    public void Advance_OverlappingAlarmsFireOnlyTopOne_ButAllReload()
    {
        var s = new AlarmScheduler();
        var custom = NoCustom();
        s.ResetAll(custom);
        var en = Enabled(0, 1, 2);                            // 7200 / 3600 / 1800 은 7200초에 모두 겹침
        Assert.Null(s.Advance(1799, en, custom));
        Assert.Equal("경뿌/경쿠/유니온", s.Advance(1, en, custom));   // 1800초: 가장 위쪽이 아니라 도달한 것만
        string? atTop = null;
        for (int t = 1801; t <= 7200; t++)                    // 매초 한 번씩(정상 타이머 동작)
        {
            string? fired = s.Advance(1, en, custom);
            if (t == 7200) atTop = fired;
        }
        Assert.Equal("재획비", atTop);                         // 7200초: 셋이 겹치면 목록 위쪽(재획비)
        Assert.Equal(7200, s.GetRemaining(0));
        Assert.Equal(3600, s.GetRemaining(1));
        Assert.Equal(1800, s.GetRemaining(2));
    }

    [Fact]
    public void Advance_CustomAlarmFiresWithNameOrDefault()
    {
        var s = new AlarmScheduler();
        var custom = NoCustom();
        custom[0].Enabled = true; custom[0].Tick = 3; custom[0].Name = "물약";
        custom[2].Enabled = true; custom[2].Tick = 5; custom[2].Name = "   ";
        s.ResetAll(custom);
        Assert.Equal("물약", s.Advance(3, Enabled(), custom));
        Assert.Equal(AlarmScheduler.DefaultCustomName, s.Advance(2, Enabled(), custom));
    }

    [Fact]
    public void Advance_CustomAlarmWithNonPositiveTickIsIgnored()
    {
        var s = new AlarmScheduler();
        var custom = NoCustom();
        custom[0].Enabled = true; custom[0].Tick = 0;
        s.ResetAll(custom);
        Assert.Null(s.Advance(10, Enabled(), custom));
        Assert.Null(s.MinRemaining(Enabled(), custom));
    }

    [Fact]
    public void MinRemaining_PicksNearestAcrossBuiltInAndCustom()
    {
        var s = new AlarmScheduler();
        var custom = NoCustom();
        custom[1].Enabled = true; custom[1].Tick = 30;
        s.ResetAll(custom);
        Assert.Equal(30, s.MinRemaining(Enabled(7), custom));
        s.Advance(20, Enabled(7), custom);
        Assert.Equal(10, s.MinRemaining(Enabled(7), custom));
        Assert.Null(s.MinRemaining(Enabled(), NoCustom()));
    }

    [Fact]
    public void ApplyChanges_ResetsOnlyChangedEnabledItems()
    {
        var s = new AlarmScheduler();
        var custom = NoCustom();
        s.ResetAll(custom);
        s.Advance(10, Enabled(6, 7), custom);                 // 100초·55초 알람이 10초 진행
        var before = Enabled(6, 7);
        var after = Enabled(6, 7, 5);                         // 600초 알람을 새로 켬
        s.ApplyChanges(before, [(false, 1), (false, 1), (false, 1)], after, custom);
        Assert.Equal(100 - 10, s.GetRemaining(6));            // 그대로 유지
        Assert.Equal(55 - 10, s.GetRemaining(7));
        Assert.Equal(600, s.GetRemaining(5));                 // 새로 켠 것만 리셋
    }

    [Fact]
    public void ApplyChanges_WithoutSnapshotResetsAllEnabled()
    {
        var s = new AlarmScheduler();
        var custom = NoCustom();
        custom[0].Enabled = true; custom[0].Tick = 20;
        s.ResetAll(custom);
        s.Advance(5, Enabled(7), custom);
        s.ApplyChanges(null, null, Enabled(7), custom);
        Assert.Equal(55, s.GetRemaining(7));
        Assert.Equal(20, s.GetCustomRemaining(0));
    }

    [Fact]
    public void ApplyChanges_CustomTickChangeResetsThatAlarm()
    {
        var s = new AlarmScheduler();
        var custom = NoCustom();
        custom[0].Enabled = true; custom[0].Tick = 20;
        s.ResetAll(custom);
        s.Advance(5, Enabled(), custom);
        var snap = new (bool, int)[] { (true, 20), (false, 1), (false, 1) };
        custom[0].Tick = 60;
        s.ApplyChanges(Enabled(), snap, Enabled(), custom);
        Assert.Equal(60, s.GetCustomRemaining(0));
    }
}
