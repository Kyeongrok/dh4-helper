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

    // 값(타일 번호) -> 색(0xAARRGGBB). Chip.DK4 타일 아틀라스(64x64 타일 256개)의 타일별 평균색.
    // 실제 게임 지형 타일 색이라 줄무늬 없이 진짜 세계지도처럼 보인다. 195~255는 미사용 타일(흰색).
    private static readonly int[] Lut =
    {
        // 0~31: 바다 계열 — 깔끔하게 단일 청록으로 통일(여러 바다 타일의 점박이 제거)
        unchecked((int)0xFF28465F), unchecked((int)0xFF28465F), unchecked((int)0xFF28465F), unchecked((int)0xFF28465F), unchecked((int)0xFF28465F), unchecked((int)0xFF28465F), unchecked((int)0xFF28465F), unchecked((int)0xFF28465F), unchecked((int)0xFF28465F), unchecked((int)0xFF28465F), unchecked((int)0xFF28465F), unchecked((int)0xFF28465F), unchecked((int)0xFF28465F), unchecked((int)0xFF28465F), unchecked((int)0xFF28465F), unchecked((int)0xFF28465F),
        unchecked((int)0xFF28465F), unchecked((int)0xFF28465F), unchecked((int)0xFF28465F), unchecked((int)0xFF28465F), unchecked((int)0xFF28465F), unchecked((int)0xFF28465F), unchecked((int)0xFF28465F), unchecked((int)0xFF28465F), unchecked((int)0xFF28465F), unchecked((int)0xFF28465F), unchecked((int)0xFF28465F), unchecked((int)0xFF28465F), unchecked((int)0xFF28465F), unchecked((int)0xFF28465F), unchecked((int)0xFF28465F), unchecked((int)0xFF28465F),
        unchecked((int)0xFF4D5E4A), unchecked((int)0xFF4A5B3C), unchecked((int)0xFF4C5A3D), unchecked((int)0xFF495B48), unchecked((int)0xFF465A45), unchecked((int)0xFF4D5D3A), unchecked((int)0xFF5B6850), unchecked((int)0xFF738582), unchecked((int)0xFF5E7281), unchecked((int)0xFF617482), unchecked((int)0xFF5A6E7D), unchecked((int)0xFF5E7280), unchecked((int)0xFF596D7B), unchecked((int)0xFF5E7281), unchecked((int)0xFF576C7C), unchecked((int)0xFF627684),
        unchecked((int)0xFF8998A3), unchecked((int)0xFF96A4AF), unchecked((int)0xFF909F8F), unchecked((int)0xFF819567), unchecked((int)0xFF73885F), unchecked((int)0xFF83946E), unchecked((int)0xFF91A094), unchecked((int)0xFF6A879D), unchecked((int)0xFF738C31), unchecked((int)0xFF889140), unchecked((int)0xFF859834), unchecked((int)0xFF899739), unchecked((int)0xFF8A973A), unchecked((int)0xFF81982E), unchecked((int)0xFFA4A869), unchecked((int)0xFFCBCDB5),
        unchecked((int)0xFF889141), unchecked((int)0xFF929447), unchecked((int)0xFF7B8F2E), unchecked((int)0xFF839534), unchecked((int)0xFF9AA258), unchecked((int)0xFFCACCB4), unchecked((int)0xFFACAB76), unchecked((int)0xFFB6B78B), unchecked((int)0xFF677D28), unchecked((int)0xFF657C2A), unchecked((int)0xFF708B25), unchecked((int)0xFF688126), unchecked((int)0xFF6F8B25), unchecked((int)0xFF8E9A5F), unchecked((int)0xFFB9BB93), unchecked((int)0xFFC3C5A6),
        unchecked((int)0xFF678126), unchecked((int)0xFF647A28), unchecked((int)0xFF7C833D), unchecked((int)0xFF6B7734), unchecked((int)0xFF708030), unchecked((int)0xFF929F5F), unchecked((int)0xFFA2A86D), unchecked((int)0xFF9AA468), unchecked((int)0xFF717B37), unchecked((int)0xFF717B37), unchecked((int)0xFF8C8E45), unchecked((int)0xFF81863F), unchecked((int)0xFF6F8030), unchecked((int)0xFF6C7A39), unchecked((int)0xFF747D42), unchecked((int)0xFF75823E),
        unchecked((int)0xFFBAB470), unchecked((int)0xFFBBB572), unchecked((int)0xFF717B37), unchecked((int)0xFFA3A25F), unchecked((int)0xFFB1AC67), unchecked((int)0xFF758639), unchecked((int)0xFF74833A), unchecked((int)0xFF77873A), unchecked((int)0xFFBAB472), unchecked((int)0xFFBBB572), unchecked((int)0xFFB4AF6A), unchecked((int)0xFFB1AD68), unchecked((int)0xFFB1AC67), unchecked((int)0xFF77873A), unchecked((int)0xFF72754C), unchecked((int)0xFF72754C),
        unchecked((int)0xFFAEA964), unchecked((int)0xFFAFAA65), unchecked((int)0xFFAEBBC0), unchecked((int)0xFFC2C3A5), unchecked((int)0xFF74803E), unchecked((int)0xFF758041), unchecked((int)0xFF72754C), unchecked((int)0xFF72754C), unchecked((int)0xFFB1BEC3), unchecked((int)0xFFB7C2C7), unchecked((int)0xFFA2B1B7), unchecked((int)0xFF9DA89A), unchecked((int)0xFF737F3F), unchecked((int)0xFF748040), unchecked((int)0xFF636E3B), unchecked((int)0xFF636E3A),
        unchecked((int)0xFFA8B6BB), unchecked((int)0xFFA5B4B9), unchecked((int)0xFF8B9EA4), unchecked((int)0xFF8B9EA4), unchecked((int)0xFFB6C2C7), unchecked((int)0xFF626D39), unchecked((int)0xFF676F3F), unchecked((int)0xFF687141), unchecked((int)0xFFA7B5BA), unchecked((int)0xFFA6B4BA), unchecked((int)0xFF8C9683), unchecked((int)0xFF8C9783), unchecked((int)0xFFB7C3C8), unchecked((int)0xFF646E3B), unchecked((int)0xFF676F3F), unchecked((int)0xFF687041),
        unchecked((int)0xFF868B7E), unchecked((int)0xFF868B7E), unchecked((int)0xFF8F9061), unchecked((int)0xFF8E9067), unchecked((int)0xFF666F3D), unchecked((int)0xFF536744), unchecked((int)0xFF476455), unchecked((int)0xFF5E7A54), unchecked((int)0xFF868B7E), unchecked((int)0xFF868B7E), unchecked((int)0xFF8F9062), unchecked((int)0xFF8A9057), unchecked((int)0xFF6D7D38), unchecked((int)0xFF576E46), unchecked((int)0xFF536E48), unchecked((int)0xFF557856),
        unchecked((int)0xFF8C8E68), unchecked((int)0xFF8B8F6B), unchecked((int)0xFF8D8F69), unchecked((int)0xFF889051), unchecked((int)0xFF748A4D), unchecked((int)0xFF51735C), unchecked((int)0xFF7A9AAA), unchecked((int)0xFF507256), unchecked((int)0xFF8B8D6A), unchecked((int)0xFF8B8E6C), unchecked((int)0xFF667C5D), unchecked((int)0xFF7E8D65), unchecked((int)0xFF618971), unchecked((int)0xFF547B5B), unchecked((int)0xFF527053), unchecked((int)0xFF587653),
        unchecked((int)0xFF577364), unchecked((int)0xFF84895A), unchecked((int)0xFF758460), unchecked((int)0xFF809780), unchecked((int)0xFFA4B9B2), unchecked((int)0xFF97B3B2), unchecked((int)0xFFB0C4D1), unchecked((int)0xFF95B2B0), unchecked((int)0xFF848B5C), unchecked((int)0xFF7F8A61), unchecked((int)0xFF808D62), unchecked((int)0xFFA1B1AE), unchecked((int)0xFFB4C7D3), unchecked((int)0xFF9DB9C9), unchecked((int)0xFF7094A5), unchecked((int)0xFFA5BFCE),
        unchecked((int)0xFF738A70), unchecked((int)0xFF5D735F), unchecked((int)0xFF798D6C), unchecked((int)0xFF55735D), unchecked((int)0xFF6A8369), unchecked((int)0xFF8CABB0), unchecked((int)0xFFA3BCCB), unchecked((int)0xFF90A8B6), unchecked((int)0xFF7E8E66), unchecked((int)0xFF788868), unchecked((int)0xFF7E8C62), unchecked((int)0xFF6F9149), unchecked((int)0xFF708461), unchecked((int)0xFF839585), unchecked((int)0xFFBFC8B8), unchecked((int)0xFFC3CBBE),
        unchecked((int)0xFF788D4C), unchecked((int)0xFF6E8D49), unchecked((int)0xFF6D8E4C), unchecked((int)0xFF607674), unchecked((int)0xFF85938D), unchecked((int)0xFFA1AE90), unchecked((int)0xFF8F9E79), unchecked((int)0xFFC1CABB), unchecked((int)0xFF668C5C), unchecked((int)0xFF708D64), unchecked((int)0xFF698F57), unchecked((int)0xFF5D6E7B), unchecked((int)0xFF5D6E7B), unchecked((int)0xFF96A2A5), unchecked((int)0xFFB9C3B1), unchecked((int)0xFFC2CABC),
        unchecked((int)0xFF729056), unchecked((int)0xFF698B53), unchecked((int)0xFF738E53), unchecked((int)0xFF2C343A), unchecked((int)0xFF2C343A), unchecked((int)0xFF4C5254), unchecked((int)0xFF676A66), unchecked((int)0xFF2C343A), unchecked((int)0xFFFFFFFF), unchecked((int)0xFFFFFFFF), unchecked((int)0xFFFFFFFF), unchecked((int)0xFFFFFFFF), unchecked((int)0xFFFFFFFF), unchecked((int)0xFFFFFFFF), unchecked((int)0xFFFFFFFF), unchecked((int)0xFFFFFFFF),
        unchecked((int)0xFFFFFFFF), unchecked((int)0xFFFFFFFF), unchecked((int)0xFFFFFFFF), unchecked((int)0xFFFFFFFF), unchecked((int)0xFFFFFFFF), unchecked((int)0xFFFFFFFF), unchecked((int)0xFFFFFFFF), unchecked((int)0xFFFFFFFF), unchecked((int)0xFFFFFFFF), unchecked((int)0xFFFFFFFF), unchecked((int)0xFFFFFFFF), unchecked((int)0xFFFFFFFF), unchecked((int)0xFFFFFFFF), unchecked((int)0xFFFFFFFF), unchecked((int)0xFFFFFFFF), unchecked((int)0xFFFFFFFF),
    };

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
