cmake_minimum_required(VERSION 3.20)

project(hota-twitch LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

# Code that knows nothing about the game or about Windows: it builds for the DLL and for the
# native test runner alike.
add_library(hota_twitch_core STATIC
    src/core/codepage.cpp
    src/core/config.cpp
    src/core/hero_rules.cpp
    src/core/ini.cpp
    src/core/json_writer.cpp
    src/core/logging.cpp
    src/core/mailbox.cpp
    src/core/serialize.cpp
    src/core/snapshot.cpp
    src/core/town_rules.cpp
)
target_include_directories(hota_twitch_core PUBLIC src)

if(MSVC)
    target_compile_options(hota_twitch_core PRIVATE /W4 /WX /permissive-)
else()
    target_compile_options(hota_twitch_core PRIVATE -Wall -Wextra -Wpedantic -Werror)
endif()

find_package(Threads REQUIRED)
target_link_libraries(hota_twitch_core PUBLIC Threads::Threads)

if(WIN32)
    add_subdirectory(external/NH3API)

    add_library(hota-twitch SHARED
        src/dllmain.cpp
        src/game/hd_mod.cpp
        src/game/hooks.cpp
        src/game/reader.cpp
        src/platform/file_logger.cpp
        src/platform/http_client.cpp
        src/platform/module_path.cpp
        src/plugin.cpp
    )
    set_target_properties(hota-twitch PROPERTIES
        PREFIX ""
        OUTPUT_NAME "hota-twitch"
    )
    target_link_libraries(hota-twitch PRIVATE hota_twitch_core nh3api winhttp)
    target_sources(hota-twitch PRIVATE src/hota-twitch.def)

    if(MSVC)
        target_compile_options(hota-twitch PRIVATE /W4 /WX /permissive-)
    else()
        target_compile_options(hota-twitch PRIVATE -Wall -Wextra -Werror)
        # The streamer copies one file next to the game, so nothing may be left to find at
        # load time: link the GCC, C++ and pthread runtimes into the DLL.
        target_link_options(hota-twitch PRIVATE
            -static -static-libgcc -static-libstdc++
            -Wl,--enable-stdcall-fixup
        )
    endif()
else()
    enable_testing()
    add_subdirectory(test)
endif()
