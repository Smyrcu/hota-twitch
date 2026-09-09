#include "framework.hpp"

#include "core/config.hpp"

using namespace hota_twitch;

namespace
{

struct Parsed
{
    Config config;
    std::vector<std::string> warnings;
    bool usable = false;
};

Parsed read(const char* contents)
{
    Parsed parsed;
    parsed.usable = configFromIni(IniDocument::parse(contents), parsed.config, parsed.warnings);
    return parsed;
}

} // namespace

HOTA_TEST(config_reads_a_complete_file)
{
    const Parsed parsed = read("[backend]\n"
                               "url = https://example.test\n"
                               "token = hts_abc\n"
                               "[log]\n"
                               "file = plugin.log\n"
                               "level = debug\n"
                               "[text]\n"
                               "codepage = 1250\n");

    HOTA_CHECK(parsed.usable);
    HOTA_CHECK_EQ(parsed.config.backendUrl, std::string("https://example.test"));
    HOTA_CHECK_EQ(parsed.config.token, std::string("hts_abc"));
    HOTA_CHECK_EQ(parsed.config.logFile, std::string("plugin.log"));
    HOTA_CHECK(parsed.config.logLevel == LogLevel::Debug);
    HOTA_CHECK(parsed.config.codepage == Codepage::Windows1250);
    HOTA_CHECK(parsed.warnings.empty());
}

HOTA_TEST(config_falls_back_to_defaults_for_omitted_settings)
{
    const Parsed parsed = read("[backend]\ntoken = hts_abc\n");

    HOTA_CHECK(parsed.usable);
    HOTA_CHECK_EQ(parsed.config.backendUrl, std::string("https://hota.smyrcu.net"));
    HOTA_CHECK_EQ(parsed.config.logFile, std::string("hota-twitch.log"));
    HOTA_CHECK(parsed.config.logLevel == LogLevel::Info);
    HOTA_CHECK(parsed.config.codepage == Codepage::Windows1252);
    HOTA_CHECK(parsed.warnings.empty());
}

HOTA_TEST(config_without_a_token_is_not_usable)
{
    const Parsed parsed = read("[backend]\nurl = https://example.test\n");

    HOTA_CHECK(!parsed.usable);
}

HOTA_TEST(config_warns_about_a_token_that_lost_its_prefix)
{
    const Parsed parsed = read("[backend]\ntoken = abc\n");

    HOTA_CHECK(parsed.usable);
    HOTA_CHECK_EQ(parsed.warnings.size(), std::size_t{1});
}

HOTA_TEST(config_keeps_the_default_url_when_the_file_gives_a_bad_one)
{
    const Parsed parsed = read("[backend]\nurl = hota.smyrcu.net\ntoken = hts_abc\n");

    HOTA_CHECK_EQ(parsed.config.backendUrl, std::string("https://hota.smyrcu.net"));
    HOTA_CHECK_EQ(parsed.warnings.size(), std::size_t{1});
}

HOTA_TEST(config_warns_about_an_unknown_level_and_codepage)
{
    const Parsed parsed = read("[backend]\ntoken = hts_abc\n"
                               "[log]\nlevel = chatty\n"
                               "[text]\ncodepage = 866\n");

    HOTA_CHECK(parsed.config.logLevel == LogLevel::Info);
    HOTA_CHECK(parsed.config.codepage == Codepage::Windows1252);
    HOTA_CHECK_EQ(parsed.warnings.size(), std::size_t{2});
}
