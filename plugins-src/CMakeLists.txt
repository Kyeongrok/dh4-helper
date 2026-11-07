cmake_minimum_required(VERSION 3.20)
project(Dh4Plugins C)

# DK4HD_xx.exe 는 x64 프로세스다. 32비트로 빌드한 DLL 은 아예 로드되지 않는다.
if(NOT CMAKE_SIZEOF_VOID_P EQUAL 8)
    message(FATAL_ERROR
        "64비트로 구성해야 한다: cmake -S plugins-src -B plugins-src/build -A x64")
endif()

set(CMAKE_C_STANDARD 11)

# 모든 플러그인이 공유하는 헤더(로그·메뉴 API)
add_library(dh4api INTERFACE)
target_include_directories(dh4api INTERFACE ${CMAKE_CURRENT_SOURCE_DIR}/include)
target_compile_definitions(dh4api INTERFACE
    UNICODE _UNICODE WIN32_LEAN_AND_MEAN NOMINMAX _CRT_SECURE_NO_WARNINGS)
# 소스는 UTF-8 이다. 이 옵션이 없으면 MSVC 가 cp949 로 읽어, 한글 주석 끝바이트가
# 0x5C(\)인 글자에서 줄이 이어져 다음 줄이 통째로 사라진다.
if(MSVC)
    target_compile_options(dh4api INTERFACE /utf-8)
endif()

# 플러그인 하나를 만든다. 확장자는 .plugin — 로더가 이 확장자만 훑는다.
function(dh4_add_plugin name)
    add_library(${name} SHARED ${ARGN})
    set_target_properties(${name} PROPERTIES
        PREFIX ""
        SUFFIX ".plugin"
        OUTPUT_NAME "${name}")
    target_link_libraries(${name} PRIVATE dh4api)
endfunction()

# 게임 함수를 가로채는 플러그인이 쓰는 후킹 라이브러리 (정적 링크)
add_subdirectory(third_party/minhook EXCLUDE_FROM_ALL)

add_subdirectory(Dh4Loader)
add_subdirectory(MenuUtilKR)
add_subdirectory(SampleUtilKR)
add_subdirectory(KeyMapKR)
add_subdirectory(WorldMapKR)
