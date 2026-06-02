namespace Dh4Launcher.Forms.Services;

public interface IGameLauncherService
{
    /// <summary>마지막으로 사용한 게임 폴더(없으면 null). 레지스트리에 보존된다.</summary>
    string? GameDirectory { get; set; }

    /// <summary>해당 언어의 실행 파일 이름(예: DK4HD_kr.exe).</summary>
    string GetExeFileName(GameLanguage language);

    /// <summary>후보 폴더에서 해당 언어 실행 파일을 찾아 전체 경로를 반환. 없으면 null.</summary>
    string? FindExecutable(GameLanguage language);

    /// <summary>지정한 실행 파일을 게임 폴더를 작업 디렉터리로 하여 실행하고 그 폴더를 기억한다.</summary>
    void Launch(string exePath);

    /// <summary>해당 실행 파일이 고성능 GPU로 지정돼 있는지 여부.</summary>
    bool IsHighPerformanceGpu(string exePath);

    /// <summary>해당 실행 파일의 고성능 GPU 지정을 켜거나(true) 해제한다(false).</summary>
    void SetHighPerformanceGpu(string exePath, bool enabled);
}
