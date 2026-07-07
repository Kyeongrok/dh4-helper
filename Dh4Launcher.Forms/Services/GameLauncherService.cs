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

    // '높은 DPI 배율 재정의' AppCompat 키. HIGHDPIAWARE = 재정의를 '응용 프로그램'으로 지정.
    private const string LayersKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";
    private const string HighDpiToken = "HIGHDPIAWARE";

    // 서로 충돌하는 DPI 관련 토큰들. 우리 토큰을 넣기 전에 걷어낸다.
    private static readonly string[] DpiTokens =
        ["HIGHDPIAWARE", "DPIUNAWARE", "GDIDPISCALING",
         "PERPROCESSSYSTEMDPIFORCEON", "PERPROCESSSYSTEMDPIFORCEOFF"];

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
        // 고배율(4K 175% 등) 모니터에서 창이 축소되어 뜨는 것을 막는다.
        // DK4HD 는 DPI 미인식 게임이라, Windows 가 화면을 가상화하지 않도록
        // '높은 DPI 배율 재정의 = 응용 프로그램'을 걸어 실제 해상도를 그대로 넘겨준다.
        EnsureHighDpiOverride(exePath);

        var dir = Path.GetDirectoryName(exePath)!;
        Process.Start(new ProcessStartInfo
        {
            FileName = exePath,
            WorkingDirectory = dir,
            UseShellExecute = true,
        });

        GameDirectory = dir; // 다음 실행 때 자동으로 찾도록 기억
    }

    /// <summary>해당 exe 에 HIGHDPIAWARE 오버라이드가 없으면 추가한다(다른 호환성 토큰은 보존).</summary>
    private static void EnsureHighDpiOverride(string exePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(LayersKey, writable: true);

        // 기존 값에서 '~' 접두사와 충돌하는 DPI 토큰을 제거하고 HIGHDPIAWARE 를 넣는다.
        var existing = (key.GetValue(exePath) as string)?.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            ?? [];
        var tokens = existing
            .Where(t => t != "~" && !DpiTokens.Contains(t, StringComparer.OrdinalIgnoreCase))
            .Prepend(HighDpiToken);

        key.SetValue(exePath, "~ " + string.Join(' ', tokens), RegistryValueKind.String);
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
