namespace Dh4Launcher.Forms.Services;

public interface IWorldMapService
{
    /// <summary>지도 가로 칸 수 (World.dat = 2500x1250 칸, 칸당 uint16 LE 타일 번호).</summary>
    int Width { get; }

    /// <summary>지도 세로 칸 수.</summary>
    int Height { get; }

    /// <summary>타일 한 변 픽셀 수(Chip.DK4 아틀라스 원본 타일 = 32x32).</summary>
    int TileSrc { get; }

    /// <summary>타일 개수(아틀라스 16열 x 64행 = 1024).</summary>
    int TileCount { get; }

    /// <summary>게임 폴더의 World.dat 경로. 없으면 null.</summary>
    string? FindWorldDat(string? gameDirectory);

    /// <summary>World.dat를 칸 단위 타일 번호 배열(Width*Height)로 읽는다.</summary>
    ushort[] Load(string path);

    /// <summary>
    /// Chip.DK4 타일 아틀라스를 디코드해 TileCount개 타일 그래픽을 만든다.
    /// 각 타일은 32*32 BGRA(0xAARRGGBB) int 배열. 못 읽으면 단색 타일로 대체.
    /// </summary>
    int[][] LoadTiles(string? gameDirectory);

    /// <summary>편집한 데이터를 저장한다(최초 1회 .bak 백업).</summary>
    void Save(string path, ushort[] data);

    /// <summary>원본 백업(.bak)이 있는지.</summary>
    bool HasBackup(string path);

    /// <summary>.bak 원본으로 World.dat 전체를 되돌린다. 백업 없으면 false.</summary>
    bool Restore(string path);
}
