#include "framework.hpp"

#include "core/ini.hpp"

using namespace hota_twitch;

namespace
{

constexpr const char* kSample = "; the streamer's file\n"
                                "[backend]\n"
                                "url = https://hota.smyrcu.net\n"
                                "token = hts_abc\n"
                                "\n"
                                "[log]\n"
                                "file = hota-twitch.log\n"
                                "level = debug\n";

} // namespace

HOTA_TEST(ini_reads_sections_and_keys)
{
    const IniDocument ini = IniDocument::parse(kSample);

    HOTA_CHECK_EQ(std::string(ini.value("backend", "url")),
                  std::string("https://hota.smyrcu.net"));
    HOTA_CHECK_EQ(std::string(ini.value("backend", "token")), std::string("hts_abc"));
    HOTA_CHECK_EQ(std::string(ini.value("log", "level")), std::string("debug"));
}

HOTA_TEST(ini_matches_section_and_key_case_insensitively)
{
    const IniDocument ini = IniDocument::parse("[Backend]\nToken = hts_x\n");

    HOTA_CHECK_EQ(std::string(ini.value("backend", "token")), std::string("hts_x"));
    HOTA_CHECK(ini.has("BACKEND", "TOKEN"));
}

HOTA_TEST(ini_returns_the_fallback_for_missing_keys)
{
    const IniDocument ini = IniDocument::parse(kSample);

    HOTA_CHECK_EQ(std::string(ini.value("backend", "proxy", "none")), std::string("none"));
    HOTA_CHECK(!ini.has("backend", "proxy"));
}

HOTA_TEST(ini_trims_whitespace_and_keeps_value_case)
{
    const IniDocument ini = IniDocument::parse("[backend]\n   token   =   hts_MiXeD   \n");

    HOTA_CHECK_EQ(std::string(ini.value("backend", "token")), std::string("hts_MiXeD"));
}

HOTA_TEST(ini_ignores_comments_and_lines_without_a_separator)
{
    const IniDocument ini = IniDocument::parse("# comment\n; another\n[backend]\nnonsense\n"
                                              "token = hts_ok\n");

    HOTA_CHECK_EQ(std::string(ini.value("backend", "token")), std::string("hts_ok"));
}

HOTA_TEST(ini_accepts_windows_line_endings)
{
    const IniDocument ini = IniDocument::parse("[backend]\r\ntoken = hts_crlf\r\n");

    HOTA_CHECK_EQ(std::string(ini.value("backend", "token")), std::string("hts_crlf"));
}

HOTA_TEST(ini_keeps_a_value_containing_an_equals_sign)
{
    const IniDocument ini = IniDocument::parse("[backend]\nurl = https://x/?a=b\n");

    HOTA_CHECK_EQ(std::string(ini.value("backend", "url")), std::string("https://x/?a=b"));
}

HOTA_TEST(ini_reads_a_file_without_a_trailing_newline)
{
    const IniDocument ini = IniDocument::parse("[backend]\ntoken = hts_tail");

    HOTA_CHECK_EQ(std::string(ini.value("backend", "token")), std::string("hts_tail"));
}
