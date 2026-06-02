using Velopack;
using Velopack.Sources;

namespace Dh4Launcher.Forms.Services;

/// <summary>
/// Velopack 자동업데이트 (방식 A: 조용히 받아두고 종료 시 적용 → 다음 실행 때 새 버전).
/// 설치본(Setup.exe)에서만 동작하며, 포터블/개발빌드/오프라인이면 조용히 건너뛴다.
/// </summary>
public class UpdateService : IUpdateService
{
    private const string RepoUrl = "https://github.com/Kyeongrok/dh4-helper";

    public async Task<string?> CheckAndStageAsync()
    {
        try
        {
            var mgr = new UpdateManager(new GithubSource(RepoUrl, null, false));

            // Velopack로 설치된 경우에만 자동업데이트 가능 (포터블/dev는 제외)
            if (!mgr.IsInstalled)
                return null;

            var info = await mgr.CheckForUpdatesAsync();
            if (info is null)
                return null;

            await mgr.DownloadUpdatesAsync(info);
            // 지금 재시작하지 않고, 앱 종료 시 적용 → 다음 실행 때 새 버전 (조용한 방식 A)
            mgr.WaitExitThenApplyUpdates(info);

            return info.TargetFullRelease.Version.ToString();
        }
        catch
        {
            // 오프라인·릴리즈 없음 등은 무시하고 그냥 현재 버전으로 실행
            return null;
        }
    }
}
