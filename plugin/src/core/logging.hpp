#pragma once

#include <string_view>

namespace hota_twitch
{

enum class LogLevel
{
    Error,
    Warn,
    Info,
    Debug,
};

/// Parses a level name ("error", "warn", "info", "debug"), case-insensitively.
bool parseLogLevel(std::string_view text, LogLevel& out);

std::string_view logLevelName(LogLevel level);

/// Sink the plugin writes diagnostics to. Implementations must be safe to call from the game
/// thread and from the worker thread at the same time.
class Logger
{
public:
    virtual ~Logger() = default;

    virtual void write(LogLevel level, std::string_view message) = 0;

    void error(std::string_view message) { write(LogLevel::Error, message); }
    void warn(std::string_view message) { write(LogLevel::Warn, message); }
    void info(std::string_view message) { write(LogLevel::Info, message); }
    void debug(std::string_view message) { write(LogLevel::Debug, message); }
};

} // namespace hota_twitch
