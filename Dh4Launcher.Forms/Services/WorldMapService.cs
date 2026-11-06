using System.IO;

namespace Dh4Launcher.Forms.Services;

/// <summary>
/// World.dat 세계지도를 읽고 타일 단위로 편집한다.
/// 형식: 2500x1250 칸, 칸당 uint16 LE = Chip.DK4 타일 아틀라스(512x2048, 32x32 타일 16열 x 64행)의 타일 번호.
/// 지형은 대부분 2x2 타일(64px) 블록으로 깔린다: 블록 기준 번호 b → (x짝,y짝)=b, (홀,짝)=b+1, (짝,홀)=b+16, (홀,홀)=b+17.
/// 바다 = 투명 타일 0/1/16/17 (게임이 바다 배경 위에 그림). 운하 뚫기 = 육지 칸을 바다 타일로 바꾸는 것.
/// </summary>
public class WorldMapService : IWorldMapService
{
    public int Width => 2500;
    public int Height => 1250;
    public int TileSrc => 32;
    public int TileCount => 1024;

    private const int AtlasCols = 16;

    public string? FindWorldDat(string? gameDirectory)
    {
        if (string.IsNullOrEmpty(gameDirectory))
            return null;
        var p = Path.Combine(gameDirectory, "World.dat");
        return File.Exists(p) ? p : null;
    }

    public ushort[] Load(string path)
    {
        var d = File.ReadAllBytes(path);
        int n = Width * Height;
        if (d.Length != n * 2)
            throw new InvalidDataException($"World.dat 크기가 예상과 다릅니다 ({d.Length} != {n * 2}).");
        var cells = new ushort[n];
        Buffer.BlockCopy(d, 0, cells, 0, d.Length); // x86/x64 = little-endian
        return cells;
    }

    public int[][] LoadTiles(string? gameDirectory)
    {
        byte[]? atlas = null;
        int aw = 0, ah = 0;
        try
        {
            if (!string.IsNullOrEmpty(gameDirectory))
            {
                var chip = Path.Combine(gameDirectory, "Chip.DK4");
                if (File.Exists(chip))
                    (atlas, aw, ah) = DecodeChipAtlas(File.ReadAllBytes(chip));
            }
        }
        catch { atlas = null; }

        int src = TileSrc;
        int cols = atlas != null ? aw / src : 0;
        int rows = atlas != null ? ah / src : 0;
        var tiles = new int[TileCount][];
        for (int v = 0; v < TileCount; v++)
        {
            var t = new int[src * src];
            if (atlas != null && cols == AtlasCols && v / cols < rows)
            {
                int ax = (v % cols) * src, ay = (v / cols) * src;
                for (int y = 0; y < src; y++)
                {
                    int srcRow = ((ay + y) * aw + ax) * 4;
                    for (int x = 0; x < src; x++)
                    {
                        int si = srcRow + x * 4;
                        // DecodeBc3 = BGRA 순서 → 0xAARRGGBB 패킹
                        t[y * src + x] = (atlas[si + 3] << 24) | (atlas[si + 2] << 16) | (atlas[si + 1] << 8) | atlas[si];
                    }
                }
            }
            else
            {
                // 아틀라스 없으면: 바다 타일은 투명(바다 배경), 나머지는 육지색 단색
                int c = IsSeaTile(v) ? 0 : unchecked((int)0xFF8A8F4A);
                for (int i = 0; i < t.Length; i++) t[i] = c;
            }
            tiles[v] = t;
        }
        return tiles;
    }

    public static bool IsSeaTile(int v) => v is 0 or 1 or 16 or 17;

    /// <summary>Chip.DK4의 tex0(타일 아틀라스, BC3)을 디코드해 (BGRA, w, h) 반환.</summary>
    private static (byte[] bgra, int w, int h) DecodeChipAtlas(byte[] d)
    {
        if (d.Length < 0x14 || d[0] != (byte)'G' || d[1] != (byte)'T' || d[2] != (byte)'1' || d[3] != (byte)'G')
            throw new InvalidDataException("Chip.DK4가 G1T가 아닙니다.");
        uint tableOff = BitConverter.ToUInt32(d, 0x0C);
        uint off0 = BitConverter.ToUInt32(d, (int)tableOff);
        int tb = (int)(tableOff + off0);
        byte dim = d[tb + 2];                 // 하위 니블=width 지수, 상위 니블=height 지수
        int w = 1 << (dim & 0xF);             // 512
        int h = 1 << ((dim >> 4) & 0xF);      // 2048
        int dataOff = tb + 0x14;              // 텍스처 헤더 20바이트
        var block = new byte[w * h];          // BC3 = w*h 바이트
        Array.Copy(d, dataOff, block, 0, block.Length);
        return (Dxt.DecodeBc3(block, w, h), w, h);
    }

    public void Save(string path, ushort[] data)
    {
        var bak = path + ".bak";
        if (!File.Exists(bak))
            File.Copy(path, bak);
        var bytes = new byte[data.Length * 2];
        Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);
        File.WriteAllBytes(path, bytes);
    }

    public bool HasBackup(string path) => File.Exists(path + ".bak");

    public bool Restore(string path)
    {
        var bak = path + ".bak";
        if (!File.Exists(bak))
            return false;
        File.Copy(bak, path, overwrite: true);
        return true;
    }
}
