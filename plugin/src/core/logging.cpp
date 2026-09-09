#include "logging.hpp"

#include <algorithm>
#include <array>
#include <cctype>
#include <utility>

namespace hota_twitch
{
namespace
{

constexpr std::array<std::pair<std::string_view, LogLevel>, 4> kLevels = {{
    {"error", LogLevel::Error},
    {"warn", LogLevel::Warn},
    {"info", LogLevel::Info},
    {"debug", LogLevel::Debug},
}};

bool equalsIgnoreCase(std::string_view left, std::string_view right)
{
    return left.size() == right.size() &&
           std::equal(left.begin(), left.end(), right.begin(), [](char a, char b) {
               return std::tolower(static_cast<unsigned char>(a)) ==
                      std::tolower(static_cast<unsigned char>(b));
           });
}

} // namespace

bool parseLogLevel(std::string_view text, LogLevel& out)
{
    for (const auto& [name, level] : kLevels)
    {
        if (equalsIgnoreCase(text, name))
        {
            out = level;
            return true;
        }
    }
    return false;
}

std::string_view logLevelName(LogLevel level)
{
    for (const auto& [name, candidate] : kLevels)
    {
        if (candidate == level)
        {
            return name;
        }
    }
    return "info";
}

} // namespace hota_twitch
