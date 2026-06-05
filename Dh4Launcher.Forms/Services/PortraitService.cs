using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Dh4Launcher.Forms.Services;

/// <summary>
/// Portrait.dk4 (KOEI Tecmo G1T 텍스처 아카이브, BC3/DXT5)에서 초상화를 읽고 교체한다.
/// 텍스처 헤더는 28바이트 고정, 데이터는 BC3 (w*h 바이트), 같은 크기로 in-place 교체.
/// </summary>
public class PortraitService : IPortraitService
{
    private const int TexHeaderSize = 0x1C; // 28

    public string? FindPortraitFile(string? gameDirectory)
        => FindFiles(gameDirectory).FirstOrDefault()?.Path;

    public IReadOnlyList<PortraitFile> FindFiles(string? gameDirectory)
    {
        var list = new List<PortraitFile>();
        if (string.IsNullOrEmpty(gameDirectory))
            return list;
        void Add(string file, string display)
        {
            var p = Path.Combine(gameDirectory, file);
            if (File.Exists(p)) list.Add(new PortraitFile(display, p));
        }
        Add("bustup.dk4", "얼굴/흉상 (bustup)");
        Add("Portrait.dk4", "대형 초상화 (Portrait)");
        return list;
    }

    public IReadOnlyList<PortraitFile> FindCutsceneFiles(string? gameDirectory)
    {
        var list = new List<PortraitFile>();
        if (string.IsNullOrEmpty(gameDirectory))
            return list;
        void Add(string file, string display)
        {
            var p = Path.Combine(gameDirectory, file);
            if (File.Exists(p)) list.Add(new PortraitFile(display, p));
        }
        for (int i = 1; i <= 8; i++)
            Add($"EventBG{i}.dk4", $"이벤트 CG {i} (EventBG{i})");
        Add("EventBGEX.dk4", "이벤트 CG 추가 (EventBGEX)");
        return list;
    }

    public IReadOnlyList<PortraitFile> FindTownFiles(string? gameDirectory)
    {
        var list = new List<PortraitFile>();
        if (string.IsNullOrEmpty(gameDirectory))
            return list;
        void Add(string file, string display)
        {
            var p = Path.Combine(gameDirectory, file);
            if (File.Exists(p)) list.Add(new PortraitFile(display, p));
        }
        Add("TownGrp.DK4", "도시/항구 배경 (TownGrp)");
        Add("Plaza.dk4", "항구 광장 (Plaza)");
        Add("DeckScrn.dk4", "갑판 화면 (DeckScrn)");
        Add("DeckShip.dk4", "갑판 배 (DeckShip)");
        return list;
    }

    // 포맷 코드: 0x01 = 비압축 32비트 RGBA, 0x59 = BC1(DXT1, 4bpp), 0x5B = BC3(DXT5, 8bpp)
    private record TexInfo(int Index, long DataOffset, int Width, int Height, byte Format, int DataLen);

    private static List<TexInfo> Parse(byte[] d)
    {
        if (d.Length < 0x14 || d[0] != (byte)'G' || d[1] != (byte)'T' || d[2] != (byte)'1' || d[3] != (byte)'G')
            throw new InvalidDataException("G1T 파일이 아닙니다.");
        uint tableOff = BitConverter.ToUInt32(d, 0x0C);
        uint numTex = BitConverter.ToUInt32(d, 0x10);
        var list = new List<TexInfo>((int)numTex);
        for (int i = 0; i < numTex; i++)
        {
            uint off = BitConverter.ToUInt32(d, (int)tableOff + 4 * i);
            long texBase = tableOff + off;
            byte fmt = d[texBase + 1];
            int w = (int)BitConverter.ToUInt32(d, (int)texBase + 0x14);
            int h = (int)BitConverter.ToUInt32(d, (int)texBase + 0x18);
            int dataLen = fmt switch
            {
                0x01 => w * h * 4, // 비압축 RGBA
                0x59 => w * h / 2, // BC1
                _ => w * h,        // BC3
            };
            list.Add(new TexInfo(i, texBase + TexHeaderSize, w, h, fmt, dataLen));
        }
        return list;
    }

    private static PortraitItem MakeItem(byte[] d, TexInfo t, int thumbWidth)
    {
        var full = DecodeToBitmap(d, t);
        double s = thumbWidth / (double)t.Width;
        var scaled = new TransformedBitmap(full, new ScaleTransform(s, s));
        // 픽셀을 복사해 독립 비트맵으로 굽는다(원본 풀해상도 디코드는 즉시 GC 가능 — 1080p 컷신 50장 메모리 방지).
        var thumb = new WriteableBitmap(scaled);
        thumb.Freeze();
        return new PortraitItem(t.Index, t.Width, t.Height, thumb);
    }

    public IReadOnlyList<PortraitItem> Load(string portraitPath, int thumbWidth = 110)
    {
        var d = File.ReadAllBytes(portraitPath);
        var infos = Parse(d);
        var items = new List<PortraitItem>(infos.Count);
        foreach (var t in infos)
            items.Add(MakeItem(d, t, thumbWidth));
        return items;
    }

    public PortraitItem LoadOne(string portraitPath, int index, int thumbWidth = 110)
    {
        var d = File.ReadAllBytes(portraitPath);
        var t = Parse(d)[index];
        return MakeItem(d, t, thumbWidth);
    }

    public bool HasBackup(string portraitPath) => File.Exists(portraitPath + ".bak");

    public bool Restore(string portraitPath, int index)
    {
        var bak = portraitPath + ".bak";
        if (!File.Exists(bak))
            return false;

        var orig = File.ReadAllBytes(bak);
        var live = File.ReadAllBytes(portraitPath);
        var to = Parse(orig)[index];
        var tl = Parse(live)[index];
        if (to.DataLen != tl.DataLen)
            throw new InvalidOperationException("백업과 텍스처 크기가 다릅니다.");

        // 원본 바이트를 그대로 복사 → 재인코딩 손실 없는 정확 복원.
        Array.Copy(orig, to.DataOffset, live, tl.DataOffset, tl.DataLen);
        File.WriteAllBytes(portraitPath, live);
        return true;
    }

    public BitmapSource DecodeFull(string portraitPath, int index)
    {
        var d = File.ReadAllBytes(portraitPath);
        var t = Parse(d)[index];
        return DecodeToBitmap(d, t);
    }

    private static BitmapSource DecodeToBitmap(byte[] d, TexInfo t)
    {
        var block = new byte[t.DataLen];
        Array.Copy(d, t.DataOffset, block, 0, block.Length);
        var bgra = t.Format switch
        {
            0x01 => SwapRb(block), // RGBA -> BGRA
            0x59 => Dxt.DecodeBc1(block, t.Width, t.Height),
            _ => Dxt.DecodeBc3(block, t.Width, t.Height),
        };
        var bmp = BitmapSource.Create(t.Width, t.Height, 96, 96,
            PixelFormats.Bgra32, null, bgra, t.Width * 4);
        bmp.Freeze();
        return bmp;
    }

    public void ExportPng(string portraitPath, int index, string outPng)
    {
        var bmp = DecodeFull(portraitPath, index);
        using var fs = File.Create(outPng);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        enc.Save(fs);
    }

    public void Replace(string portraitPath, int index, string imagePath)
    {
        var d = File.ReadAllBytes(portraitPath);
        var t = Parse(d)[index];

        // 새 이미지 → t.Width x t.Height BGRA32
        var bgra = LoadImageAsBgra(imagePath, t.Width, t.Height);
        var encoded = t.Format switch
        {
            0x01 => SwapRb(bgra), // BGRA -> RGBA (스왑은 대칭)
            0x59 => Dxt.EncodeBc1(bgra, t.Width, t.Height),
            _ => Dxt.EncodeBc3(bgra, t.Width, t.Height),
        };
        if (encoded.Length != t.DataLen)
            throw new InvalidOperationException($"인코딩 크기 불일치 ({encoded.Length} != {t.DataLen}).");

        // 전체 파일 백업(최초 1회) — 통째로 복구용
        var bak = portraitPath + ".bak";
        if (!File.Exists(bak))
            File.Copy(portraitPath, bak);

        // 원본 이미지 자동 백업 — 교체 전 해당 인덱스를 PNG로 저장(인덱스별 최초 1회만 = 진짜 원본 보존)
        var bakDir = Path.Combine(Path.GetDirectoryName(portraitPath)!, "portrait_backup");
        Directory.CreateDirectory(bakDir);
        var imgBak = Path.Combine(bakDir, $"{Path.GetFileNameWithoutExtension(portraitPath)}_{index:00}.png");
        if (!File.Exists(imgBak))
        {
            var orig = DecodeToBitmap(d, t); // d는 아직 원본 상태
            using var bfs = File.Create(imgBak);
            var penc = new PngBitmapEncoder();
            penc.Frames.Add(BitmapFrame.Create(orig));
            penc.Save(bfs);
        }

        Array.Copy(encoded, 0, d, t.DataOffset, t.DataLen);
        File.WriteAllBytes(portraitPath, d);
    }

    /// <summary>R↔B 채널 스왑(RGBA↔BGRA, 4바이트/픽셀). 대칭이라 양방향 동일.</summary>
    private static byte[] SwapRb(byte[] src)
    {
        var o = new byte[src.Length];
        for (int i = 0; i + 3 < src.Length; i += 4)
        {
            o[i] = src[i + 2];
            o[i + 1] = src[i + 1];
            o[i + 2] = src[i];
            o[i + 3] = src[i + 3];
        }
        return o;
    }

    private static byte[] LoadImageAsBgra(string imagePath, int w, int h)
    {
        // PNG/JPG/BMP/GIF 등 BitmapDecoder가 지원하는 모든 포맷.
        var decoder = BitmapDecoder.Create(new Uri(imagePath), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        BitmapSource src = decoder.Frames[0];

        // 정확히 w×h 로 렌더(어떤 입력 크기든 안전). DrawImage로 스케일.
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
            dc.DrawImage(src, new Rect(0, 0, w, h));
        var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);

        // Pbgra32(미리곱셈) → Bgra32(직선) 변환
        var conv = new FormatConvertedBitmap(rtb, PixelFormats.Bgra32, null, 0);
        var bgra = new byte[w * h * 4];
        conv.CopyPixels(bgra, w * 4, 0);
        return bgra;
    }
}
