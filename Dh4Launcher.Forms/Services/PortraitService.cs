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

    // 포맷 코드: 0x59 = BC1(DXT1, 4bpp), 0x5B = BC3(DXT5, 8bpp)
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
            int dataLen = fmt == 0x59 ? w * h / 2 : w * h; // BC1=4bpp, BC3=8bpp
            list.Add(new TexInfo(i, texBase + TexHeaderSize, w, h, fmt, dataLen));
        }
        return list;
    }

    public IReadOnlyList<PortraitItem> Load(string portraitPath, int thumbWidth = 110)
    {
        var d = File.ReadAllBytes(portraitPath);
        var infos = Parse(d);
        var items = new List<PortraitItem>(infos.Count);
        foreach (var t in infos)
        {
            var full = DecodeToBitmap(d, t);
            double s = thumbWidth / (double)t.Width;
            var thumb = new TransformedBitmap(full, new ScaleTransform(s, s));
            thumb.Freeze();
            items.Add(new PortraitItem(t.Index, t.Width, t.Height, thumb));
        }
        return items;
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
        var bgra = t.Format == 0x59
            ? Dxt.DecodeBc1(block, t.Width, t.Height)
            : Dxt.DecodeBc3(block, t.Width, t.Height);
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
        var encoded = t.Format == 0x59
            ? Dxt.EncodeBc1(bgra, t.Width, t.Height)
            : Dxt.EncodeBc3(bgra, t.Width, t.Height);
        if (encoded.Length != t.DataLen)
            throw new InvalidOperationException($"인코딩 크기 불일치 ({encoded.Length} != {t.DataLen}).");

        var bak = portraitPath + ".bak";
        if (!File.Exists(bak))
            File.Copy(portraitPath, bak);

        Array.Copy(encoded, 0, d, t.DataOffset, t.DataLen);
        File.WriteAllBytes(portraitPath, d);
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
