namespace Dh4Launcher.Forms.Services;

public interface IUpdateService
{
    /// <summary>
    /// GitHub 릴리즈에서 새 버전을 확인하고, 있으면 조용히 받아 다음 실행 때 적용되도록 예약한다.
    /// 새 버전을 받았으면 그 버전 문자열을, 아니면(최신/포터블/오프라인) null을 반환.
    /// </summary>
    Task<string?> CheckAndStageAsync();
}
