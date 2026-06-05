namespace Dh4Launcher.Forms.Services;

public interface IWorldMapService
{
    /// <summary>지도 한 변의 타일 수 (World.dat = 2500x2500, 1바이트/타일).</summary>
    int Size { get; }

    /// <summary>타일 한 변 픽셀 수(아틀라스 원본 타일 = 64x64).</summary>
    int TileSrc { get; }

    /// <summary>게임 폴더의 World.dat 경로. 없으면 null.</summary>
    string? FindWorldDat(string? gameDirectory);

    /// <summary>World.dat 원본 바이트(2500*2500)를 읽는다.</summary>
    byte[] Load(string path);

    /// <summary>
    /// Chip.DK4 타일 아틀라스를 디코드해 256개 타일 그래픽을 만든다.
    /// 각 타일은 64*64 BGRA(0xAARRGGBB) int 배열. 못 읽으면 평균색 단색 타일로 대체.
    /// </summary>
    int[][] LoadTiles(string? gameDirectory);

    /// <summary>편집한 데이터를 저장한다(최초 1회 .bak 백업).</summary>
    void Save(string path, byte[] data);

    /// <summary>원본 백업(.bak)이 있는지.</summary>
    bool HasBackup(string path);

    /// <summary>.bak 원본으로 World.dat 전체를 되돌린다. 백업 없으면 false.</summary>
    bool Restore(string path);
}
