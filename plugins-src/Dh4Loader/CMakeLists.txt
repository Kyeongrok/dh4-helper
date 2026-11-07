# 결과물은 dinput8.dll — 게임 폴더(DK4HD_xx.exe 옆)에 둔다.
add_library(Dh4Loader SHARED dllmain.c)
set_target_properties(Dh4Loader PROPERTIES
    PREFIX ""
    OUTPUT_NAME "dinput8")
target_link_libraries(Dh4Loader PRIVATE dh4api)
