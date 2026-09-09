#include "config.hpp"

namespace hota_twitch
{
namespace
{

constexpr std::string_view kTokenPrefix = "hts_";

bool startsWith(std::string_view text, std::string_view prefix)
{
    return text.size() >= prefix.size() && text.compare(0, prefix.size(), prefix) == 0;
}

} // namespace

bool configFromIni(const IniDocument& ini, Config& out, std::vector<std::string>& warnings)
{
    const std::string_view url = ini.value("backend", "url", out.backendUrl);
    if (startsWith(url, "http://") || startsWith(url, "https://"))
    {
        out.backendUrl = std::string(url);
    }
    else
    {
        warnings.emplace_back("[backend] url is not an http(s) address, using " + out.backendUrl);
    }

    out.token = std::string(ini.value("backend", "token"));
    if (!out.token.empty() && !startsWith(out.token, kTokenPrefix))
    {
        warnings.emplace_back("[backend] token does not start with hts_ - copy it again from "
                              "the extension configuration page");
    }

    const std::string_view logFile = ini.value("log", "file", out.logFile);
    if (!logFile.empty())
    {
        out.logFile = std::string(logFile);
    }

    if (ini.has("log", "level") && !parseLogLevel(ini.value("log", "level"), out.logLevel))
    {
        warnings.emplace_back("[log] level is not error/warn/info/debug, using " +
                              std::string(logLevelName(out.logLevel)));
    }

    if (ini.has("text", "codepage") && !parseCodepage(ini.value("text", "codepage"), out.codepage))
    {
        warnings.emplace_back("[text] codepage is not 1250/1251/1252, using 1252");
    }

    return !out.token.empty();
}

} // namespace hota_twitch
