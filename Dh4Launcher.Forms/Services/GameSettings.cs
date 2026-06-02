namespace Dh4Launcher.Forms.Services;

/// <summary>
/// 대항해시대 IV HD 게임이 레지스트리
/// (HKCU\SOFTWARE\KOEITECMO\DK4HD\System Config)에서 읽어가는 화면 설정 값.
/// </summary>
public class GameSettings
{
    public int ScreenWidth { get; set; } = 1920;
    public int ScreenHeight { get; set; } = 1080;

    /// <summary>0 = 창 모드, 1 = 전체 화면.</summary>
    public int ScreenMode { get; set; }
}
