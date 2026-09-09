#include "framework.hpp"

#include "core/codepage.hpp"

using namespace hota_twitch;

HOTA_TEST(codepage_passes_ascii_through)
{
    HOTA_CHECK_EQ(toUtf8("New Dolere", Codepage::Windows1252), std::string("New Dolere"));
}

HOTA_TEST(codepage_1250_maps_central_european_letters)
{
    // 0xB3 is "l with stroke" and 0x9C is "s with acute" in Windows-1250.
    const std::string raw = "\xb3\x9c";

    HOTA_CHECK_EQ(toUtf8(raw, Codepage::Windows1250), std::string("\xc5\x82\xc5\x9b"));
}

HOTA_TEST(codepage_1251_maps_cyrillic_letters)
{
    // 0xC0 is "A" and 0xDF is "Ya" in Windows-1251.
    const std::string raw = "\xc0\xdf";

    HOTA_CHECK_EQ(toUtf8(raw, Codepage::Windows1251), std::string("\xd0\x90\xd0\xaf"));
}

HOTA_TEST(codepage_1252_maps_western_letters)
{
    // 0xE9 is "e with acute", 0x80 the euro sign.
    const std::string raw = "\xe9\x80";

    HOTA_CHECK_EQ(toUtf8(raw, Codepage::Windows1252), std::string("\xc3\xa9\xe2\x82\xac"));
}

HOTA_TEST(codepage_replaces_bytes_the_page_leaves_undefined)
{
    // 0x81 is unmapped in Windows-1252.
    HOTA_CHECK_EQ(toUtf8(std::string("\x81"), Codepage::Windows1252),
                  std::string("\xef\xbf\xbd"));
}

HOTA_TEST(codepage_names_are_parsed_with_and_without_a_prefix)
{
    Codepage codepage = Codepage::Windows1252;

    HOTA_CHECK(parseCodepage("1250", codepage));
    HOTA_CHECK(codepage == Codepage::Windows1250);
    HOTA_CHECK(parseCodepage("cp1251", codepage));
    HOTA_CHECK(codepage == Codepage::Windows1251);
    HOTA_CHECK(parseCodepage("windows-1252", codepage));
    HOTA_CHECK(codepage == Codepage::Windows1252);
}

HOTA_TEST(codepage_rejects_unsupported_names)
{
    Codepage codepage = Codepage::Windows1250;

    HOTA_CHECK(!parseCodepage("utf-8", codepage));
    HOTA_CHECK(!parseCodepage("866", codepage));
    HOTA_CHECK(codepage == Codepage::Windows1250);
}

HOTA_TEST(until_nul_stops_at_the_terminator_and_at_the_capacity)
{
    const char padded[13] = {'T', 'o', 'd', 'd', '\0', 'x'};
    const char unterminated[3] = {'a', 'b', 'c'};

    HOTA_CHECK_EQ(std::string(untilNul(padded, sizeof(padded))), std::string("Todd"));
    HOTA_CHECK_EQ(std::string(untilNul(unterminated, sizeof(unterminated))), std::string("abc"));
    HOTA_CHECK(untilNul(nullptr, 8).empty());
}
