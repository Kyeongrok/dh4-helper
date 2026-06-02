using Microsoft.Win32;

namespace Dh4Launcher.Forms.Services;

/// <summary>
/// 원본 DK4HDLauncher 와 동일하게
/// HKEY_CURRENT_USER\SOFTWARE\KOEITECMO\DK4HD\System Config 의
/// SCREEN_W / SCREEN_H / SCREEN_MODE (모두 REG_DWORD) 를 읽고 쓴다.
/// </summary>
public class GameSettingsService : IGameSettingsService
{
    private const string SubKey = @"SOFTWARE\KOEITECMO\DK4HD\System Config";

    public GameSettings Load()
    {
        using var key = Registry.CurrentUser.OpenSubKey(SubKey);
        if (key is null)
            return new GameSettings();

        int Get(string name, int fallback)
            => key.GetValue(name) is int v ? v : fallback;

        return new GameSettings
        {
            ScreenWidth = Get("SCREEN_W", 1920),
            ScreenHeight = Get("SCREEN_H", 1080),
            ScreenMode = Get("SCREEN_MODE", 0),
        };
    }

    public void Save(GameSettings settings)
    {
        // CreateSubKey 는 기존 키를 비우지 않으므로 LANG_MODE/BGM_MODE 등 다른 값은 유지된다.
        using var key = Registry.CurrentUser.CreateSubKey(SubKey, writable: true);
        key.SetValue("SCREEN_W", settings.ScreenWidth, RegistryValueKind.DWord);
        key.SetValue("SCREEN_H", settings.ScreenHeight, RegistryValueKind.DWord);
        key.SetValue("SCREEN_MODE", settings.ScreenMode, RegistryValueKind.DWord);
    }
}
