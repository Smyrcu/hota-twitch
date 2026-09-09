add_executable(hota_twitch_tests
    framework.cpp
    test_codepage.cpp
    test_config.cpp
    test_hero_rules.cpp
    test_ini.cpp
    test_json_writer.cpp
    test_mailbox.cpp
    test_post_policy.cpp
    test_serialize.cpp
    test_snapshot.cpp
    test_town_rules.cpp
)
target_link_libraries(hota_twitch_tests PRIVATE hota_twitch_core)

if(MSVC)
    target_compile_options(hota_twitch_tests PRIVATE /W4 /WX /permissive-)
else()
    target_compile_options(hota_twitch_tests PRIVATE -Wall -Wextra -Wpedantic -Werror)
endif()

add_test(NAME core COMMAND hota_twitch_tests)
