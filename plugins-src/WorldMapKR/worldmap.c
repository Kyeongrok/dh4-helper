// WorldMapKR — 게임 중 R 키로 세계지도 창을 띄운다(대항해시대3 의 지도처럼).
//
// 게임 폴더의 World.dat(2500x1250 칸, 칸당 uint16 타일 번호)와 Chip.DK4(타일 아틀라스,
// G1T/BC3 512x2048 = 32px 타일 16열 x 64행)를 직접 읽어 실제 게임 타일로 그린다.
// 게임 렌더링(D3D11)에는 손대지 않고, 게임 창을 소유자로 하는 별도 창을 띄운다.
//
//   R            지도 열기/닫기 (게임 창이나 지도 창이 앞에 있을 때)
//   Esc          지도 닫기
//   휠           커서 기준 확대/축소
//   왼쪽 드래그  지도 이동 (동서는 이어져 있다)
//   더블클릭     전체 보기
//
// 지도 창이 앞에 있는 동안 게임이 클릭/키를 받지 않도록 하는 일은 KeyMapKR 의
// GetAsyncKeyState 후크가 맡는다. 그래서 여기서 R 키를 볼 때는 그 후크를 거치지 않도록
// win32u!NtUserGetAsyncKeyState 를 직접 부른다.

#include <windows.h>
#include <windowsx.h>
#include <math.h>
#include <stdlib.h>
#include "dh4log.h"
#include "dh4menu.h"
#include "ports.h"

#define TAG L"WorldMap"

#define MAP_W 2500
#define MAP_H 1250
#define TILE 32
#define TILE_COUNT 1024
#define ATLAS_COLS 16

#define SEA_COLOR 0xFF28465Fu   // 0xAARRGGBB, 타일 투명부 아래 바다색
#define OUT_COLOR 0xFF10161Cu   // 지도 바깥(남북 끝 너머)

#define TOGGLE_VK 'R'
#define WM_APP_TOGGLE (WM_APP + 1)

static const wchar_t* kClass = L"Dh4WorldMapKR";
static const wchar_t* kTitle = L"세계지도  —  휠: 확대/축소 · 드래그: 이동 · 더블클릭: 전체 · R/Esc: 닫기";

static HINSTANCE g_inst;
static HWND g_wnd;
static HWND g_game;

// 데이터
static BOOL g_loaded, g_loadFailed;
static UINT16* g_cells;        // MAP_W*MAP_H
static UINT32* g_tiles;        // TILE_COUNT * TILE*TILE, 바다색 위에 합성된 불투명 BGRA
static UINT32 g_avg[TILE_COUNT];
// 축소용 타일(밉맵): g_mip[k] = 한 변 (TILE >> k) px 타일들. k=0 은 g_tiles 원본.
#define MIP_LEVELS 5           // 32, 16, 8, 4, 2
static UINT32* g_mip[MIP_LEVELS];

// 뷰: 화면 (sx,sy) ↔ 지도 칸 (ox + sx/scale, oy + sy/scale)
static double g_scale, g_ox, g_oy;
static BOOL g_viewInit;

// 렌더 버퍼
static HDC g_memDC;
static HBITMAP g_dib;
static UINT32* g_px;
static int g_bw, g_bh;

static HFONT g_font;

static BOOL g_dragging;
static POINT g_dragLast;

// ── 키 상태(후크 우회) ───────────────────────────────────────────────────────

typedef SHORT(WINAPI* PFN_AsyncKey)(int);
static PFN_AsyncKey g_rawKey;

static SHORT RawKey(int vk)
{
    if (!g_rawKey)
    {
        HMODULE w = LoadLibraryW(L"win32u.dll");
        if (w)
            g_rawKey = (PFN_AsyncKey)GetProcAddress(w, "NtUserGetAsyncKeyState");
        if (!g_rawKey)
            g_rawKey = GetAsyncKeyState;
    }
    return g_rawKey(vk);
}

// ── 파일 읽기 ────────────────────────────────────────────────────────────────

static BYTE* ReadAll(const wchar_t* path, DWORD* size)
{
    HANDLE h = CreateFileW(path, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, NULL,
                           OPEN_EXISTING, 0, NULL);
    if (h == INVALID_HANDLE_VALUE)
        return NULL;
    DWORD n = GetFileSize(h, NULL), got = 0;
    BYTE* buf = (BYTE*)malloc(n ? n : 1);
    if (buf && !ReadFile(h, buf, n, &got, NULL))
        got = 0;
    CloseHandle(h);
    if (!buf || got != n)
    {
        free(buf);
        return NULL;
    }
    *size = n;
    return buf;
}

static void GameDir(wchar_t* out, size_t cch)
{
    GetModuleFileNameW(NULL, out, (DWORD)cch);
    wchar_t* s = wcsrchr(out, L'\\');
    if (s) *s = 0;
}

// ── BC3(DXT5) → BGRA ─────────────────────────────────────────────────────────

static void Rgb565(UINT16 c, int* r, int* g, int* b)
{
    *r = (((c >> 11) & 31) * 255 + 15) / 31;
    *g = (((c >> 5) & 63) * 255 + 31) / 63;
    *b = ((c & 31) * 255 + 15) / 31;
}

static void DecodeBc3(const BYTE* src, int w, int h, UINT32* dst /* 0xAARRGGBB */)
{
    const BYTE* p = src;
    for (int by = 0; by < h; by += 4)
        for (int bx = 0; bx < w; bx += 4, p += 16)
        {
            int at[8];
            at[0] = p[0]; at[1] = p[1];
            if (at[0] > at[1])
                for (int i = 1; i <= 6; i++) at[i + 1] = ((7 - i) * at[0] + i * at[1]) / 7;
            else
            {
                for (int i = 1; i <= 4; i++) at[i + 1] = ((5 - i) * at[0] + i * at[1]) / 5;
                at[6] = 0; at[7] = 255;
            }
            UINT64 ab = 0;
            for (int i = 0; i < 6; i++) ab |= (UINT64)p[2 + i] << (8 * i);

            UINT16 c0 = (UINT16)(p[8] | (p[9] << 8)), c1 = (UINT16)(p[10] | (p[11] << 8));
            int cr[4], cg[4], cb[4];
            Rgb565(c0, &cr[0], &cg[0], &cb[0]);
            Rgb565(c1, &cr[1], &cg[1], &cb[1]);
            cr[2] = (2 * cr[0] + cr[1]) / 3; cg[2] = (2 * cg[0] + cg[1]) / 3; cb[2] = (2 * cb[0] + cb[1]) / 3;
            cr[3] = (cr[0] + 2 * cr[1]) / 3; cg[3] = (cg[0] + 2 * cg[1]) / 3; cb[3] = (cb[0] + 2 * cb[1]) / 3;
            UINT32 bits = (UINT32)(p[12] | (p[13] << 8) | (p[14] << 16) | ((UINT32)p[15] << 24));

            for (int k = 0; k < 16; k++)
            {
                int x = bx + (k & 3), y = by + (k >> 2);
                int ci = (bits >> (2 * k)) & 3;
                int a = at[(ab >> (3 * k)) & 7];
                dst[y * w + x] = ((UINT32)a << 24) | (cr[ci] << 16) | (cg[ci] << 8) | cb[ci];
            }
        }
}

// ── 데이터 준비 ──────────────────────────────────────────────────────────────

static BOOL LoadData(void)
{
    if (g_loaded) return TRUE;
    if (g_loadFailed) return FALSE;

    wchar_t dir[MAX_PATH], path[MAX_PATH];
    GameDir(dir, MAX_PATH);

    DWORD n = 0;
    _snwprintf(path, MAX_PATH, L"%s\\World.dat", dir);
    BYTE* world = ReadAll(path, &n);
    if (!world || n != MAP_W * MAP_H * 2)
    {
        Dh4Log(TAG, L"World.dat 를 읽지 못했다 (%s, %lu 바이트)", path, n);
        free(world);
        g_loadFailed = TRUE;
        return FALSE;
    }
    g_cells = (UINT16*)world; // x64 = little-endian, 그대로 쓴다

    g_tiles = (UINT32*)malloc(sizeof(UINT32) * TILE_COUNT * TILE * TILE);
    if (!g_tiles) { g_loadFailed = TRUE; return FALSE; }

    _snwprintf(path, MAX_PATH, L"%s\\Chip.DK4", dir);
    BYTE* chip = ReadAll(path, &n);
    BOOL atlasOk = FALSE;
    if (chip && n > 0x20 && chip[0] == 'G' && chip[1] == 'T' && chip[2] == '1' && chip[3] == 'G')
    {
        UINT32 tableOff = *(UINT32*)(chip + 0x0C);
        UINT32 off0 = tableOff + 4 <= n ? *(UINT32*)(chip + tableOff) : 0;
        UINT32 tb = tableOff + off0;
        if (tb + 0x14 < n)
        {
            BYTE dim = chip[tb + 2];
            int w = 1 << (dim & 15), h = 1 << ((dim >> 4) & 15);
            if (w == ATLAS_COLS * TILE && (UINT64)tb + 0x14 + (UINT64)w * h <= n
                && h / TILE * ATLAS_COLS >= TILE_COUNT)
            {
                UINT32* atlas = (UINT32*)malloc(sizeof(UINT32) * w * h);
                if (atlas)
                {
                    DecodeBc3(chip + tb + 0x14, w, h, atlas);
                    for (int v = 0; v < TILE_COUNT; v++)
                    {
                        int ax = (v % ATLAS_COLS) * TILE, ay = (v / ATLAS_COLS) * TILE;
                        UINT32* t = g_tiles + (size_t)v * TILE * TILE;
                        for (int y = 0; y < TILE; y++)
                            for (int x = 0; x < TILE; x++)
                            {
                                UINT32 c = atlas[(ay + y) * w + ax + x];
                                int a = c >> 24, ba = 255 - a;
                                int r = (((c >> 16) & 255) * a + ((SEA_COLOR >> 16) & 255) * ba) / 255;
                                int g = (((c >> 8) & 255) * a + ((SEA_COLOR >> 8) & 255) * ba) / 255;
                                int b = ((c & 255) * a + (SEA_COLOR & 255) * ba) / 255;
                                t[y * TILE + x] = 0xFF000000u | (r << 16) | (g << 8) | b;
                            }
                    }
                    free(atlas);
                    atlasOk = TRUE;
                }
            }
        }
    }
    free(chip);

    if (!atlasOk)
    {
        Dh4Log(TAG, L"Chip.DK4 를 읽지 못해 단색으로 그린다");
        for (int v = 0; v < TILE_COUNT; v++)
        {
            UINT32 c = (v == 0 || v == 1 || v == 16 || v == 17) ? SEA_COLOR : 0xFF8A8F4Au;
            UINT32* t = g_tiles + (size_t)v * TILE * TILE;
            for (int i = 0; i < TILE * TILE; i++) t[i] = c;
        }
    }

    // 축소 화면용 타일 평균색
    for (int v = 0; v < TILE_COUNT; v++)
    {
        UINT32* t = g_tiles + (size_t)v * TILE * TILE;
        UINT64 r = 0, g = 0, b = 0;
        for (int i = 0; i < TILE * TILE; i++)
        {
            r += (t[i] >> 16) & 255; g += (t[i] >> 8) & 255; b += t[i] & 255;
        }
        int N = TILE * TILE;
        g_avg[v] = 0xFF000000u | ((UINT32)(r / N) << 16) | ((UINT32)(g / N) << 8) | (UINT32)(b / N);
    }

    // 밉맵: 한 단계마다 2x2 평균으로 반씩 줄인다
    g_mip[0] = g_tiles;
    for (int k = 1; k < MIP_LEVELS; k++)
    {
        int ps = TILE >> (k - 1), cs = TILE >> k;
        g_mip[k] = (UINT32*)malloc(sizeof(UINT32) * TILE_COUNT * cs * cs);
        if (!g_mip[k]) { g_loadFailed = TRUE; return FALSE; }
        for (int v = 0; v < TILE_COUNT; v++)
        {
            const UINT32* src = g_mip[k - 1] + (size_t)v * ps * ps;
            UINT32* dst = g_mip[k] + (size_t)v * cs * cs;
            for (int y = 0; y < cs; y++)
                for (int x = 0; x < cs; x++)
                {
                    UINT32 a = src[(2 * y) * ps + 2 * x], b = src[(2 * y) * ps + 2 * x + 1];
                    UINT32 c = src[(2 * y + 1) * ps + 2 * x], d = src[(2 * y + 1) * ps + 2 * x + 1];
                    UINT32 r = (((a >> 16) & 255) + ((b >> 16) & 255) + ((c >> 16) & 255) + ((d >> 16) & 255)) / 4;
                    UINT32 g = (((a >> 8) & 255) + ((b >> 8) & 255) + ((c >> 8) & 255) + ((d >> 8) & 255)) / 4;
                    UINT32 bl = ((a & 255) + (b & 255) + (c & 255) + (d & 255)) / 4;
                    dst[y * cs + x] = 0xFF000000u | (r << 16) | (g << 8) | bl;
                }
        }
    }

    g_loaded = TRUE;
    Dh4Log(TAG, L"세계지도 데이터 준비 완료 (타일 %s)", atlasOk ? L"Chip.DK4" : L"단색");
    return TRUE;
}

// ── 뷰 ───────────────────────────────────────────────────────────────────────

static double FitScale(int cw, int ch)
{
    double s = (double)cw / MAP_W;
    double s2 = (double)ch / MAP_H;
    return s < s2 ? s : s2;
}

static void ClampView(int cw, int ch)
{
    double fit = FitScale(cw, ch);
    if (g_scale < fit) g_scale = fit;
    if (g_scale > 48) g_scale = 48;

    // 동서는 이어지므로 ox 는 감기만 한다
    g_ox = fmod(g_ox, (double)MAP_W);
    if (g_ox < 0) g_ox += MAP_W;

    double viewH = ch / g_scale;
    if (viewH >= MAP_H)
        g_oy = -(viewH - MAP_H) / 2; // 위아래 여백을 가운데로
    else
    {
        if (g_oy < 0) g_oy = 0;
        if (g_oy > MAP_H - viewH) g_oy = MAP_H - viewH;
    }
}

static void FitAll(int cw, int ch)
{
    g_scale = FitScale(cw, ch);
    double viewW = cw / g_scale;
    g_ox = -(viewW - MAP_W) / 2;   // 가로가 남으면 가운데 정렬(감기 처리 전)
    if (g_ox < 0) g_ox += MAP_W;
    g_oy = 0;
    ClampView(cw, ch);
}

// ── 렌더 ─────────────────────────────────────────────────────────────────────

static void EnsureBuffer(HDC ref, int w, int h)
{
    if (g_dib && g_bw == w && g_bh == h)
        return;
    if (g_dib) DeleteObject(g_dib);
    if (!g_memDC) g_memDC = CreateCompatibleDC(ref);

    BITMAPINFO bi = { 0 };
    bi.bmiHeader.biSize = sizeof(bi.bmiHeader);
    bi.bmiHeader.biWidth = w;
    bi.bmiHeader.biHeight = -h; // 위에서 아래로
    bi.bmiHeader.biPlanes = 1;
    bi.bmiHeader.biBitCount = 32;
    bi.bmiHeader.biCompression = BI_RGB;
    g_dib = CreateDIBSection(ref, &bi, DIB_RGB_COLORS, (void**)&g_px, NULL, 0);
    SelectObject(g_memDC, g_dib);
    g_bw = w;
    g_bh = h;
}

static void Render(int w, int h)
{
    if (!g_px || w <= 0 || h <= 0)
        return;

    // 배율에 맞는 타일 크기 고르기: 화면상 칸 크기 이상인 가장 작은 밉(없으면 평균색)
    int level = -1, ts = 0;
    for (int k = MIP_LEVELS - 1; k >= 0; k--)
    {
        int size = TILE >> k;
        if (size >= g_scale || k == 0) { level = k; ts = size; break; }
    }
    if (g_scale < 1.5) level = -1; // 칸이 1px 남짓이면 무늬가 안 보이니 평균색
    const UINT32* mip = level >= 0 ? g_mip[level] : NULL;

    // 열마다 지도 x 칸과 타일 안 x 픽셀을 미리 구한다
    int* colCell = (int*)malloc(sizeof(int) * w);
    int* colSub = (int*)malloc(sizeof(int) * w);
    if (!colCell || !colSub) { free(colCell); free(colSub); return; }
    for (int sx = 0; sx < w; sx++)
    {
        double wx = g_ox + (sx + 0.5) / g_scale;
        double fx = floor(wx);
        int cx = (int)fx % MAP_W;
        if (cx < 0) cx += MAP_W;
        colCell[sx] = cx;
        int sub = (int)((wx - fx) * (ts ? ts : 1));
        colSub[sx] = sub < 0 ? 0 : (ts && sub > ts - 1 ? ts - 1 : sub);
    }

    for (int sy = 0; sy < h; sy++)
    {
        UINT32* row = g_px + (size_t)sy * w;
        double wy = g_oy + (sy + 0.5) / g_scale;
        if (wy < 0 || wy >= MAP_H)
        {
            for (int sx = 0; sx < w; sx++) row[sx] = OUT_COLOR;
            continue;
        }
        double fy = floor(wy);
        int cy = (int)fy;
        int suby = (int)((wy - fy) * (ts ? ts : 1));
        if (ts && suby > ts - 1) suby = ts - 1;
        const UINT16* cells = g_cells + (size_t)cy * MAP_W;

        if (mip)
        {
            size_t tsz = (size_t)ts * ts;
            int rowOff = suby * ts;
            for (int sx = 0; sx < w; sx++)
            {
                UINT16 v = cells[colCell[sx]];
                if (v >= TILE_COUNT) { row[sx] = SEA_COLOR; continue; }
                row[sx] = mip[v * tsz + rowOff + colSub[sx]];
            }
        }
        else
        {
            for (int sx = 0; sx < w; sx++)
            {
                UINT16 v = cells[colCell[sx]];
                row[sx] = v < TILE_COUNT ? g_avg[v] : SEA_COLOR;
            }
        }
    }
    free(colCell);
    free(colSub);
}

// 항구 점과 이름을 렌더 버퍼(g_memDC) 위에 그린다
static void DrawPorts(int w, int h)
{
    if (!g_font)
        g_font = CreateFontW(-13, 0, 0, 0, FW_BOLD, FALSE, FALSE, FALSE, DEFAULT_CHARSET,
                             OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY,
                             DEFAULT_PITCH, L"Malgun Gothic");

    BOOL names = g_scale >= 0.9;                 // 너무 작으면 이름은 빼고 점만
    int r = g_scale >= 4 ? 5 : (g_scale >= 1.5 ? 4 : 3);

    HGDIOBJ oldFont = SelectObject(g_memDC, g_font);
    HPEN pen = CreatePen(PS_SOLID, 1, RGB(255, 255, 255));
    HBRUSH brush = CreateSolidBrush(RGB(230, 50, 40));
    HGDIOBJ oldPen = SelectObject(g_memDC, pen);
    HGDIOBJ oldBrush = SelectObject(g_memDC, brush);
    SetBkMode(g_memDC, TRANSPARENT);

    for (int i = 0; i < PORT_COUNT; i++)
    {
        double sy = (kPorts[i].y + 0.5 - g_oy) * g_scale;
        if (sy < -20 || sy > h + 20)
            continue;
        // 동서가 이어져 있으니 한 바퀴 앞뒤도 본다
        for (int wrap = -1; wrap <= 1; wrap++)
        {
            double sx = (kPorts[i].x + 0.5 + wrap * MAP_W - g_ox) * g_scale;
            if (sx < -100 || sx > w + 20)
                continue;
            int x = (int)sx, y = (int)sy;
            Ellipse(g_memDC, x - r, y - r, x + r + 1, y + r + 1);
            if (names)
            {
                const wchar_t* t = kPorts[i].name;
                int len = (int)wcslen(t), tx = x + r + 3, ty = y - 8;
                SetTextColor(g_memDC, RGB(20, 14, 8));
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        if (dx || dy) TextOutW(g_memDC, tx + dx, ty + dy, t, len);
                SetTextColor(g_memDC, RGB(255, 244, 214));
                TextOutW(g_memDC, tx, ty, t, len);
            }
        }
    }

    SelectObject(g_memDC, oldPen);
    SelectObject(g_memDC, oldBrush);
    SelectObject(g_memDC, oldFont);
    DeleteObject(pen);
    DeleteObject(brush);
}

static void Redraw(HWND hwnd)
{
    InvalidateRect(hwnd, NULL, FALSE);
}

// ── 창 ───────────────────────────────────────────────────────────────────────

static void ShowMap(void)
{
    if (!LoadData())
    {
        MessageBoxW(g_game, L"World.dat 를 읽지 못했습니다.\n게임 폴더에 World.dat 가 있는지 확인하세요.",
                    L"세계지도", MB_OK | MB_ICONWARNING);
        return;
    }

    HWND game = Dh4MenuGameWindow();
    if (game != g_game)
    {
        g_game = game;
        // 소유자를 지금 게임 창으로 맞춘다(게임이 창을 다시 만들었을 수 있다)
        SetWindowLongPtrW(g_wnd, GWLP_HWNDPARENT, (LONG_PTR)game);
    }

    // 게임 창의 85% 크기로 가운데
    RECT gr;
    if (game && GetWindowRect(game, &gr))
    {
        int gw = gr.right - gr.left, gh = gr.bottom - gr.top;
        int w = gw * 85 / 100, h = gh * 85 / 100;
        if (w < 640) w = 640;
        if (h < 400) h = 400;
        SetWindowPos(g_wnd, NULL, gr.left + (gw - w) / 2, gr.top + (gh - h) / 2, w, h,
                     SWP_NOZORDER | SWP_NOACTIVATE);
    }

    RECT cr;
    GetClientRect(g_wnd, &cr);
    if (!g_viewInit)
    {
        // 처음에는 타일이 보이는 배율(칸당 4px)로 지중해를 보여 준다. 전체는 더블클릭.
        g_scale = 4.0;
        g_ox = 1340 - cr.right / g_scale / 2;
        g_oy = 330 - cr.bottom / g_scale / 2;
        ClampView(cr.right, cr.bottom);
        g_viewInit = TRUE;
    }
    else
        ClampView(cr.right, cr.bottom);

    ShowWindow(g_wnd, SW_SHOW);
    SetForegroundWindow(g_wnd);
    Redraw(g_wnd);
}

static void HideMap(void)
{
    if (!IsWindowVisible(g_wnd))
        return;
    g_dragging = FALSE;
    ReleaseCapture();
    ShowWindow(g_wnd, SW_HIDE);
    if (g_game && IsWindow(g_game))
        SetForegroundWindow(g_game);
}

static void ToggleMap(void)
{
    if (IsWindowVisible(g_wnd))
        HideMap();
    else
        ShowMap();
}

static void ZoomAt(HWND hwnd, double factor, int sx, int sy)
{
    RECT cr;
    GetClientRect(hwnd, &cr);
    double wx = g_ox + sx / g_scale, wy = g_oy + sy / g_scale;
    g_scale *= factor;
    ClampView(cr.right, cr.bottom);
    g_ox = wx - sx / g_scale;
    g_oy = wy - sy / g_scale;
    ClampView(cr.right, cr.bottom);
    Redraw(hwnd);
}

static LRESULT CALLBACK WndProc(HWND hwnd, UINT msg, WPARAM wp, LPARAM lp)
{
    switch (msg)
    {
    case WM_APP_TOGGLE:
        ToggleMap();
        return 0;

    case WM_CLOSE:
        HideMap(); // 닫기 버튼은 숨기기만 한다
        return 0;

    case WM_KEYDOWN:
        if (wp == VK_ESCAPE)
            HideMap();
        return 0;

    case WM_SIZE:
    {
        RECT cr;
        GetClientRect(hwnd, &cr);
        if (g_viewInit) ClampView(cr.right, cr.bottom);
        Redraw(hwnd);
        return 0;
    }

    case WM_MOUSEWHEEL:
    {
        POINT p = { GET_X_LPARAM(lp), GET_Y_LPARAM(lp) };
        ScreenToClient(hwnd, &p);
        int d = GET_WHEEL_DELTA_WPARAM(wp);
        ZoomAt(hwnd, d > 0 ? 1.25 : 0.8, p.x, p.y);
        return 0;
    }

    case WM_LBUTTONDOWN:
        g_dragging = TRUE;
        g_dragLast.x = GET_X_LPARAM(lp);
        g_dragLast.y = GET_Y_LPARAM(lp);
        SetCapture(hwnd);
        return 0;

    case WM_MOUSEMOVE:
        if (g_dragging)
        {
            int x = GET_X_LPARAM(lp), y = GET_Y_LPARAM(lp);
            RECT cr;
            GetClientRect(hwnd, &cr);
            g_ox -= (x - g_dragLast.x) / g_scale;
            g_oy -= (y - g_dragLast.y) / g_scale;
            g_dragLast.x = x;
            g_dragLast.y = y;
            ClampView(cr.right, cr.bottom);
            Redraw(hwnd);
        }
        return 0;

    case WM_LBUTTONUP:
    case WM_CAPTURECHANGED:
        g_dragging = FALSE;
        if (msg == WM_LBUTTONUP) ReleaseCapture();
        return 0;

    case WM_LBUTTONDBLCLK:
    {
        RECT cr;
        GetClientRect(hwnd, &cr);
        FitAll(cr.right, cr.bottom);
        Redraw(hwnd);
        return 0;
    }

    case WM_SETCURSOR:
        if (LOWORD(lp) == HTCLIENT)
        {
            SetCursor(LoadCursor(NULL, g_dragging ? IDC_SIZEALL : IDC_HAND));
            return TRUE;
        }
        break;

    case WM_ERASEBKGND:
        return 1;

    case WM_PAINT:
    {
        PAINTSTRUCT ps;
        HDC dc = BeginPaint(hwnd, &ps);
        RECT cr;
        GetClientRect(hwnd, &cr);
        if (g_loaded && cr.right > 0 && cr.bottom > 0)
        {
            EnsureBuffer(dc, cr.right, cr.bottom);
            Render(cr.right, cr.bottom);
            GdiFlush();
            DrawPorts(cr.right, cr.bottom);
            BitBlt(dc, 0, 0, cr.right, cr.bottom, g_memDC, 0, 0, SRCCOPY);
        }
        EndPaint(hwnd, &ps);
        return 0;
    }
    }
    return DefWindowProcW(hwnd, msg, wp, lp);
}

// ── 단축키 감시 ──────────────────────────────────────────────────────────────

static BOOL ForegroundIsOurs(void)
{
    HWND fg = GetForegroundWindow();
    if (!fg) return FALSE;
    if (fg == g_wnd) return TRUE;
    HWND game = Dh4MenuGameWindow();
    return game && fg == game;
}

static VOID CALLBACK HotkeyTimer(HWND hwnd, UINT msg, UINT_PTR id, DWORD t)
{
    (void)msg; (void)id; (void)t;
    static BOOL wasDown = TRUE; // 시작할 때 눌려 있던 것은 무시
    BOOL down = (RawKey(TOGGLE_VK) & 0x8000) != 0;
    if (down && !wasDown && ForegroundIsOurs())
        PostMessageW(hwnd, WM_APP_TOGGLE, 0, 0);
    wasDown = down;
}

static void WINAPI OnMenuOpen(void* user)
{
    (void)user;
    if (g_wnd)
        PostMessageW(g_wnd, WM_APP_TOGGLE, 0, 0);
}

static DWORD WINAPI WorkerThread(LPVOID unused)
{
    (void)unused;

    // 게임 창이 생길 때까지 기다린다. 런처 스텁 프로세스에는 창이 없으니 조용히 물러난다.
    HWND game = NULL;
    for (int i = 0; i < 600 && !game; i++) // 최대 60초
    {
        game = Dh4MenuGameWindow();
        if (!game) Sleep(100);
    }
    if (!game)
        return 0;
    g_game = game;

    WNDCLASSEXW wc = { sizeof(wc) };
    wc.style = CS_DBLCLKS;
    wc.lpfnWndProc = WndProc;
    wc.hInstance = g_inst;
    wc.hCursor = LoadCursor(NULL, IDC_HAND);
    wc.lpszClassName = kClass;
    wc.hIcon = (HICON)GetClassLongPtrW(game, GCLP_HICON);
    RegisterClassExW(&wc);

    g_wnd = CreateWindowExW(WS_EX_TOOLWINDOW, kClass, kTitle,
                            WS_POPUP | WS_CAPTION | WS_SYSMENU | WS_THICKFRAME,
                            CW_USEDEFAULT, CW_USEDEFAULT, 1280, 720,
                            game, NULL, wc.hInstance, NULL);
    if (!g_wnd)
    {
        Dh4Log(TAG, L"지도 창을 만들지 못했다 (%lu)", GetLastError());
        return 0;
    }

    SetTimer(g_wnd, 1, 30, HotkeyTimer);
    Dh4MenuAdd(L"세계지도 (R)", OnMenuOpen, NULL);
    Dh4Log(TAG, L"준비 완료 — 게임 중 R 키로 세계지도");

    MSG m;
    while (GetMessageW(&m, NULL, 0, 0) > 0)
    {
        TranslateMessage(&m);
        DispatchMessageW(&m);
    }
    return 0;
}

BOOL WINAPI DllMain(HINSTANCE inst, DWORD reason, LPVOID reserved)
{
    (void)reserved;
    if (reason == DLL_PROCESS_ATTACH)
    {
        g_inst = inst;
        DisableThreadLibraryCalls(inst);
        HANDLE t = CreateThread(NULL, 0, WorkerThread, NULL, 0, NULL);
        if (t)
            CloseHandle(t);
    }
    return TRUE;
}
