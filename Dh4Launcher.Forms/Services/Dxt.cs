namespace Dh4Launcher.Forms.Services;

/// <summary>
/// BC3(DXT5) 디코드/인코드. 대항해시대 IV의 Portrait.dk4(G1T) 텍스처가 BC3 포맷이다.
/// 픽셀은 BGRA(8888) 바이트 배열로 다룬다 (WPF BitmapSource와 호환).
/// </summary>
public static class Dxt
{
    /// <summary>BC3 블록 데이터 → BGRA8888 (w*h*4 바이트).</summary>
    public static byte[] DecodeBc3(byte[] src, int w, int h)
    {
        var dst = new byte[w * h * 4];
        int p = 0;
        for (int by = 0; by < h; by += 4)
        for (int bx = 0; bx < w; bx += 4)
        {
            // ---- alpha block (8 bytes) ----
            byte a0 = src[p], a1 = src[p + 1];
            ulong aBits = 0;
            for (int i = 0; i < 6; i++) aBits |= (ulong)src[p + 2 + i] << (8 * i);
            var aTab = new byte[8];
            aTab[0] = a0; aTab[1] = a1;
            if (a0 > a1)
                for (int i = 1; i <= 6; i++) aTab[i + 1] = (byte)(((7 - i) * a0 + i * a1) / 7);
            else
            {
                for (int i = 1; i <= 4; i++) aTab[i + 1] = (byte)(((5 - i) * a0 + i * a1) / 5);
                aTab[6] = 0; aTab[7] = 255;
            }

            // ---- color block (8 bytes) ----
            int q = p + 8;
            ushort c0 = (ushort)(src[q] | (src[q + 1] << 8));
            ushort c1 = (ushort)(src[q + 2] | (src[q + 3] << 8));
            var col = new (byte r, byte g, byte b)[4];
            col[0] = Rgb565(c0);
            col[1] = Rgb565(c1);
            col[2] = (Mix(col[0].r, col[1].r, 2, 1), Mix(col[0].g, col[1].g, 2, 1), Mix(col[0].b, col[1].b, 2, 1));
            col[3] = (Mix(col[0].r, col[1].r, 1, 2), Mix(col[0].g, col[1].g, 1, 2), Mix(col[0].b, col[1].b, 1, 2));
            uint cBits = (uint)(src[q + 4] | (src[q + 5] << 8) | (src[q + 6] << 16) | (src[q + 7] << 24));

            for (int py = 0; py < 4; py++)
            for (int px = 0; px < 4; px++)
            {
                int xx = bx + px, yy = by + py;
                int bit = py * 4 + px;
                var c = col[(cBits >> (2 * bit)) & 3];
                byte a = aTab[(aBits >> (3 * bit)) & 7];
                if (xx < w && yy < h)
                {
                    int o = (yy * w + xx) * 4;
                    dst[o] = c.b; dst[o + 1] = c.g; dst[o + 2] = c.r; dst[o + 3] = a;
                }
            }
            p += 16;
        }
        return dst;
    }

    /// <summary>BGRA8888 (w*h*4) → BC3 블록 데이터. 간단한(고속) 인코더.</summary>
    public static byte[] EncodeBc3(byte[] bgra, int w, int h)
    {
        int bw = (w + 3) / 4, bh = (h + 3) / 4;
        var dst = new byte[bw * bh * 16];
        int p = 0;
        var blkA = new byte[16];
        var blkR = new byte[16];
        var blkG = new byte[16];
        var blkB = new byte[16];
        for (int by = 0; by < h; by += 4)
        for (int bx = 0; bx < w; bx += 4)
        {
            for (int py = 0; py < 4; py++)
            for (int px = 0; px < 4; px++)
            {
                int xx = Math.Min(bx + px, w - 1), yy = Math.Min(by + py, h - 1);
                int o = (yy * w + xx) * 4;
                int k = py * 4 + px;
                blkB[k] = bgra[o]; blkG[k] = bgra[o + 1]; blkR[k] = bgra[o + 2]; blkA[k] = bgra[o + 3];
            }

            // ---- alpha ----
            byte aMin = 255, aMax = 0;
            foreach (var a in blkA) { if (a < aMin) aMin = a; if (a > aMax) aMax = a; }
            dst[p] = aMax; dst[p + 1] = aMin;
            var aTab = new byte[8];
            aTab[0] = aMax; aTab[1] = aMin;
            if (aMax > aMin)
                for (int i = 1; i <= 6; i++) aTab[i + 1] = (byte)(((7 - i) * aMax + i * aMin) / 7);
            else { for (int i = 2; i < 8; i++) aTab[i] = aMax; }
            ulong aBits = 0;
            for (int k = 0; k < 16; k++)
            {
                int best = 0, bestd = 1 << 30;
                for (int i = 0; i < 8; i++) { int dd = blkA[k] - aTab[i]; dd *= dd; if (dd < bestd) { bestd = dd; best = i; } }
                aBits |= (ulong)best << (3 * k);
            }
            for (int i = 0; i < 6; i++) dst[p + 2 + i] = (byte)(aBits >> (8 * i));

            // ---- color (bounding box) ----
            byte rMin = 255, gMin = 255, bMin = 255, rMax = 0, gMax = 0, bMax = 0;
            for (int k = 0; k < 16; k++)
            {
                if (blkR[k] < rMin) rMin = blkR[k]; if (blkR[k] > rMax) rMax = blkR[k];
                if (blkG[k] < gMin) gMin = blkG[k]; if (blkG[k] > gMax) gMax = blkG[k];
                if (blkB[k] < bMin) bMin = blkB[k]; if (blkB[k] > bMax) bMax = blkB[k];
            }
            ushort c0 = To565(rMax, gMax, bMax), c1 = To565(rMin, gMin, bMin);
            if (c0 < c1) { (c0, c1) = (c1, c0); }
            if (c0 == c1) { if (c0 == 0) c0 = 1; else c1 = (ushort)(c0 - 1); }
            int q = p + 8;
            dst[q] = (byte)c0; dst[q + 1] = (byte)(c0 >> 8);
            dst[q + 2] = (byte)c1; dst[q + 3] = (byte)(c1 >> 8);
            var col = new (int r, int g, int b)[4];
            var p0 = Rgb565(c0); var p1 = Rgb565(c1);
            col[0] = (p0.r, p0.g, p0.b); col[1] = (p1.r, p1.g, p1.b);
            col[2] = (Mix(p0.r, p1.r, 2, 1), Mix(p0.g, p1.g, 2, 1), Mix(p0.b, p1.b, 2, 1));
            col[3] = (Mix(p0.r, p1.r, 1, 2), Mix(p0.g, p1.g, 1, 2), Mix(p0.b, p1.b, 1, 2));
            uint cBits = 0;
            for (int k = 0; k < 16; k++)
            {
                int best = 0, bestd = 1 << 30;
                for (int i = 0; i < 4; i++)
                {
                    int dr = blkR[k] - col[i].r, dg = blkG[k] - col[i].g, db = blkB[k] - col[i].b;
                    int dd = dr * dr + dg * dg + db * db;
                    if (dd < bestd) { bestd = dd; best = i; }
                }
                cBits |= (uint)best << (2 * k);
            }
            dst[q + 4] = (byte)cBits; dst[q + 5] = (byte)(cBits >> 8);
            dst[q + 6] = (byte)(cBits >> 16); dst[q + 7] = (byte)(cBits >> 24);
            p += 16;
        }
        return dst;
    }

    /// <summary>BC1(DXT1) 블록 데이터 → BGRA8888. 4bpp.</summary>
    public static byte[] DecodeBc1(byte[] src, int w, int h)
    {
        var dst = new byte[w * h * 4];
        int p = 0;
        for (int by = 0; by < h; by += 4)
        for (int bx = 0; bx < w; bx += 4)
        {
            ushort c0 = (ushort)(src[p] | (src[p + 1] << 8));
            ushort c1 = (ushort)(src[p + 2] | (src[p + 3] << 8));
            var col = new (byte r, byte g, byte b, byte a)[4];
            var a0 = Rgb565(c0); var a1 = Rgb565(c1);
            col[0] = (a0.r, a0.g, a0.b, 255);
            col[1] = (a1.r, a1.g, a1.b, 255);
            if (c0 > c1)
            {
                col[2] = (Mix(a0.r, a1.r, 2, 1), Mix(a0.g, a1.g, 2, 1), Mix(a0.b, a1.b, 2, 1), 255);
                col[3] = (Mix(a0.r, a1.r, 1, 2), Mix(a0.g, a1.g, 1, 2), Mix(a0.b, a1.b, 1, 2), 255);
            }
            else
            {
                col[2] = ((byte)((a0.r + a1.r) / 2), (byte)((a0.g + a1.g) / 2), (byte)((a0.b + a1.b) / 2), 255);
                col[3] = (0, 0, 0, 0); // 투명
            }
            uint bits = (uint)(src[p + 4] | (src[p + 5] << 8) | (src[p + 6] << 16) | (src[p + 7] << 24));
            for (int py = 0; py < 4; py++)
            for (int px = 0; px < 4; px++)
            {
                int xx = bx + px, yy = by + py;
                var c = col[(bits >> (2 * (py * 4 + px))) & 3];
                if (xx < w && yy < h)
                {
                    int o = (yy * w + xx) * 4;
                    dst[o] = c.b; dst[o + 1] = c.g; dst[o + 2] = c.r; dst[o + 3] = c.a;
                }
            }
            p += 8;
        }
        return dst;
    }

    /// <summary>BGRA8888 → BC1(DXT1) 블록 데이터. 투명(알파&lt;128)은 1비트 펀치스루로 처리.</summary>
    public static byte[] EncodeBc1(byte[] bgra, int w, int h)
    {
        int bw = (w + 3) / 4, bh = (h + 3) / 4;
        var dst = new byte[bw * bh * 8];
        int p = 0;
        for (int by = 0; by < h; by += 4)
        for (int bx = 0; bx < w; bx += 4)
        {
            // 블록 픽셀 수집
            var r = new int[16]; var g = new int[16]; var b = new int[16]; var trans = new bool[16];
            bool hasAlpha = false;
            byte rMin = 255, gMin = 255, bMin = 255, rMax = 0, gMax = 0, bMax = 0;
            for (int py = 0; py < 4; py++)
            for (int px = 0; px < 4; px++)
            {
                int xx = Math.Min(bx + px, w - 1), yy = Math.Min(by + py, h - 1);
                int o = (yy * w + xx) * 4; int k = py * 4 + px;
                b[k] = bgra[o]; g[k] = bgra[o + 1]; r[k] = bgra[o + 2];
                trans[k] = bgra[o + 3] < 128;
                if (trans[k]) { hasAlpha = true; continue; }
                if (r[k] < rMin) rMin = (byte)r[k]; if (r[k] > rMax) rMax = (byte)r[k];
                if (g[k] < gMin) gMin = (byte)g[k]; if (g[k] > gMax) gMax = (byte)g[k];
                if (b[k] < bMin) bMin = (byte)b[k]; if (b[k] > bMax) bMax = (byte)b[k];
            }
            ushort cHi = To565(rMax, gMax, bMax), cLo = To565(rMin, gMin, bMin);
            ushort c0, c1;
            var pal = new (int r, int g, int b)[4];
            if (hasAlpha)
            {
                // 3색 + 투명: c0 <= c1
                c0 = cLo; c1 = cHi;
                if (c0 > c1) (c0, c1) = (c1, c0);
                var q0 = Rgb565(c0); var q1 = Rgb565(c1);
                pal[0] = (q0.r, q0.g, q0.b); pal[1] = (q1.r, q1.g, q1.b);
                pal[2] = ((q0.r + q1.r) / 2, (q0.g + q1.g) / 2, (q0.b + q1.b) / 2);
                pal[3] = (-1, -1, -1); // 투명 마커
            }
            else
            {
                // 4색 불투명: c0 > c1
                c0 = cHi; c1 = cLo;
                if (c0 < c1) (c0, c1) = (c1, c0);
                if (c0 == c1) { if (c0 == 0) c0 = 1; else c1 = (ushort)(c0 - 1); }
                var q0 = Rgb565(c0); var q1 = Rgb565(c1);
                pal[0] = (q0.r, q0.g, q0.b); pal[1] = (q1.r, q1.g, q1.b);
                pal[2] = (Mix(q0.r, q1.r, 2, 1), Mix(q0.g, q1.g, 2, 1), Mix(q0.b, q1.b, 2, 1));
                pal[3] = (Mix(q0.r, q1.r, 1, 2), Mix(q0.g, q1.g, 1, 2), Mix(q0.b, q1.b, 1, 2));
            }
            dst[p] = (byte)c0; dst[p + 1] = (byte)(c0 >> 8);
            dst[p + 2] = (byte)c1; dst[p + 3] = (byte)(c1 >> 8);
            uint bits = 0;
            for (int k = 0; k < 16; k++)
            {
                int idx;
                if (hasAlpha && trans[k]) idx = 3;
                else
                {
                    int best = 0, bestd = 1 << 30;
                    int top = hasAlpha ? 3 : 4;
                    for (int i = 0; i < top; i++)
                    {
                        int dr = r[k] - pal[i].r, dg = g[k] - pal[i].g, db = b[k] - pal[i].b;
                        int dd = dr * dr + dg * dg + db * db;
                        if (dd < bestd) { bestd = dd; best = i; }
                    }
                    idx = best;
                }
                bits |= (uint)idx << (2 * k);
            }
            dst[p + 4] = (byte)bits; dst[p + 5] = (byte)(bits >> 8);
            dst[p + 6] = (byte)(bits >> 16); dst[p + 7] = (byte)(bits >> 24);
            p += 8;
        }
        return dst;
    }

    private static (byte r, byte g, byte b) Rgb565(ushort c)
    {
        int r = (c >> 11) & 0x1F, g = (c >> 5) & 0x3F, b = c & 0x1F;
        return ((byte)((r * 255 + 15) / 31), (byte)((g * 255 + 31) / 63), (byte)((b * 255 + 15) / 31));
    }

    private static ushort To565(byte r, byte g, byte b)
        => (ushort)(((r >> 3) << 11) | ((g >> 2) << 5) | (b >> 3));

    private static byte Mix(int a, int b, int wa, int wb) => (byte)((a * wa + b * wb) / (wa + wb));
}
