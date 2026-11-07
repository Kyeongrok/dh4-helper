// SampleUtilKR — 플러그인을 어떻게 쓰는지 보여 주는 최소 예제.
// 타이틀바 ≡ 메뉴에 '샘플: 게임 정보' 항목 하나를 넣는다.
//
// 새 플러그인을 만들 때 이 파일을 복사해서 시작하면 된다. 지켜야 할 것은 두 가지다.
//   1) DllMain 에서는 스레드만 띄운다. LoadLibrary·MessageBox 같은 것을 DllMain 에서
//      부르면 로더 락에 걸려 게임이 멈출 수 있다.
//   2) 메뉴 콜백은 이 플러그인의 스레드가 아니라 MenuUtilKR 워커 스레드에서 불린다.

#include <windows.h>
#include "dh4log.h"
#include "dh4menu.h"

#define TAG L"SampleUtil"

static void WINAPI OnShowInfo(void* user)
{
    (void)user;

    wchar_t exe[MAX_PATH];
    GetModuleFileNameW(NULL, exe, MAX_PATH);

    HWND game = Dh4MenuGameWindow();
    RECT r = { 0, 0, 0, 0 };
    if (game)
        GetClientRect(game, &r);

    wchar_t msg[MAX_PATH + 200];
    _snwprintf(msg, MAX_PATH + 200,
               L"실행 파일\n%s\n\n창 핸들: 0x%p\n클라이언트 크기: %ld x %ld\n프로세스 id: %lu",
               exe, game, r.right - r.left, r.bottom - r.top, GetCurrentProcessId());
    msg[MAX_PATH + 199] = 0;

    MessageBoxW(game, msg, L"샘플 플러그인", MB_OK | MB_ICONINFORMATION);
}

static DWORD WINAPI WorkerThread(LPVOID unused)
{
    (void)unused;
    int id = Dh4MenuAdd(L"샘플: 게임 정보", OnShowInfo, NULL);
    Dh4Log(TAG, L"메뉴 등록 %s (id=%d)", id ? L"성공" : L"실패", id);
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
