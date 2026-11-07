# DK4HD 플러그인 (게임 내 햄버거 메뉴)

『대항해시대 IV HD』(DK4HD)에 cds95-mod 와 같은 방식의 플러그인 구조를 붙인 것입니다.
게임 폴더에 프록시 DLL 하나를 두면, 그 DLL 이 `DK4HDUtil\*.plugin` 을 전부 불러들이고,
게임 창 **타이틀바에 햄버거(≡) 버튼**이 생겨 플러그인들이 등록한 기능을 게임 실행 중에
열 수 있습니다.

```
게임폴더/
    DK4HD_kr.exe
    dinput8.dll                로더(프록시). 이 파일이 있으면 설치된 것이다
    DK4HDUtil/
        MenuUtilKR.plugin      타이틀바 햄버거 메뉴 + 메뉴 등록 API
        KeyMapKR.plugin        키 매핑(GetAsyncKeyState 후킹)
        WorldMapKR.plugin      R 키 세계지도(게임 타일 + 항구 97곳)
        SampleUtilKR.plugin    예제 플러그인
        keymap.ini             키 매핑 설정(처음 실행 때 기본값으로 만들어진다)
        loader.log             로드 기록(문제 생겼을 때 여기부터 본다)
        plugins/<만든이>/*.plugin   폴더를 나눠도 로더가 찾아 준다
```

## 왜 dinput8.dll 인가

`DK4HD_kr.exe` 의 임포트 테이블을 뜯어 보면 `DINPUT8.dll` 에서 쓰는 함수가
`DirectInput8Create` **하나뿐**입니다. dinput8 은 KnownDLLs 가 아니라서 게임 폴더의
DLL 이 시스템 것보다 먼저 로드되므로, 그 자리에 끼어들어 호출을 시스템
`C:\Windows\System32\dinput8.dll` 로 그대로 넘겨주기만 하면 게임은 아무 것도 눈치채지
못합니다. cds95-mod 의 `ddraw.dll`(DDrawWrapper) 과 같은 수법입니다.

d3d9/d3d11 프록시도 가능하지만 렌더링 경로에 끼어드는 만큼 사고 확률이 높아 쓰지
않았습니다. `version.dll`, `xinput1_3.dll` 도 후보이고, 필요하면 `Dh4Loader` 의 출력
이름만 바꾸면 됩니다(`xinput1_3` 은 서수 export 라 넘겨주는 코드가 더 필요합니다).

## 게임 쪽 확인된 사실

| 항목 | 값 |
| --- | --- |
| 프로세스 | **x64** (플러그인도 반드시 x64. cds95 는 x86 이었다) |
| 창 클래스 | `KT_DK4_PK_HD_WINDOW_NAME` |
| 창 스타일(창 모드) | `0x94CA0000` = WS_POPUP\|WS_VISIBLE\|WS_CAPTION\|WS_SYSMENU\|WS_MINIMIZEBOX — **진짜 타이틀바가 있다** |
| 메뉴바 | 없음(`GetMenu` = 0) |
| 화면 설정 | `HKCU\SOFTWARE\KOEITECMO\DK4HD\System Config` 의 `SCREEN_MODE`(0=창, 1=전체) |
| 실행 구조 | exe 가 자기 자신을 한 번 다시 띄운다. 먼저 뜬 쪽에는 창이 생기지 않으므로 플러그인은 창을 못 찾으면 조용히 물러나야 한다 |

## 빌드

Visual Studio 2022(C++ 데스크톱 워크로드)와 CMake 가 필요합니다. 후킹에 쓰는
[MinHook](https://github.com/TsudaKageyu/minhook) 은 서브모듈이라 처음 받은 뒤 한 번
초기화해야 합니다.

```powershell
git submodule update --init
cmake -S plugins-src -B plugins-src/build -A x64
cmake --build plugins-src/build --config Release
```

산출물:

```
plugins-src/build/Dh4Loader/Release/dinput8.dll
plugins-src/build/MenuUtilKR/Release/MenuUtilKR.plugin
plugins-src/build/KeyMapKR/Release/KeyMapKR.plugin
plugins-src/build/SampleUtilKR/Release/SampleUtilKR.plugin
```

> `-A x64` 를 빼면 32비트로 잡혀 CMake 가 바로 멈춥니다. 소스는 UTF-8 이라
> MSVC 에 `/utf-8` 을 넘깁니다(없으면 cp949 로 읽어 한글 주석 끝의 `\` 바이트에서
> 다음 줄이 통째로 사라집니다).

## 설치 / 제거

```powershell
$g = "<게임 폴더>"
Copy-Item plugins-src\build\Dh4Loader\Release\dinput8.dll $g
New-Item -ItemType Directory -Force "$g\DK4HDUtil"
Copy-Item plugins-src\build\*\Release\*.plugin "$g\DK4HDUtil\"
```

제거는 `dinput8.dll` 을 지우면 끝입니다(플러그인은 남아 있어도 아무 일도 하지 않습니다).
게임 폴더에 이미 다른 `ddraw.dll`/`dinput8.dll` 계열 유틸이 있으면 충돌하므로 먼저
확인하세요. 문제가 있는 플러그인은 `.plugin` 파일 하나만 빼면 나머지는 그대로 돕니다.

## 확인 방법

게임을 켜고 `DK4HDUtil\loader.log` 를 봅니다. 정상이면 이렇게 남습니다.

```
[Loader] 시작 — exe=...\DK4HD_kr.exe, pid=43660
[Loader] 플러그인 로드: MenuUtilKR.plugin
[Loader] 플러그인 로드: SampleUtilKR.plugin
[MenuUtil] 게임 창 발견: hwnd=0x...
[Loader] 시스템 dinput8.dll 로드 (C:\Windows\system32\dinput8.dll)
[MenuUtil] 타이틀바 버튼 준비 완료 (F12 로도 열 수 있다)
```

실시간으로 보려면 DebugView(`OutputDebugString`)를 써도 됩니다.
메뉴는 세 가지 방법으로 열립니다: **타이틀바 ≡ 클릭**, **F12**, **타이틀바 우클릭 →
플러그인 메뉴**.

## 햄버거 버튼을 어떻게 그리나

게임 클라이언트 영역은 D3D11 스왑체인이 쓰고 있어 건드리지 않습니다. `SetMenu` 로
메뉴바를 달면 클라이언트가 줄어들어 렌더링이 틀어지므로 그것도 하지 않습니다.

대신 게임 창을 소유자로 하는 작은 팝업 창을 캡션 위(최소화 버튼 왼쪽)에 겹쳐 띄웁니다.

* 버튼 자리는 `WM_GETTITLEBARINFOEX` 로 **실제 캡션 버튼 사각형**을 받아 그 왼쪽에
  붙입니다(고배율 DPI 에서도 어긋나지 않습니다). 실패하면 창/클라이언트 크기 차이로
  계산합니다.
* 배경색은 화면에서 캡션 픽셀을 직접 읽어 씁니다. 밝은 테마·어두운 테마·강조색 설정과
  무관하게 타이틀바에 묻어 보이고, 글리프 색은 그 밝기에 따라 자동으로 뒤집힙니다.
* 위치·표시 여부는 게임 창 서브클래싱(이동/활성/스타일 변경)과 250ms 타이머 두 가지로
  맞춥니다. 전체 화면이라 캡션이 없으면 버튼은 숨습니다.
* 게임이 창을 다시 만들면 타이머가 알아채고 새 창에 다시 붙습니다.

## KeyMapKR — 키 매핑을 exe 를 고치지 않고

런처의 `KeyMappingService` 는 `DK4HD_kr.exe` 안의 `mov ecx, <VK>` 즉시값(0x42216 등)을
직접 덮어씁니다. 그래서 `.bak` 백업이 필요하고, 오프셋이 v1.0.2 한국어판에 묶이며,
스팀판(SteamStub)은 `.text` 가 암호화돼 있어 아예 손을 못 댔습니다.

`KeyMapKR.plugin` 은 대신 MinHook 으로 USER32 의 **`GetAsyncKeyState`** 를 가로챕니다.
게임의 키 폴링 루틴이 이 함수로 키 상태를 묻는 것을 실제로 확인했습니다 — 게임을 켜자마자
폴링되는 가상키가 런처가 패치하던 자리와 정확히 겹칩니다.

```
0x25 ← / 0x27 →      돛 조타          0x26 ↑ / 0x28 ↓     배 선회
0x70~0x74 F1~F5      지도·기능키       0x60~0x69 넘패드 0~9
0x6B ADD / 0x6D SUB  넘패드 +/−        0x09 TAB, 0x10 SHIFT, 0x0D ENTER, 0x20 SPACE, 0x1B ESC
```

파일을 건드리지 않으니 백업이 필요 없고, 게임 버전·언어·스팀판을 가리지 않습니다.
설정은 `DK4HDUtil\keymap.ini` 이고, 없으면 주석이 달린 기본 파일이 자동으로 생깁니다.

```ini
Mode = Add          ; Add = 원래 키도 그대로 + 아래 키가 하나 더 먹는다
                    ; Replace = 원래 키는 무시하고 아래 키만 먹는다(exe 패치와 같은 동작)
NUMPAD7 = Q
NUMPAD9 = E
NUMPAD4 = COMMA
ADD     = R
```

왼쪽이 **게임이 원래 쓰는 키**, 오른쪽이 **실제로 누를 키**입니다. 기본값 `Add` 는 넘패드
없는 키보드에서 넘패드 기능을 글자키로 쓰려는 원래 목적에 맞춰 원래 키를 살려 둡니다.

타이틀바 `≡` 메뉴에 세 항목이 붙습니다: **키 매핑 보기 / 키 매핑 다시 읽기 /
keymap.ini 열기**. ini 를 고친 뒤 '다시 읽기' 를 누르면 게임을 껐다 켜지 않아도 바로
반영됩니다.

주의할 점:

* 후크는 `GetAsyncKeyState` 하나에만 겁니다. 게임은 `GetKeyState`/`GetKeyboardState` 도
  임포트하고 있으므로, 만약 매핑이 안 먹는 자리가 있으면 그 자리는 다른 함수로 키를
  읽는 것입니다(그때는 후크를 하나 더 걸면 됩니다).
* 매핑은 **키 단위**라 게임 전체에 걸립니다. exe 패치처럼 '이 폴링 자리에서만' 바꾸는
  것이 아닙니다. `Add` 모드에서는 원래 키가 그대로 살아 있어 문제가 되지 않습니다.
* 런처의 [키설정] 으로 이미 exe 를 패치해 두었다면 게임이 폴링하는 VK 자체가 바뀐
  상태입니다. 플러그인만 쓰려면 먼저 런처에서 기본값으로 되돌리거나 `.bak` 를 복구하세요.

## WorldMapKR — R 키 세계지도

게임 중 **R** 을 누르면 게임 창 위에 세계지도 창이 뜹니다(다시 R 또는 Esc 로 닫기).
휠로 확대/축소, 드래그로 이동, 더블클릭으로 전체 보기. 동서는 이어져 있습니다.

* 지도는 게임 폴더의 `World.dat`(2500x1250 칸, 칸당 uint16 타일 번호)와
  `Chip.DK4`(G1T/BC3 512x2048, 32px 타일 16열 x 64행)를 그 자리에서 읽어 그립니다.
  배포하는 지도 데이터는 없습니다. 축소 배율에서는 타일을 2x2 평균으로 줄인 밉맵을 씁니다.
* 항구 97곳의 이름·좌표(`ports.h`)는 게임 메모리 `exe+0x42DB900`(v1.0.2 한국어판)의
  도시 구조체(132바이트, 이름 CP949 + 좌표 칸x16 정수)에서 뽑았습니다.
* 게임은 마우스·Esc 를 `GetAsyncKeyState` 로 읽어 포커스와 무관하게 받아 버리므로,
  KeyMapKR 후크가 **게임 본창이 아닌 같은 프로세스 창이 앞에 있으면 모든 키를 '안 눌림'**
  으로 돌려줍니다. 지도 플러그인 자신은 R 키를 `win32u!NtUserGetAsyncKeyState` 로 직접 읽어
  이 후크를 피합니다. 게임이 원래 R(0x52)을 폴링하지 않는 것은 로그로 확인했습니다.

## 플러그인 만드는 법

`SampleUtilKR/sample.c` 를 복사해서 시작하고, `plugins-src/CMakeLists.txt` 에
`add_subdirectory(내플러그인)` 을 추가한 뒤 폴더에 `dh4_add_plugin(내플러그인 소스.c)`
한 줄짜리 `CMakeLists.txt` 를 둡니다.

```c
#include "dh4log.h"
#include "dh4menu.h"

static void WINAPI OnClick(void* user) { /* 메뉴에서 고르면 여기로 온다 */ }

static DWORD WINAPI Worker(LPVOID p) {
    Dh4MenuAdd(L"내 기능", OnClick, NULL);
    return 0;
}

BOOL WINAPI DllMain(HINSTANCE inst, DWORD reason, LPVOID r) {
    if (reason == DLL_PROCESS_ATTACH) {
        DisableThreadLibraryCalls(inst);
        CreateThread(NULL, 0, Worker, NULL, 0, NULL);   // DllMain 에서는 스레드만 띄운다
    }
    return TRUE;
}
```

지켜야 할 것:

1. **DllMain 에서 일하지 않는다.** 로더 락 안이라 `LoadLibrary`·`MessageBox` 같은 것을
   부르면 게임이 멈출 수 있습니다. 스레드를 띄워 거기서 합니다.
2. 메뉴 콜백은 **MenuUtilKR 워커 스레드**에서 불립니다. 게임 스레드가 아니므로 게임
   내부 상태를 직접 만지려면 후킹한 함수 쪽에 플래그를 넘기거나
   `SendMessage(Dh4MenuGameWindow(), ...)` 로 게임 스레드에 태우세요.
3. 반드시 **x64** 로 빌드합니다. 32비트 DLL 은 로드 자체가 실패하고 `loader.log` 에
   오류 193 으로 남습니다.

## 다음에 할 만한 것

* **런처 연동** — 런처 [키설정] 탭이 exe 를 패치하는 대신 `keymap.ini` 를 쓰도록 바꾸면
  `.bak` 백업도, 스팀판 미지원 안내도 필요 없어집니다. 설정 탭에 '플러그인 설치/제거'
  버튼(=`dinput8.dll`·`DK4HDUtil` 복사·삭제)을 두면 배포도 쉬워집니다.
* **스팀판 확인** — SteamStub 판에서도 후킹이 먹는지 실제로 확인해 볼 것. 이론상
  `.text` 는 실행 시점에 복호화되므로 GetAsyncKeyState 후킹은 영향을 받지 않습니다.
* **다른 기능 후킹** — 세이브 자금 편집, 도시 정보 표시 같은 것도 같은 방식으로
  플러그인에 담을 수 있습니다. 게임이 어떤 키를 폴링하는지는 `keymap.ini` 의
  `LogPolledKeys=1` 로 `loader.log` 에 남길 수 있어 자리 찾기에 씁니다.
