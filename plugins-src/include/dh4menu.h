#pragma once
#include <windows.h>
#include <stdio.h>
#include <wchar.h>

// MenuUtilKR.plugin 이 게임 창 타이틀바에 붙이는 햄버거 메뉴에 항목을 등록하는 API.
//
// 플러그인 쪽에서는 Dh4MenuAdd() 한 줄만 부르면 된다. MenuUtilKR 이 아직 로드되지
// 않았으면 알아서 로드한다. 단 LoadLibrary 를 쓸 수 있으므로 **DllMain 안에서 부르면
// 안 된다**(로더 락). 워커 스레드에서 부를 것.
//
// 콜백은 메뉴를 관리하는 MenuUtilKR 워커 스레드에서 불린다. 게임 스레드가 아니므로
// 게임 내부 상태를 만지려면 후킹한 함수 쪽에 플래그를 넘기거나
// SendMessage(Dh4MenuGetGameWindow(), ...) 로 게임 스레드에 태워야 한다.

typedef void(WINAPI* Dh4MenuProc)(void* user);

typedef int(WINAPI* PFN_Dh4MenuAddItem)(const wchar_t* text, Dh4MenuProc proc, void* user);
typedef void(WINAPI* PFN_Dh4MenuRemoveItem)(int id);
typedef HWND(WINAPI* PFN_Dh4MenuGetGameWindow)(void);

static HMODULE Dh4MenuModule(void)
{
    HMODULE h = GetModuleHandleW(L"MenuUtilKR.plugin");
    if (h)
        return h;

    // 아직 안 올라왔으면 직접 올린다(로드 순서에 의존하지 않기 위해서다).
    wchar_t exe[MAX_PATH], path[MAX_PATH];
    GetModuleFileNameW(NULL, exe, MAX_PATH);
    wchar_t* slash = wcsrchr(exe, L'\\');
    if (slash) *slash = 0;
    _snwprintf(path, MAX_PATH, L"%s\\DK4HDUtil\\MenuUtilKR.plugin", exe);
    path[MAX_PATH - 1] = 0;
    return LoadLibraryW(path);
}

/// 메뉴 항목을 추가한다. 실패하면 0, 성공하면 항목 id(>0).
static int Dh4MenuAdd(const wchar_t* text, Dh4MenuProc proc, void* user)
{
    HMODULE h = Dh4MenuModule();
    if (!h)
        return 0;
    PFN_Dh4MenuAddItem fn = (PFN_Dh4MenuAddItem)GetProcAddress(h, "Dh4MenuAddItem");
    return fn ? fn(text, proc, user) : 0;
}

/// 게임 창 핸들. 아직 창이 안 만들어졌으면 NULL.
static HWND Dh4MenuGameWindow(void)
{
    HMODULE h = Dh4MenuModule();
    if (!h)
        return NULL;
    PFN_Dh4MenuGetGameWindow fn = (PFN_Dh4MenuGetGameWindow)GetProcAddress(h, "Dh4MenuGetGameWindow");
    return fn ? fn() : NULL;
}
