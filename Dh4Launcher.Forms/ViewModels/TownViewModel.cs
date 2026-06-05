using Dh4Launcher.Forms.Services;

namespace Dh4Launcher.Forms.ViewModels;

/// <summary>
/// 도시/항구 배경(TownGrp/Plaza/Deck*) 갤러리·교체 VM.
/// 초상화와 같은 G1T 아카이브(비압축 RGBA fmt 0x01 포함)라 PortraitViewModel을 재사용한다.
/// </summary>
public class TownViewModel : PortraitViewModel
{
    public TownViewModel(IPortraitService portraits, IGameLauncherService launcher)
        : base(portraits, launcher)
    {
    }

    protected override IReadOnlyList<PortraitFile> DiscoverFiles()
        => _portraits.FindTownFiles(_launcher.GameDirectory);

    protected override string NotFoundMessage
        => "도시 배경 파일(TownGrp.DK4 등)을 찾을 수 없습니다. (게임 폴더 지정/실행 필요)";
}
