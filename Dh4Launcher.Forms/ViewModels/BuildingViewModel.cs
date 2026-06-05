using Dh4Launcher.Forms.Services;

namespace Dh4Launcher.Forms.ViewModels;

/// <summary>
/// 건물/상점(Plaza.dk4) 갤러리·교체 VM. 초상화와 같은 G1T(비압축 RGBA 포함)라 PortraitViewModel 재사용.
/// </summary>
public class BuildingViewModel : PortraitViewModel
{
    public BuildingViewModel(IPortraitService portraits, IGameLauncherService launcher)
        : base(portraits, launcher)
    {
    }

    protected override IReadOnlyList<PortraitFile> DiscoverFiles()
        => _portraits.FindBuildingFiles(_launcher.GameDirectory);

    protected override string NotFoundMessage
        => "건물 파일(Plaza.dk4)을 찾을 수 없습니다. (게임 폴더 지정/실행 필요)";
}
