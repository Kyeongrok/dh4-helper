using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dh4Launcher.Forms.Services;

namespace Dh4Launcher.Forms.ViewModels;

/// <summary>
/// World.dat 세계지도 편집 탭. 타일을 찍어 육지↔바다로 바꾼다(파나마·수에즈 운하 뚫기 등).
/// </summary>
public partial class WorldMapViewModel : ObservableObject
{
    private readonly IWorldMapService _svc;
    private readonly IGameLauncherService _launcher;

    private byte[]? _data;
    private string? _path;
    private bool _loaded;

    public int Size => _svc.Size;

    /// <summary>지점으로 스크롤 요청 (뷰가 구독). 타일 (x,y).</summary>
    public event Action<int, int>? JumpRequested;

    [ObservableProperty]
    private WriteableBitmap? _bitmap;

    [ObservableProperty]
    private double _zoom = 3;

    /// <summary>0=바다 칠하기, 1=육지 칠하기는 PaintValue로 표현. Mode: 0=칠하기, 2=스포이드.</summary>
    [ObservableProperty]
    private int _mode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PaintValueText))]
    private byte _paintValue;

    /// <summary>브러시 반경(0=1칸, 1=3x3, 2=5x5, 3=7x7).</summary>
    [ObservableProperty]
    private int _brushRadius = 1;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private string _cursorText = string.Empty;

    public string PaintValueText =>
        $"칠할 값: {PaintValue} ({(PaintValue < 32 ? "바다" : "육지/해안")})";

    public WorldMapViewModel(IWorldMapService svc, IGameLauncherService launcher)
    {
        _svc = svc;
        _launcher = launcher;
    }

    public void EnsureLoaded()
    {
        if (_loaded)
            return;
        _loaded = true;

        _path = _svc.FindWorldDat(_launcher.GameDirectory);
        if (_path is null)
        {
            Status = "World.dat를 찾을 수 없습니다. (게임 폴더 지정/실행 필요)";
            return;
        }
        try
        {
            _data = _svc.Load(_path);
            Bitmap = _svc.CreateBitmap(_data);
            Status = $"World.dat 로드됨 ({Size}×{Size}) · 타일을 찍어 편집하세요";
            RestoreCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex)
        {
            Status = $"불러오기 실패: {ex.Message}";
        }
    }

    /// <summary>뷰에서 마우스로 호출 — 타일 칠하기/스포이드.</summary>
    public void PaintAt(int x, int y)
    {
        if (_data is null || Bitmap is null)
            return;
        if (x < 0 || y < 0 || x >= Size || y >= Size)
            return;

        if (Mode == 2) // 스포이드: 값 복사 후 칠하기 모드로 전환
        {
            PaintValue = _data[y * Size + x];
            Mode = 0;
            Status = $"값 복사됨: {PaintValue} — 이제 칠할 수 있습니다";
            return;
        }

        for (int dy = -BrushRadius; dy <= BrushRadius; dy++)
            for (int dx = -BrushRadius; dx <= BrushRadius; dx++)
                _svc.PaintTile(Bitmap, _data, x + dx, y + dy, PaintValue);
    }

    public void HoverAt(int x, int y)
    {
        if (_data is null || x < 0 || y < 0 || x >= Size || y >= Size)
            return;
        CursorText = $"({x}, {y}) = {_data[y * Size + x]}";
    }

    [RelayCommand]
    private void SetModeSea()
    {
        Mode = 0;
        PaintValue = 0;
        Status = "바다(0)로 칠하기 — 육지를 찍으면 항로가 뚫립니다";
    }

    [RelayCommand]
    private void SetModeLand()
    {
        Mode = 0;
        PaintValue = 128;
        Status = "육지(128)로 칠하기";
    }

    [RelayCommand]
    private void SetModeEyedropper()
    {
        Mode = 2;
        Status = "스포이드 — 복사할 바다/육지 타일을 클릭하세요";
    }

    [RelayCommand]
    private void BrushSmall() => BrushRadius = 0;

    [RelayCommand]
    private void BrushMedium() => BrushRadius = 1;

    [RelayCommand]
    private void BrushLarge() => BrushRadius = 3;

    [RelayCommand]
    private void ZoomIn() => Zoom = Math.Min(12, Zoom + 1);

    [RelayCommand]
    private void ZoomOut() => Zoom = Math.Max(1, Zoom - 1);

    [RelayCommand]
    private void JumpSuez() => JumpRequested?.Invoke(420, 820);

    [RelayCommand]
    private void JumpPanama() => JumpRequested?.Invoke(2360, 1080);

    [RelayCommand]
    private void Save()
    {
        if (_data is null || _path is null)
            return;
        try
        {
            _svc.Save(_path, _data);
            RestoreCommand.NotifyCanExecuteChanged();
            Status = "World.dat 저장됨 (원본은 .bak 자동 백업)";
        }
        catch (IOException)
        {
            Status = "파일이 잠겨 있습니다 (게임 종료 후 다시 시도).";
        }
        catch (Exception ex)
        {
            Status = $"저장 실패: {ex.Message}";
        }
    }

    private bool CanRestore() => _path is not null && _svc.HasBackup(_path);

    [RelayCommand(CanExecute = nameof(CanRestore))]
    private void Restore()
    {
        if (_path is null)
            return;
        try
        {
            if (!_svc.Restore(_path))
            {
                Status = "백업이 없습니다 (저장한 적이 없습니다).";
                return;
            }
            _data = _svc.Load(_path);
            Bitmap = _svc.CreateBitmap(_data);
            Status = "원본 World.dat로 되돌림";
        }
        catch (Exception ex)
        {
            Status = $"되돌리기 실패: {ex.Message}";
        }
    }
}
