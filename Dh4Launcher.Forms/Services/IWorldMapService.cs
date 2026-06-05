using System.Windows.Media.Imaging;

namespace Dh4Launcher.Forms.Services;

public interface IWorldMapService
{
    /// <summary>지도 한 변의 타일 수 (World.dat = 2500x2500, 1바이트/타일).</summary>
    int Size { get; }

    /// <summary>게임 폴더의 World.dat 경로. 없으면 null.</summary>
    string? FindWorldDat(string? gameDirectory);

    /// <summary>World.dat 원본 바이트(2500*2500)를 읽는다.</summary>
    byte[] Load(string path);

    /// <summary>타일 데이터로 편집용 비트맵(2500x2500 Bgra32)을 만든다.</summary>
    WriteableBitmap CreateBitmap(byte[] data);

    /// <summary>한 타일 값을 바꾸고 비트맵의 해당 픽셀도 갱신한다.</summary>
    void PaintTile(WriteableBitmap bmp, byte[] data, int x, int y, byte value);

    /// <summary>편집한 데이터를 저장한다(최초 1회 .bak 백업).</summary>
    void Save(string path, byte[] data);

    /// <summary>원본 백업(.bak)이 있는지.</summary>
    bool HasBackup(string path);

    /// <summary>.bak 원본으로 World.dat 전체를 되돌린다. 백업 없으면 false.</summary>
    bool Restore(string path);
}
