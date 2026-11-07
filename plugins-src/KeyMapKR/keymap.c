// KeyMapKR — 키 매핑을 exe 를 고치지 않고 실행 중에 바꾼다.
//
// 런처의 KeyMappingService 는 DK4HD_kr.exe 안의 'mov ecx, <VK>' 즉시값(0x42216 등)을
// 직접 덮어썼다. 그래서 (1) .bak 백업이 필요하고 (2) 오프셋이 v1.0.2 한국어판에 묶이며
// (3) 스팀판(SteamStub)은 .text 가 암호화돼 있어 아예 손을 못 댔다.
//
// 여기서는 MinHook 으로 USER32 의 GetAsyncKeyState 를 가로챈다. 게임의 키 폴링 루틴이
// 이 함수로 키 상태를 묻기 때문에, "게임이 A 키 상태를 물으면 B 키 상태도 같이 봐 준다"
// 로 매핑이 끝난다. 파일을 건드리지 않으니 백업이 필요 없고, 게임 버전·언어·스팀판을
// 가리지 않는다.
//
// 기본은 '더하기(Add)'다. 원래 키는 그대로 살아 있고 지정한 키가 하나 더 먹는다.
// 넘패드 없는 키보드에서 넘패드 기능을 글자키로 쓰려는 것이 원래 목적이므로 이 쪽이
// 자연스럽다. exe 패치와 똑같이 '바꿔치기'를 원하면 ini 에서 Mode=Replace 로 둔다.

#include <windows.h>
#include <shellapi.h>
#include <stdio.h>
#include <string.h>
#include <ctype.h>
#include <stdlib.h>
#include "MinHook.h"
#include "dh4log.h"
#include "dh4menu.h"

#define TAG L"KeyMap"

typedef SHORT(WINAPI* PFN_GetAsyncKeyState)(int);

static PFN_GetAsyncKeyState g_orig;
static BYTE g_map[256];        // g_map[게임이 묻는 VK] = 대신 볼 VK (0 이면 매핑 없음)
static BOOL g_replace;         // TRUE 면 원래 키를 무시하고 대체 키만 본다
static BOOL g_logPolled = TRUE;
static BYTE g_seen[256];       // 폴링된 적 있는 VK (분석용 로그를 한 번씩만 남기려고)
static int  g_seenLogged;
static int  g_hitLogged;
static SRWLOCK g_mapLock = SRWLOCK_INIT;

// ── 후크 ─────────────────────────────────────────────────────────────────────

/// 이 프로세스의 '게임 본창이 아닌' 창(세계지도, ≡ 메뉴, 메시지 상자 등)이 맨 앞이면 TRUE.
/// GetAsyncKeyState 는 포커스와 상관없이 키를 돌려주므로, 그때 게임이 지도 창 위의
/// 클릭·드래그·Esc 를 자기 입력으로 받지 않도록 막는 데 쓴다.
static BOOL PluginWindowInFront(void)
{
    static HWND s_lastFg;
    static BOOL s_lastResult;

    HWND fg = GetForegroundWindow();
    if (fg == s_lastFg)
        return s_lastResult;

    BOOL result = FALSE;
    DWORD pid = 0;
    if (fg)
        GetWindowThreadProcessId(fg, &pid);
    if (pid == GetCurrentProcessId())
    {
        wchar_t cls[64];
        if (!GetClassNameW(fg, cls, 64) || wcscmp(cls, L"KT_DK4_PK_HD_WINDOW_NAME") != 0)
            result = TRUE;
    }
    s_lastFg = fg;
    s_lastResult = result;
    return result;
}

static SHORT WINAPI HookedGetAsyncKeyState(int vKey)
{
    if (vKey < 0 || vKey > 255)
        return g_orig(vKey);

    if (PluginWindowInFront())
    {
        g_orig(vKey); // '마지막 호출 이후 눌렸음' 비트를 비워 둔다(닫은 뒤 한꺼번에 먹지 않게)
        return 0;
    }

    // 게임이 어떤 키를 실제로 폴링하는지 한 번씩만 남긴다(새 기능을 찾을 때 쓴다).
    if (g_logPolled && !g_seen[vKey] && g_seenLogged < 64)
    {
        g_seen[vKey] = 1;
        g_seenLogged++;
        Dh4Log(TAG, L"게임이 폴링하는 키: 0x%02X", (unsigned)vKey);
    }

    AcquireSRWLockShared(&g_mapLock);
    BYTE alt = g_map[vKey];
    BOOL replace = g_replace;
    ReleaseSRWLockShared(&g_mapLock);

    if (!alt)
        return g_orig(vKey);

    SHORT sub = g_orig(alt);
    if (replace)
        return sub;

    SHORT self = g_orig(vKey);
    SHORT both = (SHORT)(self | sub);

    if ((both & 0x8000) && !(self & 0x8000) && g_hitLogged < 8)
    {
        g_hitLogged++;
        Dh4Log(TAG, L"매핑 적용: 게임이 묻는 0x%02X ← 눌린 키 0x%02X", (unsigned)vKey, (unsigned)alt);
    }
    return both;
}

// ── 키 이름 → VK ─────────────────────────────────────────────────────────────

typedef struct { const char* name; BYTE vk; } KeyName;

static const KeyName kKeyNames[] = {
    { "LEFT", VK_LEFT }, { "RIGHT", VK_RIGHT }, { "UP", VK_UP }, { "DOWN", VK_DOWN },
    { "SPACE", VK_SPACE }, { "ENTER", VK_RETURN }, { "RETURN", VK_RETURN },
    { "TAB", VK_TAB }, { "ESC", VK_ESCAPE }, { "ESCAPE", VK_ESCAPE },
    { "BACKSPACE", VK_BACK }, { "SHIFT", VK_SHIFT }, { "CTRL", VK_CONTROL },
    { "CONTROL", VK_CONTROL }, { "ALT", VK_MENU }, { "CAPSLOCK", VK_CAPITAL },
    { "HOME", VK_HOME }, { "END", VK_END }, { "PGUP", VK_PRIOR }, { "PGDN", VK_NEXT },
    { "INSERT", VK_INSERT }, { "DELETE", VK_DELETE },
    { "NUMPAD0", VK_NUMPAD0 }, { "NUMPAD1", VK_NUMPAD1 }, { "NUMPAD2", VK_NUMPAD2 },
    { "NUMPAD3", VK_NUMPAD3 }, { "NUMPAD4", VK_NUMPAD4 }, { "NUMPAD5", VK_NUMPAD5 },
    { "NUMPAD6", VK_NUMPAD6 }, { "NUMPAD7", VK_NUMPAD7 }, { "NUMPAD8", VK_NUMPAD8 },
    { "NUMPAD9", VK_NUMPAD9 },
    { "ADD", VK_ADD }, { "PLUS", VK_ADD }, { "SUBTRACT", VK_SUBTRACT }, { "MINUS", VK_SUBTRACT },
    { "MULTIPLY", VK_MULTIPLY }, { "DIVIDE", VK_DIVIDE }, { "DECIMAL", VK_DECIMAL },
    { "COMMA", VK_OEM_COMMA }, { "PERIOD", VK_OEM_PERIOD }, { "SEMICOLON", VK_OEM_1 },
    { "SLASH", VK_OEM_2 }, { "GRAVE", VK_OEM_3 }, { "LBRACKET", VK_OEM_4 },
    { "BACKSLASH", VK_OEM_5 }, { "RBRACKET", VK_OEM_6 }, { "QUOTE", VK_OEM_7 },
    { NULL, 0 },
};

static BYTE ParseKey(const char* s)
{
    while (*s == ' ' || *s == '\t') s++;

    size_t len = strlen(s);
    while (len && (s[len - 1] == ' ' || s[len - 1] == '\t' || s[len - 1] == '\r'))
        len--;
    if (len == 0)
        return 0;

    char buf[32];
    if (len >= sizeof(buf))
        return 0;
    for (size_t i = 0; i < len; i++)
        buf[i] = (char)toupper((unsigned char)s[i]);
    buf[len] = 0;

    if (len == 1)
    {
        char c = buf[0];
        if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
            return (BYTE)c;   // 글자·숫자 키의 VK 는 대문자 ASCII 와 같다
    }
    if (len > 2 && buf[0] == '0' && buf[1] == 'X')
    {
        unsigned v = 0;
        if (sscanf(buf + 2, "%x", &v) == 1 && v < 256)
            return (BYTE)v;
        return 0;
    }
    if (buf[0] == 'F' && len >= 2 && buf[1] >= '1' && buf[1] <= '9')
    {
        int n = atoi(buf + 1);
        if (n >= 1 && n <= 12)
            return (BYTE)(VK_F1 + n - 1);
    }
    for (const KeyName* k = kKeyNames; k->name; k++)
        if (strcmp(buf, k->name) == 0)
            return k->vk;
    return 0;
}

static const wchar_t* KeyLabel(BYTE vk, wchar_t* buf, size_t cch)
{
    for (const KeyName* k = kKeyNames; k->name; k++)
        if (k->vk == vk)
        {
            _snwprintf(buf, cch, L"%S", k->name);
            buf[cch - 1] = 0;
            return buf;
        }
    if ((vk >= 'A' && vk <= 'Z') || (vk >= '0' && vk <= '9'))
        _snwprintf(buf, cch, L"%c", (wchar_t)vk);
    else if (vk >= VK_F1 && vk <= VK_F12)
        _snwprintf(buf, cch, L"F%d", vk - VK_F1 + 1);
    else
        _snwprintf(buf, cch, L"0x%02X", (unsigned)vk);
    buf[cch - 1] = 0;
    return buf;
}

// ── 설정 파일 ────────────────────────────────────────────────────────────────

static void ConfigPath(wchar_t* out, size_t cch)
{
    wchar_t dir[MAX_PATH];
    Dh4UtilDir(dir, MAX_PATH);
    _snwprintf(out, cch, L"%s\\keymap.ini", dir);
    out[cch - 1] = 0;
}

static const char* kDefaultConfig =
    "\xEF\xBB\xBF"
    "; DK4HD 키 매핑 (KeyMapKR)\r\n"
    ";\r\n"
    "; 왼쪽 = 게임이 원래 쓰는 키, 오른쪽 = 그 대신 누를 키.\r\n"
    "; 기본값(Mode=Add)에서는 원래 키도 그대로 먹고, 여기 적은 키가 하나 더 먹는다.\r\n"
    "; Mode=Replace 로 두면 원래 키는 무시하고 적은 키만 먹는다.\r\n"
    ";\r\n"
    "; 쓸 수 있는 이름: A~Z, 0~9, F1~F12, LEFT/RIGHT/UP/DOWN, SPACE, ENTER, TAB, ESC,\r\n"
    ";   SHIFT, CTRL, ALT, HOME, END, PGUP, PGDN, INSERT, DELETE,\r\n"
    ";   NUMPAD0~NUMPAD9, ADD, SUBTRACT, MULTIPLY, DIVIDE, DECIMAL,\r\n"
    ";   COMMA, PERIOD, SEMICOLON, SLASH, GRAVE, LBRACKET, RBRACKET, QUOTE,\r\n"
    ";   또는 0x41 같은 가상키 코드.\r\n"
    ";\r\n"
    "; 고친 뒤에는 게임 타이틀바 [≡] 메뉴에서 '키 매핑 다시 읽기' 를 누르면 바로 먹는다.\r\n"
    "\r\n"
    "Mode = Add\r\n"
    "\r\n"
    "; 넘패드 없는 키보드용 보기 (앞의 ; 를 지우면 켜진다)\r\n"
    "; NUMPAD7 = Q\r\n"
    "; NUMPAD9 = E\r\n"
    "; NUMPAD1 = Z\r\n"
    "; NUMPAD3 = C\r\n"
    "; NUMPAD4 = COMMA\r\n"
    "; NUMPAD6 = PERIOD\r\n"
    "; ADD      = R\r\n"
    "; SUBTRACT = F\r\n";

static void WriteDefaultConfig(const wchar_t* path)
{
    HANDLE h = CreateFileW(path, GENERIC_WRITE, 0, NULL, CREATE_NEW, FILE_ATTRIBUTE_NORMAL, NULL);
    if (h == INVALID_HANDLE_VALUE)
        return;
    DWORD written;
    WriteFile(h, kDefaultConfig, (DWORD)strlen(kDefaultConfig), &written, NULL);
    CloseHandle(h);
    Dh4Log(TAG, L"설정 파일을 새로 만들었다: %s", path);
}

/// keymap.ini 를 읽어 매핑표를 새로 만든다. 등록된 매핑 개수를 돌려준다.
static int LoadConfig(void)
{
    wchar_t path[MAX_PATH];
    ConfigPath(path, MAX_PATH);
    if (GetFileAttributesW(path) == INVALID_FILE_ATTRIBUTES)
        WriteDefaultConfig(path);

    FILE* f = _wfopen(path, L"rb");
    if (!f)
    {
        Dh4Log(TAG, L"설정 파일을 못 읽었다: %s", path);
        return 0;
    }

    BYTE map[256];
    ZeroMemory(map, sizeof(map));
    BOOL replace = FALSE;
    int count = 0;

    char line[256];
    while (fgets(line, sizeof(line), f))
    {
        char* p = line;
        if ((unsigned char)p[0] == 0xEF && (unsigned char)p[1] == 0xBB && (unsigned char)p[2] == 0xBF)
            p += 3;   // BOM
        while (*p == ' ' || *p == '\t') p++;
        if (*p == ';' || *p == '#' || *p == '\r' || *p == '\n' || *p == 0)
            continue;

        char* eq = strchr(p, '=');
        if (!eq)
            continue;
        *eq = 0;
        char* left = p;
        char* right = eq + 1;

        // 오른쪽의 줄 끝 주석 자르기
        char* cmt = strpbrk(right, ";#\r\n");
        if (cmt) *cmt = 0;

        // 왼쪽 공백 정리
        char* end = left + strlen(left);
        while (end > left && (end[-1] == ' ' || end[-1] == '\t')) *--end = 0;
        while (*left == ' ' || *left == '\t') left++;

        if (_stricmp(left, "Mode") == 0)
        {
            char* v = right;
            while (*v == ' ' || *v == '\t') v++;
            replace = (_strnicmp(v, "Replace", 7) == 0);
            continue;
        }
        if (_stricmp(left, "LogPolledKeys") == 0)
        {
            char* v = right;
            while (*v == ' ' || *v == '\t') v++;
            g_logPolled = (*v == '1' || *v == 'y' || *v == 'Y' || *v == 't' || *v == 'T');
            continue;
        }

        BYTE from = ParseKey(left);
        BYTE to = ParseKey(right);
        if (!from || !to)
        {
            Dh4Log(TAG, L"알 수 없는 키 이름: '%S' = '%S'", left, right);
            continue;
        }
        map[from] = to;
        count++;
    }
    fclose(f);

    AcquireSRWLockExclusive(&g_mapLock);
    CopyMemory(g_map, map, sizeof(g_map));
    g_replace = replace;
    ReleaseSRWLockExclusive(&g_mapLock);

    Dh4Log(TAG, L"매핑 %d 개 적용 (모드=%s)", count, replace ? L"Replace" : L"Add");
    return count;
}

// ── 메뉴 ─────────────────────────────────────────────────────────────────────

static void AppendMappingText(wchar_t* out, size_t cch)
{
    wchar_t a[32], b[32];
    size_t used = wcslen(out);
    int shown = 0;

    AcquireSRWLockShared(&g_mapLock);
    BOOL replace = g_replace;
    for (int vk = 0; vk < 256; vk++)
    {
        if (!g_map[vk] || used + 64 >= cch)
            continue;
        _snwprintf(out + used, cch - used, L"  %s  →  %s\n",
                   KeyLabel((BYTE)vk, a, 32), KeyLabel(g_map[vk], b, 32));
        used = wcslen(out);
        shown++;
    }
    ReleaseSRWLockShared(&g_mapLock);

    if (!shown)
    {
        _snwprintf(out + used, cch - used, L"  (설정된 매핑 없음)\n");
        used = wcslen(out);
    }
    _snwprintf(out + used, cch - used, L"\n모드: %s", replace ? L"Replace (원래 키 무시)"
                                                             : L"Add (원래 키도 그대로)");
    out[cch - 1] = 0;
}

static void WINAPI OnShowMapping(void* user)
{
    (void)user;
    wchar_t path[MAX_PATH];
    ConfigPath(path, MAX_PATH);

    wchar_t msg[4096];
    _snwprintf(msg, 4096, L"설정 파일:\n%s\n\n현재 매핑\n", path);
    msg[4095] = 0;
    AppendMappingText(msg, 4096);

    MessageBoxW(Dh4MenuGameWindow(), msg, L"키 매핑", MB_OK | MB_ICONINFORMATION);
}

static void WINAPI OnReload(void* user)
{
    (void)user;
    int n = LoadConfig();

    wchar_t msg[4096];
    _snwprintf(msg, 4096, L"keymap.ini 를 다시 읽었다. 매핑 %d 개.\n\n", n);
    msg[4095] = 0;
    AppendMappingText(msg, 4096);

    MessageBoxW(Dh4MenuGameWindow(), msg, L"키 매핑 다시 읽기", MB_OK | MB_ICONINFORMATION);
}

static void WINAPI OnOpenConfig(void* user)
{
    (void)user;
    wchar_t path[MAX_PATH];
    ConfigPath(path, MAX_PATH);
    ShellExecuteW(NULL, L"open", L"notepad.exe", path, NULL, SW_SHOWNORMAL);
}

// ── 시작 ─────────────────────────────────────────────────────────────────────

static DWORD WINAPI WorkerThread(LPVOID unused)
{
    (void)unused;

    LoadConfig();

    MH_STATUS st = MH_Initialize();
    if (st != MH_OK && st != MH_ERROR_ALREADY_INITIALIZED)
    {
        Dh4Log(TAG, L"MinHook 초기화 실패 (%S)", MH_StatusToString(st));
        return 0;
    }

    st = MH_CreateHookApi(L"user32", "GetAsyncKeyState",
                          &HookedGetAsyncKeyState, (LPVOID*)&g_orig);
    if (st != MH_OK)
    {
        Dh4Log(TAG, L"GetAsyncKeyState 후크 생성 실패 (%S)", MH_StatusToString(st));
        return 0;
    }

    st = MH_EnableHook(MH_ALL_HOOKS);
    if (st != MH_OK)
    {
        Dh4Log(TAG, L"후크 활성화 실패 (%S)", MH_StatusToString(st));
        return 0;
    }
    Dh4Log(TAG, L"GetAsyncKeyState 후킹 완료 — exe 는 건드리지 않는다");

    Dh4MenuAdd(L"키 매핑 보기", OnShowMapping, NULL);
    Dh4MenuAdd(L"키 매핑 다시 읽기", OnReload, NULL);
    Dh4MenuAdd(L"keymap.ini 열기", OnOpenConfig, NULL);
    return 0;
}

BOOL WINAPI DllMain(HINSTANCE inst, DWORD reason, LPVOID reserved)
{
    (void)reserved;
    if (reason == DLL_PROCESS_ATTACH)
    {
        DisableThreadLibraryCalls(inst);
        HANDLE t = CreateThread(NULL, 0, WorkerThread, NULL, 0, NULL);
        if (t)
            CloseHandle(t);
    }
    return TRUE;
}
