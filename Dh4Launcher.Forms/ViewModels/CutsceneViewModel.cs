using Dh4Launcher.Forms.Services;

namespace Dh4Launcher.Forms.ViewModels;

/// <summary>
/// 이벤트 컷신 CG(EventBG1~8 / EventBGEX) 갤러리·교체 VM.
/// 초상화와 동일한 G1T/BC1 포맷이라 PortraitViewModel을 그대로 재사용하고 대상 파일 목록만 바꾼다.
/// </summary>
public class CutsceneViewModel : PortraitViewModel
{
    public CutsceneViewModel(IPortraitService portraits, IGameLauncherService launcher)
        : base(portraits, launcher)
    {
    }

    protected override IReadOnlyList<PortraitFile> DiscoverFiles()
        => _portraits.FindCutsceneFiles(_launcher.GameDirectory);

    protected override string NotFoundMessage
        => "컷신 파일(EventBG1~8.dk4)을 찾을 수 없습니다. (게임 폴더 지정/실행 필요)";
}
