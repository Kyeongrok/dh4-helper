using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Dh4Launcher.Forms.Services;

/// <summary>
/// World.dat(2500x2500, 1바이트/타일 세계지도)을 읽고 타일 단위로 편집한다.
/// 값 의미(추정): 0=깊은 바다, 1~31=바다/얕은바다, 32 이상=해안선/육지.
/// 운하 뚫기 = 육지/해안 타일을 바다 타일(예: 0)로 바꿔 항로를 잇는 것.
/// </summary>
public class WorldMapService : IWorldMapService
{
    public int Size => 2500;
    private int W => Size;

    // 값 -> 색(0xAARRGGBB). 바다(파랑) / 해안·육지(탄/녹).
    private static readonly int[] Lut = BuildLut();

    private static int[] BuildLut()
    {
        var l = new int[256];
        for (int v = 0; v < 256; v++)
        {
            byte r, g, b;
            if (v == 0) { r = 28; g = 52; b = 104; }            // 깊은 바다
            else if (v < 32) { r = 44; g = 84; b = 140; }       // 바다/얕은바다
            else if (v < 128) { r = 196; g = 188; b = 150; }    // 해안선
            else { r = 120; g = 156; b = 86; }                  // 육지 지형
            l[v] = (0xFF << 24) | (r << 16) | (g << 8) | b;
        }
        return l;
    }

    public string? FindWorldDat(string? gameDirectory)
    {
        if (string.IsNullOrEmpty(gameDirectory))
            return null;
        var p = Path.Combine(gameDirectory, "World.dat");
        return File.Exists(p) ? p : null;
    }

    public byte[] Load(string path)
    {
        var d = File.ReadAllBytes(path);
        if (d.Length != W * W)
            throw new InvalidDataException($"World.dat 크기가 예상과 다릅니다 ({d.Length} != {W * W}).");
        return d;
    }

    public WriteableBitmap CreateBitmap(byte[] data)
    {
        var bmp = new WriteableBitmap(W, W, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null);
        var px = new int[W * W];
        for (int i = 0; i < px.Length; i++)
            px[i] = Lut[data[i]];
        bmp.WritePixels(new Int32Rect(0, 0, W, W), px, W * 4, 0);
        return bmp;
    }

    public void PaintTile(WriteableBitmap bmp, byte[] data, int x, int y, byte value)
    {
        if (x < 0 || y < 0 || x >= W || y >= W)
            return;
        data[y * W + x] = value;
        bmp.WritePixels(new Int32Rect(x, y, 1, 1), new[] { Lut[value] }, 4, 0);
    }

    public void Save(string path, byte[] data)
    {
        var bak = path + ".bak";
        if (!File.Exists(bak))
            File.Copy(path, bak);
        File.WriteAllBytes(path, data);
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
