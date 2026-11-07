// DK4HD 플러그인 로더 — 게임 폴더에 dinput8.dll 로 놓는다.
//
// DK4HD_xx.exe 는 DINPUT8.dll 에서 DirectInput8Create 하나만 임포트한다. dinput8 은
// KnownDLLs 가 아니므로 게임 폴더의 dinput8.dll 이 시스템 것보다 먼저 로드된다.
// 우리는 그 자리에 끼어들어
//   1) DirectInput8Create 를 시스템 dinput8.dll 로 그대로 넘겨주고(게임은 눈치채지 못한다)
//   2) DK4HDUtil\*.plugin 을 전부 LoadLibrary 한다
// cds95-mod 의 ddraw.dll(DDrawWrapper) 과 같은 구조다.
//
// 주의: 이 DLL 은 게임의 정적 임포트라 프로세스 초기화 중(=로더 락 안)에 DllMain 이
// 불린다. LoadLibrary 를 DllMain 에서 직접 부르면 교착할 수 있으므로 스레드를 띄워
// 락이 풀린 뒤에 플러그인을 올린다.

#include <windows.h>
#include "dh4log.h"

#define TAG L"Loader"

static HMODULE g_self;
static HMODULE g_real;          // 시스템 dinput8.dll
static SRWLOCK g_realLock = SRWLOCK_INIT;

// ── 시스템 dinput8.dll 로 넘기기 ──────────────────────────────────────────────

static FARPROC RealProc(const char* name)
{
    AcquireSRWLockExclusive(&g_realLock);
    if (!g_real)
    {
        wchar_t path[MAX_PATH];
        UINT n = GetSystemDirectoryW(path, MAX_PATH);
        if (n && n < MAX_PATH - 16)
        {
            wcscat(path, L"\\dinput8.dll");
            g_real = LoadLibraryW(path);
            Dh4Log(TAG, L"시스템 dinput8.dll %s (%s)", g_real ? L"로드" : L"로드 실패", path);
        }
    }
    ReleaseSRWLockExclusive(&g_realLock);
    return g_real ? GetProcAddress(g_real, name) : NULL;
}

typedef HRESULT(WINAPI* PFN_DirectInput8Create)(HINSTANCE, DWORD, const IID*, void**, void*);

// 게임이 DINPUT8.dll 에서 쓰는 함수는 이것 하나뿐이라(임포트 테이블 확인) 이것만
// 넘겨준다. COM 진입점(DllGetClassObject 등)은 게임 폴더 안에서 쓰일 일이 없어
// 일부러 내보내지 않는다 — 내보내면 windows.h 의 선언과 충돌한다.
__declspec(dllexport) HRESULT WINAPI DirectInput8Create(
    HINSTANCE inst, DWORD version, const IID* riid, void** out, void* outer)
{
    PFN_DirectInput8Create fn = (PFN_DirectInput8Create)RealProc("DirectInput8Create");
    if (!fn)
        return E_FAIL;
    return fn(inst, version, riid, out, outer);
}

// ── 플러그인 로드 ────────────────────────────────────────────────────────────

static int LoadPluginsIn(const wchar_t* dir)
{
    wchar_t pattern[MAX_PATH];
    _snwprintf(pattern, MAX_PATH, L"%s\\*.plugin", dir);
    pattern[MAX_PATH - 1] = 0;

    WIN32_FIND_DATAW fd;
    HANDLE h = FindFirstFileW(pattern, &fd);
    if (h == INVALID_HANDLE_VALUE)
        return 0;

    int count = 0;
    do
    {
        if (fd.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY)
            continue;

        wchar_t path[MAX_PATH];
        _snwprintf(path, MAX_PATH, L"%s\\%s", dir, fd.cFileName);
        path[MAX_PATH - 1] = 0;

        // 이미 다른 플러그인이 먼저 올렸을 수 있다(예: MenuUtilKR). LoadLibrary 는
        // 참조 카운트만 올리므로 그대로 불러도 된다.
        HMODULE m = LoadLibraryW(path);
        if (m)
        {
            Dh4Log(TAG, L"플러그인 로드: %s", fd.cFileName);
            count++;
        }
        else
        {
            Dh4Log(TAG, L"플러그인 로드 실패(오류 %lu): %s — 32비트로 빌드했거나 의존 DLL 이 없는지 확인",
                   GetLastError(), fd.cFileName);
        }
    } while (FindNextFileW(h, &fd));

    FindClose(h);
    return count;
}

// DK4HDUtil\plugins\<만든이>\*.plugin 까지 훑는다(cds95-mod 의 폴더 배치와 같은 규칙).
static int LoadPluginFolders(const wchar_t* root)
{
    wchar_t pattern[MAX_PATH];
    _snwprintf(pattern, MAX_PATH, L"%s\\plugins\\*", root);
    pattern[MAX_PATH - 1] = 0;

    WIN32_FIND_DATAW fd;
    HANDLE h = FindFirstFileW(pattern, &fd);
    if (h == INVALID_HANDLE_VALUE)
        return 0;

    int count = 0;
    do
    {
        if (!(fd.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY))
            continue;
        if (wcscmp(fd.cFileName, L".") == 0 || wcscmp(fd.cFileName, L"..") == 0)
            continue;

        wchar_t sub[MAX_PATH];
        _snwprintf(sub, MAX_PATH, L"%s\\plugins\\%s", root, fd.cFileName);
        sub[MAX_PATH - 1] = 0;
        count += LoadPluginsIn(sub);
    } while (FindNextFileW(h, &fd));

    FindClose(h);
    return count;
}

static DWORD WINAPI LoaderThread(LPVOID unused)
{
    (void)unused;

    wchar_t exe[MAX_PATH], util[MAX_PATH];
    GetModuleFileNameW(NULL, exe, MAX_PATH);
    Dh4UtilDir(util, MAX_PATH);
    Dh4Log(TAG, L"시작 — exe=%s, pid=%lu", exe, GetCurrentProcessId());

    if (GetFileAttributesW(util) == INVALID_FILE_ATTRIBUTES)
    {
        Dh4Log(TAG, L"플러그인 폴더가 없다: %s (플러그인 없이 계속)", util);
        return 0;
    }

    // 플러그인이 같은 폴더의 다른 DLL(예: MinHook 을 동적으로 쓰는 경우)을 찾을 수 있게.
    SetDllDirectoryW(util);
    int n = LoadPluginsIn(util) + LoadPluginFolders(util);
    SetDllDirectoryW(NULL);

    Dh4Log(TAG, L"플러그인 %d 개 로드 완료", n);
    return 0;
}

BOOL WINAPI DllMain(HINSTANCE inst, DWORD reason, LPVOID reserved)
{
    (void)reserved;
    if (reason == DLL_PROCESS_ATTACH)
    {
        g_self = inst;
        DisableThreadLibraryCalls(inst);
        // 로더 락 안에서 LoadLibrary 를 부르지 않으려고 스레드로 미룬다.
        HANDLE t = CreateThread(NULL, 0, LoaderThread, NULL, 0, NULL);
        if (t)
            CloseHandle(t);
    }
    return TRUE;
}
