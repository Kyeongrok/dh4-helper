using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace Dh4Launcher.Forms.Services;

/// <summary>
/// 언어별 DK4HD_xx.exe 를 찾아 실행한다.
/// 게임은 .dk4 데이터 파일을 현재 작업 디렉터리 기준으로 읽으므로
/// 반드시 실행 파일이 있는 폴더를 WorkingDirectory 로 지정해야 한다.
/// </summary>
public class GameLauncherService : IGameLauncherService
{
    private const string OwnKey = @"SOFTWARE\Dh4Launcher";
    private const string GameDirValue = "GameDirectory";

    // Windows '설정 > 디스플레이 > 그래픽'이 사용하는 키. GpuPreference=2 가 '고성능'(외장 GPU).
    private const string GpuPrefKey = @"SOFTWARE\Microsoft\DirectX\UserGpuPreferences";
    private const string HighPerformanceValue = "GpuPreference=2;";

    public string? GameDirectory
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(OwnKey);
            return key?.GetValue(GameDirValue) as string;
        }
        set
        {
            using var key = Registry.CurrentUser.CreateSubKey(OwnKey, writable: true);
            if (string.IsNullOrEmpty(value))
                key.DeleteValue(GameDirValue, throwOnMissingValue: false);
            else
                key.SetValue(GameDirValue, value, RegistryValueKind.String);
        }
    }

    public string GetExeFileName(GameLanguage language) => language switch
    {
        GameLanguage.Japanese => "DK4HD_jp.exe",
        GameLanguage.Korean => "DK4HD_kr.exe",
        GameLanguage.SimplifiedChinese => "DK4HD_sc.exe",
        GameLanguage.TraditionalChinese => "DK4HD_tc.exe",
        _ => "DK4HD_kr.exe",
    };

    public string? FindExecutable(GameLanguage language)
    {
        var fileName = GetExeFileName(language);
        foreach (var dir in CandidateDirectories())
        {
            var path = Path.Combine(dir, fileName);
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    public void Launch(string exePath)
    {
        var dir = Path.GetDirectoryName(exePath)!;
        Process.Start(new ProcessStartInfo
        {
            FileName = exePath,
            WorkingDirectory = dir,
            UseShellExecute = true,
        });

        GameDirectory = dir; // 다음 실행 때 자동으로 찾도록 기억
    }

    public bool IsHighPerformanceGpu(string exePath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(GpuPrefKey);
        return (key?.GetValue(exePath) as string) == HighPerformanceValue;
    }

    public void SetHighPerformanceGpu(string exePath, bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(GpuPrefKey, writable: true);
        if (enabled)
            key.SetValue(exePath, HighPerformanceValue, RegistryValueKind.String);
        else
            key.DeleteValue(exePath, throwOnMissingValue: false);
    }

    private IEnumerable<string> CandidateDirectories()
    {
        if (!string.IsNullOrEmpty(GameDirectory))
            yield return GameDirectory!;

        // 런처가 게임 폴더에 함께 배치된 경우
        yield return AppContext.BaseDirectory;
    }
}
