#pragma once

#include "core/logging.hpp"

#include <fstream>
#include <mutex>
#include <string>

namespace hota_twitch::platform
{

/// Writes the plugin's diagnostics to the file named in `hota-twitch.ini`. Nothing is ever
/// drawn in the game window: a plugin that interrupts a stream to complain is worse than one
/// that stays quiet and leaves a log behind.
class FileLogger final : public Logger
{
public:
    FileLogger(const std::string& path, LogLevel level);

    void write(LogLevel level, std::string_view message) override;

    bool enabled(LogLevel level) const override;

private:
    mutable std::mutex m_mutex;
    std::ofstream m_file;
    const LogLevel m_level;
};

} // namespace hota_twitch::platform
