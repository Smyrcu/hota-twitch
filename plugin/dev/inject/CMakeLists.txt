add_executable(inject inject.cpp)

if(MSVC)
    target_compile_options(inject PRIVATE /W4 /WX /permissive-)
else()
    target_compile_options(inject PRIVATE -Wall -Wextra -Werror)
    target_link_options(inject PRIVATE -static -static-libgcc -static-libstdc++)
endif()
