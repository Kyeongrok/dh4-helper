#pragma once
#include <windows.h>
#include <stdio.h>
#include <stdarg.h>
#include <wchar.h>

// 로더와 모든 플러그인이 같은 곳에 로그를 남긴다.
//   1) OutputDebugStringW  — DebugView 로 실시간 확인
//   2) <게임폴더>\DK4HDUtil\loader.log — 게임이 죽어도 남는 기록
//
// 게임 폴더는 exe 위치에서 구한다. 플러그인이 DK4HDUtil\plugins\<만든이>\ 에
// 있어도 로그와 데이터는 항상 한 자리(DK4HDUtil 루트)에 모인다.

static void Dh4UtilDir(wchar_t* out, size_t cch)
{
    wchar_t exe[MAX_PATH];
    GetModuleFileNameW(NULL, exe, MAX_PATH);
    wchar_t* slash = wcsrchr(exe, L'\\');
    if (slash) *slash = 0;
    _snwprintf(out, cch, L"%s\\DK4HDUtil", exe);
    out[cch - 1] = 0;
}

static void Dh4Log(const wchar_t* tag, const wchar_t* fmt, ...)
{
    wchar_t body[1024];
    va_list ap;
    va_start(ap, fmt);
    _vsnwprintf(body, 1024, fmt, ap);
    va_end(ap);
    body[1023] = 0;

    wchar_t line[1200];
    SYSTEMTIME t;
    GetLocalTime(&t);
    _snwprintf(line, 1200, L"[%02d:%02d:%02d.%03d][%s] %s\r\n",
               t.wHour, t.wMinute, t.wSecond, t.wMilliseconds, tag, body);
    line[1199] = 0;
    OutputDebugStringW(line);

    wchar_t dir[MAX_PATH], path[MAX_PATH];
    Dh4UtilDir(dir, MAX_PATH);
    _snwprintf(path, MAX_PATH, L"%s\\loader.log", dir);
    path[MAX_PATH - 1] = 0;

    // 프로세스가 둘(런처 스텁 + 본체)이라 같은 파일을 함께 연다. 쓰기 공유로 열고
    // 끝에 붙이기만 하므로 줄이 섞이지 않는다.
    HANDLE h = CreateFileW(path, FILE_APPEND_DATA, FILE_SHARE_READ | FILE_SHARE_WRITE,
                           NULL, OPEN_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
    if (h == INVALID_HANDLE_VALUE)
        return;

    char utf8[2400];
    int n = WideCharToMultiByte(CP_UTF8, 0, line, -1, utf8, sizeof(utf8), NULL, NULL);
    if (n > 1)
    {
        DWORD written;
        WriteFile(h, utf8, (DWORD)(n - 1), &written, NULL);
    }
    CloseHandle(h);
}
