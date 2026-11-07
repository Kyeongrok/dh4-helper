// MenuUtilKR — 게임 창 타이틀바에 햄버거(≡) 버튼을 붙이고, 다른 플러그인이 그 메뉴에
// 항목을 등록할 수 있게 해 준다. cds95-mod 의 게임 내 '파일' 메뉴에 해당하는 자리다.
//
// 만드는 방식
//   * 게임 창(class KT_DK4_PK_HD_WINDOW_NAME)의 클라이언트 영역은 D3D11 스왑체인이
//     쓰고 있으므로 건드리지 않는다. 타이틀바에 SetMenu 로 메뉴바를 달면 클라이언트가
//     줄어들어 렌더링이 틀어지므로 그것도 하지 않는다.
//   * 대신 게임 창을 소유자로 하는 작은 팝업 창을 캡션 위(최소화 버튼 왼쪽)에 겹쳐
//     띄운다. 배경색은 실제 캡션 픽셀을 화면에서 읽어 그대로 쓰기 때문에 밝은 테마든
//     어두운 테마든 타이틀바에 묻어 보인다.
//   * 위치·표시 여부는 (1) 게임 창 서브클래싱으로 오는 이동/활성 메시지와
//     (2) 250ms 타이머 두 가지로 맞춘다. 메시지를 놓쳐도 타이머가 복구한다.
//
// 메뉴 항목 콜백은 이 플러그인의 워커 스레드에서 불린다(게임 스레드가 아니다).

#include <windows.h>
#include "dh4log.h"
#include "dh4menu.h"

#define TAG L"MenuUtil"

#define GAME_CLASS L"KT_DK4_PK_HD_WINDOW_NAME"
#define BTN_CLASS  L"Dh4MenuButton"

#define DH4_WM_SYNC (WM_APP + 1)   // 위치·표시 상태를 다시 맞춰라
#define DH4_WM_OPEN (WM_APP + 2)   // 메뉴를 열어라
#define SYSMENU_ID  0x8800         // 시스템 메뉴(타이틀바 우클릭)에 넣는 항목 id
#define ABOUT_ID    0x7000         // 팝업 메뉴의 '정보' 항목 id
#define SYNC_TIMER  1

typedef struct
{
    int id;
    wchar_t text[64];
    Dh4MenuProc proc;
    void* user;
} MenuItem;

static MenuItem g_items[64];
static int g_itemCount;
static int g_nextId = 1;
static CRITICAL_SECTION g_lock;

static HINSTANCE g_self;
static HWND g_game;
static HWND g_btn;
static WNDPROC g_prevProc;
static COLORREF g_capColor = CLR_INVALID;
static BOOL g_hover;

// ── 색 계산 ──────────────────────────────────────────────────────────────────

static COLORREF SampleCaptionColor(const RECT* btn)
{
    // 우리 버튼 바로 왼쪽 캡션 픽셀을 화면에서 읽는다. DWM 이 그린 결과를 그대로
    // 읽으므로 밝은/어두운 테마, 강조색 설정과 무관하게 맞는다.
    HDC screen = GetDC(NULL);
    if (!screen)
        return RGB(255, 255, 255);
    COLORREF c = GetPixel(screen, btn->left - 6, (btn->top + btn->bottom) / 2);
    ReleaseDC(NULL, screen);
    return c == CLR_INVALID ? RGB(255, 255, 255) : c;
}

static BOOL IsLight(COLORREF c)
{
    return (GetRValue(c) * 299 + GetGValue(c) * 587 + GetBValue(c) * 114) / 1000 > 128;
}

static COLORREF Blend(COLORREF a, COLORREF b, int percentB)
{
    int r = (GetRValue(a) * (100 - percentB) + GetRValue(b) * percentB) / 100;
    int g = (GetGValue(a) * (100 - percentB) + GetGValue(b) * percentB) / 100;
    int bl = (GetBValue(a) * (100 - percentB) + GetBValue(b) * percentB) / 100;
    return RGB(r, g, bl);
}

// ── 버튼 위치 계산 ───────────────────────────────────────────────────────────

// 캡션 버튼(최소화/최대화/닫기)의 실제 사각형은 DefWindowProc 이 알려 준다.
// 같은 프로세스의 다른 스레드로 보내는 SendMessage 라 포인터를 그대로 넘겨도 되지만,
// 게임 스레드가 멈춰 있을 때를 대비해 타임아웃을 건다.
static BOOL CaptionButtonRect(HWND game, RECT* out)
{
    TITLEBARINFOEX tbi;
    ZeroMemory(&tbi, sizeof(tbi));
    tbi.cbSize = sizeof(tbi);

    DWORD_PTR res = 0;
    if (SendMessageTimeoutW(game, WM_GETTITLEBARINFOEX, 0, (LPARAM)&tbi,
                            SMTO_ABORTIFHUNG, 300, &res))
    {
        RECT best = { 0, 0, 0, 0 };
        for (int i = 2; i <= 5; i++)   // 2=최소화 3=최대화 4=도움말 5=닫기
        {
            RECT r = tbi.rgrect[i];
            if (r.right <= r.left || r.bottom <= r.top)
                continue;
            if (best.right == best.left || r.left < best.left)
                best = r;
        }
        if (best.right > best.left)
        {
            int w = best.right - best.left;
            out->left = best.left - w;
            out->right = best.left;
            out->top = best.top;
            out->bottom = best.bottom;
            return TRUE;
        }
    }

    // 예비 계산: 창/클라이언트 차이로 캡션 높이를 구하고 버튼 3개 폭을 빼 준다.
    RECT wr;
    POINT client = { 0, 0 };
    if (!GetWindowRect(game, &wr) || !ClientToScreen(game, &client))
        return FALSE;

    RECT cr;
    GetClientRect(game, &cr);
    int borderX = ((wr.right - wr.left) - (cr.right - cr.left)) / 2;
    int capH = client.y - wr.top - borderX;
    int w = GetSystemMetrics(SM_CXSIZE);
    if (capH <= 0 || w <= 0)
        return FALSE;

    out->right = wr.right - borderX - w * 3;
    out->left = out->right - w;
    out->top = wr.top + borderX;
    out->bottom = out->top + capH;
    return TRUE;
}

static BOOL GameHasCaption(HWND game)
{
    LONG_PTR st = GetWindowLongPtrW(game, GWL_STYLE);
    return (st & WS_CAPTION) == WS_CAPTION && (st & WS_MINIMIZE) == 0 && IsWindowVisible(game);
}

static void SyncButton(void)
{
    if (!g_btn)
        return;

    RECT r;
    if (!IsWindow(g_game) || !GameHasCaption(g_game) || !CaptionButtonRect(g_game, &r))
    {
        ShowWindow(g_btn, SW_HIDE);
        return;
    }

    SetWindowPos(g_btn, g_game, r.left, r.top, r.right - r.left, r.bottom - r.top,
                 SWP_NOACTIVATE | SWP_SHOWWINDOW);

    COLORREF c = SampleCaptionColor(&r);
    if (c != g_capColor)
    {
        g_capColor = c;
        InvalidateRect(g_btn, NULL, TRUE);
    }
}

// ── 메뉴 ─────────────────────────────────────────────────────────────────────

static void OpenMenu(void)
{
    HMENU menu = CreatePopupMenu();
    if (!menu)
        return;

    EnterCriticalSection(&g_lock);
    for (int i = 0; i < g_itemCount; i++)
        AppendMenuW(menu, MF_STRING, (UINT_PTR)g_items[i].id, g_items[i].text);
    int count = g_itemCount;
    LeaveCriticalSection(&g_lock);

    if (count == 0)
        AppendMenuW(menu, MF_STRING | MF_GRAYED, 0, L"(설치된 플러그인 없음)");
    AppendMenuW(menu, MF_SEPARATOR, 0, NULL);
    AppendMenuW(menu, MF_STRING, ABOUT_ID, L"정보");

    RECT br;
    GetWindowRect(g_btn, &br);

    // TrackPopupMenu 는 소유 창이 포그라운드가 아니면 바깥을 눌러도 안 닫힌다.
    SetForegroundWindow(g_btn);
    int cmd = (int)TrackPopupMenu(menu,
                                  TPM_RIGHTALIGN | TPM_TOPALIGN | TPM_RETURNCMD | TPM_LEFTBUTTON,
                                  br.right, br.bottom, 0, g_btn, NULL);
    PostMessageW(g_btn, WM_NULL, 0, 0);
    DestroyMenu(menu);

    Dh4MenuProc proc = NULL;
    void* user = NULL;
    if (cmd > 0 && cmd != ABOUT_ID)
    {
        EnterCriticalSection(&g_lock);
        for (int i = 0; i < g_itemCount; i++)
            if (g_items[i].id == cmd)
            {
                proc = g_items[i].proc;
                user = g_items[i].user;
                break;
            }
        LeaveCriticalSection(&g_lock);
    }

    if (g_game)
        SetForegroundWindow(g_game);

    if (cmd == ABOUT_ID)
    {
        MessageBoxW(g_game,
                    L"DK4HD 플러그인 메뉴 (MenuUtilKR)\n\n"
                    L"게임 폴더의 dinput8.dll 이 DK4HDUtil\\*.plugin 을 읽어 들입니다.\n"
                    L"타이틀바의 ≡ 버튼 또는 F12 로 이 메뉴를 엽니다.",
                    L"정보", MB_OK | MB_ICONINFORMATION);
    }
    else if (proc)
    {
        proc(user);
    }
}

// ── 버튼 창 ──────────────────────────────────────────────────────────────────

static void PaintButton(HWND hwnd)
{
    PAINTSTRUCT ps;
    HDC hdc = BeginPaint(hwnd, &ps);

    RECT rc;
    GetClientRect(hwnd, &rc);

    COLORREF cap = g_capColor == CLR_INVALID ? RGB(255, 255, 255) : g_capColor;
    COLORREF ink = IsLight(cap) ? RGB(32, 32, 32) : RGB(240, 240, 240);
    COLORREF bg = g_hover ? Blend(cap, ink, 14) : cap;

    HBRUSH back = CreateSolidBrush(bg);
    FillRect(hdc, &rc, back);
    DeleteObject(back);

    // 가운데에 막대 세 개(≡). 옆에 붙는 최소화/닫기 글리프와 굵기가 맞도록
    // 창 크기에 비례해서 그린다(고배율 화면에서도 같은 비율).
    int w = rc.right - rc.left, h = rc.bottom - rc.top;
    int barW = w / 5 < 6 ? 6 : w / 5;
    int barH = h / 18 < 1 ? 1 : h / 18;
    int gap = h / 9 < 2 ? 2 : h / 9;
    int x = rc.left + (w - barW) / 2;
    int y = rc.top + (h - (barH * 3 + gap * 2)) / 2;

    HBRUSH brush = CreateSolidBrush(ink);
    for (int i = 0; i < 3; i++)
    {
        RECT bar = { x, y, x + barW, y + barH };
        FillRect(hdc, &bar, brush);
        y += barH + gap;
    }
    DeleteObject(brush);

    EndPaint(hwnd, &ps);
}

static LRESULT CALLBACK ButtonProc(HWND hwnd, UINT msg, WPARAM wp, LPARAM lp)
{
    switch (msg)
    {
    case WM_PAINT:
        PaintButton(hwnd);
        return 0;

    case WM_ERASEBKGND:
        return 1;   // WM_PAINT 에서 전부 칠하므로 깜빡임을 막는다

    case WM_MOUSEACTIVATE:
        return MA_NOACTIVATE;   // 눌러도 게임에서 포커스를 뺏지 않는다

    case WM_MOUSEMOVE:
        if (!g_hover)
        {
            TRACKMOUSEEVENT tme = { sizeof(tme), TME_LEAVE, hwnd, 0 };
            TrackMouseEvent(&tme);
            g_hover = TRUE;
            InvalidateRect(hwnd, NULL, FALSE);
        }
        return 0;

    case WM_MOUSELEAVE:
        g_hover = FALSE;
        InvalidateRect(hwnd, NULL, FALSE);
        return 0;

    case WM_LBUTTONUP:
    case DH4_WM_OPEN:
        OpenMenu();
        return 0;

    case DH4_WM_SYNC:
        SyncButton();
        return 0;

    case WM_TIMER:
        if (wp == SYNC_TIMER)
        {
            // 게임이 창을 다시 만들면(전체 화면 전환 등) 여기서 알아채고 물러난다.
            // 워커 스레드가 새 창을 찾아 다시 붙는다.
            if (!IsWindow(g_game))
            {
                PostQuitMessage(0);
                return 0;
            }
            SyncButton();
        }
        return 0;
    }
    return DefWindowProcW(hwnd, msg, wp, lp);
}

// ── 게임 창 서브클래싱 ───────────────────────────────────────────────────────

static LRESULT CALLBACK GameProc(HWND hwnd, UINT msg, WPARAM wp, LPARAM lp)
{
    switch (msg)
    {
    case WM_WINDOWPOSCHANGED:
    case WM_SIZE:
    case WM_MOVE:
    case WM_ACTIVATE:
    case WM_NCACTIVATE:
    case WM_STYLECHANGED:
    case WM_SHOWWINDOW:
        if (g_btn)
            PostMessageW(g_btn, DH4_WM_SYNC, 0, 0);
        break;

    case WM_SYSCOMMAND:
        if ((wp & 0xFFF0) == SYSMENU_ID)
        {
            PostMessageW(g_btn, DH4_WM_OPEN, 0, 0);
            return 0;
        }
        break;

    case WM_KEYDOWN:
        if (wp == VK_F12 && g_btn)
        {
            PostMessageW(g_btn, DH4_WM_OPEN, 0, 0);
            return 0;
        }
        break;

    case WM_DESTROY:
        // 창이 없어지는 것만 알린다. 정리는 버튼 쪽 타이머가 IsWindow 로 확인한 뒤
        // 워커 스레드에서 한다(여기는 게임 스레드라 우리 창을 만지면 안 된다).
        if (g_btn)
            PostMessageW(g_btn, DH4_WM_SYNC, 0, 0);
        break;
    }
    return CallWindowProcW(g_prevProc, hwnd, msg, wp, lp);
}

// ── 게임 창 찾기 ─────────────────────────────────────────────────────────────

static BOOL CALLBACK FindGameWindow(HWND hwnd, LPARAM lp)
{
    DWORD pid = 0;
    GetWindowThreadProcessId(hwnd, &pid);
    if (pid != GetCurrentProcessId())
        return TRUE;

    wchar_t cls[128];
    if (GetClassNameW(hwnd, cls, 128) && wcscmp(cls, GAME_CLASS) == 0)
    {
        *(HWND*)lp = hwnd;
        return FALSE;
    }
    return TRUE;
}

static HWND WaitForGameWindow(void)
{
    // DK4HD_xx.exe 는 자기 자신을 한 번 다시 띄운다. 먼저 뜬 쪽(스텁)에는 창이 끝내
    // 생기지 않으므로, 못 찾으면 조용히 물러난다.
    for (int i = 0; i < 480; i++)   // 250ms * 480 = 2분
    {
        HWND h = NULL;
        EnumWindows(FindGameWindow, (LPARAM)&h);
        if (h)
            return h;
        Sleep(250);
    }
    return NULL;
}

static BOOL Attach(HWND game)
{
    g_game = game;

    WNDCLASSEXW wc;
    ZeroMemory(&wc, sizeof(wc));
    wc.cbSize = sizeof(wc);
    wc.lpfnWndProc = ButtonProc;
    wc.hInstance = g_self;
    wc.hCursor = LoadCursorW(NULL, IDC_ARROW);
    wc.lpszClassName = BTN_CLASS;
    RegisterClassExW(&wc);   // 두 번째부터는 '이미 있음'으로 실패한다. 그래도 된다.

    g_btn = CreateWindowExW(WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE,
                            BTN_CLASS, L"", WS_POPUP,
                            0, 0, 10, 10, game, NULL, g_self, NULL);
    if (!g_btn)
    {
        Dh4Log(TAG, L"버튼 창 생성 실패(오류 %lu)", GetLastError());
        return FALSE;
    }

    g_prevProc = (WNDPROC)SetWindowLongPtrW(game, GWLP_WNDPROC, (LONG_PTR)GameProc);

    HMENU sys = GetSystemMenu(game, FALSE);
    if (sys)
    {
        AppendMenuW(sys, MF_SEPARATOR, 0, NULL);
        AppendMenuW(sys, MF_STRING, SYSMENU_ID, L"플러그인 메뉴(&P)\tF12");
    }

    SetTimer(g_btn, SYNC_TIMER, 250, NULL);
    g_capColor = CLR_INVALID;
    SyncButton();
    return TRUE;
}

static void Detach(void)
{
    if (g_prevProc && IsWindow(g_game))
        SetWindowLongPtrW(g_game, GWLP_WNDPROC, (LONG_PTR)g_prevProc);
    g_prevProc = NULL;

    if (g_btn && IsWindow(g_btn))
        DestroyWindow(g_btn);
    g_btn = NULL;
    g_game = NULL;
}

static DWORD WINAPI WorkerThread(LPVOID unused)
{
    (void)unused;

    // 게임이 창을 다시 만들 수도 있으므로(전체 화면 전환 등) 한 번 붙고 끝내지 않고
    // 창이 사라지면 다시 찾아 붙는다.
    for (;;)
    {
        HWND game = WaitForGameWindow();
        if (!game)
        {
            Dh4Log(TAG, L"게임 창을 못 찾았다(pid=%lu) — 이 프로세스에서는 물러난다",
                   GetCurrentProcessId());
            return 0;
        }
        Dh4Log(TAG, L"게임 창 발견: hwnd=0x%p", game);

        if (!Attach(game))
            return 0;
        Dh4Log(TAG, L"타이틀바 버튼 준비 완료 (F12 로도 열 수 있다)");

        MSG msg;
        while (GetMessageW(&msg, NULL, 0, 0) > 0)
        {
            TranslateMessage(&msg);
            DispatchMessageW(&msg);
        }

        Detach();
        Dh4Log(TAG, L"게임 창이 사라졌다 — 새 창을 기다린다");
    }
}

// ── 내보내는 API ─────────────────────────────────────────────────────────────

__declspec(dllexport) int WINAPI Dh4MenuAddItem(const wchar_t* text, Dh4MenuProc proc, void* user)
{
    if (!text || !proc)
        return 0;

    EnterCriticalSection(&g_lock);
    int id = 0;
    if (g_itemCount < (int)(sizeof(g_items) / sizeof(g_items[0])))
    {
        MenuItem* it = &g_items[g_itemCount++];
        id = it->id = g_nextId++;
        wcsncpy(it->text, text, 63);
        it->text[63] = 0;
        it->proc = proc;
        it->user = user;
    }
    LeaveCriticalSection(&g_lock);

    Dh4Log(TAG, id ? L"메뉴 항목 등록: %s" : L"메뉴 항목 등록 실패(가득 참): %s", text);
    return id;
}

__declspec(dllexport) void WINAPI Dh4MenuRemoveItem(int id)
{
    EnterCriticalSection(&g_lock);
    for (int i = 0; i < g_itemCount; i++)
        if (g_items[i].id == id)
        {
            for (int j = i; j < g_itemCount - 1; j++)
                g_items[j] = g_items[j + 1];
            g_itemCount--;
            break;
        }
    LeaveCriticalSection(&g_lock);
}

__declspec(dllexport) HWND WINAPI Dh4MenuGetGameWindow(void)
{
    return g_game;
}

BOOL WINAPI DllMain(HINSTANCE inst, DWORD reason, LPVOID reserved)
{
    (void)reserved;
    if (reason == DLL_PROCESS_ATTACH)
    {
        g_self = inst;
        DisableThreadLibraryCalls(inst);
        InitializeCriticalSection(&g_lock);
        HANDLE t = CreateThread(NULL, 0, WorkerThread, NULL, 0, NULL);
        if (t)
            CloseHandle(t);
    }
    return TRUE;
}
