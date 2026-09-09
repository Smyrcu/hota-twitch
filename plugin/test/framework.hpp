#pragma once

#include <sstream>
#include <string>
#include <vector>

namespace hota_twitch::test
{

using TestBody = void (*)();

struct Case
{
    const char* name;
    TestBody run;
};

std::vector<Case>& registry();

/// Aborts the running case with a failure message; the runner reports it and carries on.
[[noreturn]] void fail(const char* file, int line, const std::string& message);

struct Registrar
{
    Registrar(const char* name, TestBody body);
};

template<typename T>
std::string describe(const T& value)
{
    std::ostringstream stream;
    stream << value;
    return stream.str();
}

inline std::string describe(bool value)
{
    return value ? "true" : "false";
}

} // namespace hota_twitch::test

#define HOTA_TEST(name)                                                                        \
    static void name();                                                                        \
    static const ::hota_twitch::test::Registrar hota_registrar_##name(#name, &name);           \
    static void name()

#define HOTA_CHECK(expression)                                                                 \
    do                                                                                         \
    {                                                                                          \
        if (!(expression))                                                                     \
        {                                                                                      \
            ::hota_twitch::test::fail(__FILE__, __LINE__, "expected " #expression);            \
        }                                                                                      \
    } while (false)

#define HOTA_CHECK_EQ(actual, expected)                                                        \
    do                                                                                         \
    {                                                                                          \
        const auto& hotaActual = (actual);                                                     \
        const auto& hotaExpected = (expected);                                                 \
        if (!(hotaActual == hotaExpected))                                                     \
        {                                                                                      \
            ::hota_twitch::test::fail(__FILE__, __LINE__,                                      \
                                      #actual " is " +                                         \
                                          ::hota_twitch::test::describe(hotaActual) +          \
                                          ", expected " +                                      \
                                          ::hota_twitch::test::describe(hotaExpected));        \
        }                                                                                      \
    } while (false)
