using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dh4Launcher.Forms.Services;

namespace Dh4Launcher.Forms.ViewModels;

/// <summary>
/// World.dat 세계지도 편집 탭. 실제 게임 타일(Chip.DK4)로 보이는 영역만 그려서(뷰포트 렌더링)
/// 게임 안 지도처럼 보이게 하고, 타일을 찍어 육지↔바다로 바꾼다(운하 뚫기 등).
/// </summary>
public partial class WorldMapViewModel : ObservableObject
{
    private readonly IWorldMapService _svc;
    private readonly IGameLauncherService _launcher;

    private byte[]? _data;
    private int[][]? _tiles;   // 256개 타일, 각 64*64 BGRA
    private string? _path;
    private bool _loaded;
    private bool _firstView = true;

    private int _src;          // 타일 원본 px(64)
    private int _vw, _vh;      // 캔버스 px
    private int[]? _buf;       // 캔버스 픽셀 버퍼
    private int _tilePx = 10;  // 줌(타일당 화면 px)
    private int _originX, _originY; // 좌상단 타일
    private double _panAccX, _panAccY;

    public int Size => _svc.Size;

    [ObservableProperty]
    private WriteableBitmap? _bitmap;

    /// <summary>Mode: 0=칠하기(PaintValue), 2=스포이드.</summary>
    [ObservableProperty]
    private int _mode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PaintValueText))]
    private byte _paintValue;

    [ObservableProperty]
    private int _brushRadius = 1;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private string _cursorText = string.Empty;

    public string PaintValueText => $"칠할 값: {PaintValue} ({(PaintValue < 32 ? "바다" : "육지/해안")})";

    public WorldMapViewModel(IWorldMapService svc, IGameLauncherService launcher)
    {
        _svc = svc;
        _launcher = launcher;
        _src = svc.TileSrc;
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
            _tiles = _svc.LoadTiles(_launcher.GameDirectory);
            Status = $"World.dat 로드됨 ({Size}×{Size}) · 타일을 찍어 편집하세요";
            RestoreCommand.NotifyCanExecuteChanged();
            if (_vw > 0 && _firstView) { _firstView = false; CenterOn(1250, 1000); }
            Render();
        }
        catch (Exception ex)
        {
            Status = $"불러오기 실패: {ex.Message}";
        }
    }

    // ===== 뷰포트/렌더 =====

    /// <summary>뷰가 캔버스 크기를 알려준다(SizeChanged).</summary>
    public void SetViewport(int wPx, int hPx)
    {
        if (wPx <= 0 || hPx <= 0)
            return;
        _vw = wPx;
        _vh = hPx;
        _buf = new int[wPx * hPx];
        Bitmap = new WriteableBitmap(wPx, hPx, 96, 96, PixelFormats.Bgra32, null);
        if (_firstView && _data is not null)
        {
            _firstView = false;
            CenterOn(1250, 1000); // 첫 진입 시 지도 중앙
        }
        Render();
    }

    private int TilesAcross => _tilePx > 0 ? _vw / _tilePx + 2 : 0;
    private int TilesDown => _tilePx > 0 ? _vh / _tilePx + 2 : 0;

    private void ClampOrigin()
    {
        _originX = Math.Clamp(_originX, 0, Math.Max(0, Size - Math.Max(1, _vw / Math.Max(1, _tilePx))));
        _originY = Math.Clamp(_originY, 0, Math.Max(0, Size - Math.Max(1, _vh / Math.Max(1, _tilePx))));
    }

    // 타일 투명부 아래로 보일 바다색(게임처럼 바다 위에 지형 타일을 합성).
    private const int SeaBase = unchecked((int)0xFF28465F);
    private const int SeaR = 0x28, SeaG = 0x46, SeaB = 0x5F;

    private void Render()
    {
        if (_data is null || _tiles is null || Bitmap is null || _buf is null)
            return;

        int across = TilesAcross, down = TilesDown, px = _tilePx, src = _src;
        for (int i = 0; i < _buf.Length; i++) _buf[i] = SeaBase;
        for (int ry = 0; ry < down; ry++)
        {
            int ty = _originY + ry;
            if (ty < 0 || ty >= Size) continue;
            int py0 = ry * px;
            int rowBase = ty * Size;
            for (int rx = 0; rx < across; rx++)
            {
                int tx = _originX + rx;
                if (tx < 0 || tx >= Size) continue;
                var tile = _tiles[_data[rowBase + tx]];
                int px0 = rx * px;
                for (int yy = 0; yy < px; yy++)
                {
                    int cy = py0 + yy;
                    if (cy < 0 || cy >= _vh) continue;
                    int srcRow = ((yy * src) / px) * src;
                    int rowD = cy * _vw;
                    for (int xx = 0; xx < px; xx++)
                    {
                        int cx = px0 + xx;
                        if (cx < 0 || cx >= _vw) continue;
                        int t = tile[srcRow + (xx * src) / px];
                        int a = (t >> 24) & 0xFF;
                        if (a == 255) { _buf[rowD + cx] = t; continue; }
                        if (a == 0) continue; // 바다색 유지
                        int ba = 255 - a;
                        int r = (((t >> 16) & 0xFF) * a + SeaR * ba) / 255;
                        int g = (((t >> 8) & 0xFF) * a + SeaG * ba) / 255;
                        int b = ((t & 0xFF) * a + SeaB * ba) / 255;
                        _buf[rowD + cx] = unchecked((int)0xFF000000) | (r << 16) | (g << 8) | b;
                    }
                }
            }
        }
        Bitmap.WritePixels(new Int32Rect(0, 0, _vw, _vh), _buf, _vw * 4, 0);
    }

    private void CenterOn(int tx, int ty)
    {
        _originX = tx - (_vw / Math.Max(1, _tilePx)) / 2;
        _originY = ty - (_vh / Math.Max(1, _tilePx)) / 2;
        ClampOrigin();
    }

    // ===== 입력(뷰에서 호출) =====

    private (int tx, int ty) ScreenToTile(double sx, double sy)
        => (_originX + (int)(sx / _tilePx), _originY + (int)(sy / _tilePx));

    public void PaintAtScreen(double sx, double sy)
    {
        if (_data is null || _tiles is null) return;
        var (tx, ty) = ScreenToTile(sx, sy);
        if (tx < 0 || ty < 0 || tx >= Size || ty >= Size) return;

        if (Mode == 2)
        {
            PaintValue = _data[ty * Size + tx];
            Mode = 0;
            Status = $"값 복사됨: {PaintValue} — 이제 칠할 수 있습니다";
            return;
        }
        for (int dy = -BrushRadius; dy <= BrushRadius; dy++)
            for (int dx = -BrushRadius; dx <= BrushRadius; dx++)
            {
                int x = tx + dx, y = ty + dy;
                if (x >= 0 && y >= 0 && x < Size && y < Size)
                    _data[y * Size + x] = PaintValue;
            }
        Render();
    }

    public void HoverAtScreen(double sx, double sy)
    {
        if (_data is null) return;
        var (tx, ty) = ScreenToTile(sx, sy);
        if (tx < 0 || ty < 0 || tx >= Size || ty >= Size) return;
        CursorText = $"({tx}, {ty}) = {_data[ty * Size + tx]}";
    }

    public void PanByPixels(double dxPx, double dyPx)
    {
        _panAccX -= dxPx;
        _panAccY -= dyPx;
        int tdx = (int)(_panAccX / _tilePx), tdy = (int)(_panAccY / _tilePx);
        if (tdx == 0 && tdy == 0) return;
        _originX += tdx; _originY += tdy;
        _panAccX -= tdx * _tilePx; _panAccY -= tdy * _tilePx;
        ClampOrigin(); Render();
    }

    public void WheelPan(int delta, bool horizontal)
    {
        int step = Math.Max(1, 60 / _tilePx) * (delta > 0 ? -1 : 1);
        if (horizontal) _originX += step; else _originY += step;
        ClampOrigin(); Render();
    }

    public void ZoomAtCursor(int delta, double sx, double sy)
    {
        int newPx = Math.Clamp(_tilePx + (delta > 0 ? 2 : -2), 2, 48);
        if (newPx == _tilePx) return;
        double tileX = _originX + sx / _tilePx, tileY = _originY + sy / _tilePx;
        _tilePx = newPx;
        _originX = (int)Math.Round(tileX - sx / _tilePx);
        _originY = (int)Math.Round(tileY - sy / _tilePx);
        ClampOrigin(); Render();
    }

    // ===== 커맨드 =====

    [RelayCommand]
    private void SetModeSea() { Mode = 0; PaintValue = 0; Status = "바다(0)로 칠하기 — 육지를 찍으면 항로가 뚫립니다"; }

    [RelayCommand]
    private void SetModeLand() { Mode = 0; PaintValue = 128; Status = "육지(128)로 칠하기"; }

    [RelayCommand]
    private void SetModeEyedropper() { Mode = 2; Status = "스포이드 — 복사할 바다/육지 타일을 클릭하세요"; }

    [RelayCommand]
    private void BrushSmall() => BrushRadius = 0;

    [RelayCommand]
    private void BrushMedium() => BrushRadius = 1;

    [RelayCommand]
    private void BrushLarge() => BrushRadius = 3;

    [RelayCommand]
    private void ZoomIn() => ZoomAtCursor(1, _vw / 2.0, _vh / 2.0);

    [RelayCommand]
    private void ZoomOut() => ZoomAtCursor(-1, _vw / 2.0, _vh / 2.0);

    [RelayCommand]
    private void JumpSuez() { CenterOn(420, 820); Render(); }

    [RelayCommand]
    private void JumpPanama() { CenterOn(2360, 1080); Render(); }

    [RelayCommand]
    private void Save()
    {
        if (_data is null || _path is null) return;
        try
        {
            _svc.Save(_path, _data);
            RestoreCommand.NotifyCanExecuteChanged();
            Status = "World.dat 저장됨 (원본은 .bak 자동 백업)";
        }
        catch (IOException) { Status = "파일이 잠겨 있습니다 (게임 종료 후 다시 시도)."; }
        catch (Exception ex) { Status = $"저장 실패: {ex.Message}"; }
    }

    [RelayCommand]
    private void Reset()
    {
        if (_path is null || _data is null) return;
        try
        {
            _data = _svc.Load(_path);
            Render();
            Status = "편집 취소 — 현재 저장 상태로 초기화";
        }
        catch (Exception ex) { Status = $"초기화 실패: {ex.Message}"; }
    }

    private bool CanRestore() => _path is not null && _svc.HasBackup(_path);

    [RelayCommand(CanExecute = nameof(CanRestore))]
    private void Restore()
    {
        if (_path is null) return;
        try
        {
            if (!_svc.Restore(_path)) { Status = "백업이 없습니다 (저장한 적이 없습니다)."; return; }
            _data = _svc.Load(_path);
            Render();
            Status = "원본 World.dat로 되돌림";
        }
        catch (Exception ex) { Status = $"되돌리기 실패: {ex.Message}"; }
    }
}
