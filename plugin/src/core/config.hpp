#pragma once

#include "codepage.hpp"
#include "ini.hpp"
#include "logging.hpp"

#include <string>
#include <vector>

namespace hota_twitch
{

/// Everything the streamer sets in `hota-twitch.ini` next to the DLL.
struct Config
{
    std::string backendUrl = "https://hota.smyrcu.net";
    std::string token;
    std::string logFile = "hota-twitch.log";
    LogLevel logLevel = LogLevel::Info;
    Codepage codepage = Codepage::Windows1252;
};

/// Fills `out` from the file. Values that cannot be read keep their default and are described
/// in `warnings`. Returns false when no token is configured: without one the backend rejects
/// every post, so the plugin stays idle instead of hammering it.
bool configFromIni(const IniDocument& ini, Config& out, std::vector<std::string>& warnings);

} // namespace hota_twitch
