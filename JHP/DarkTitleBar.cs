using System.Runtime.InteropServices;

namespace JHP;

// 시스템 타이틀바를 쓰는 대화상자(사이트 추가창 등)의 제목 표시줄을 다크 테마로 맞춘다 (Windows 10 1809+/11).
// 지원하지 않는 환경에서는 호출이 실패해도 아무 영향 없이 기본 모양을 유지한다.
internal static class DarkTitleBar
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    public static void Apply(IntPtr handle)
    {
        try
        {
            int on = 1;
            DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int));
        }
        catch { }
    }
}
