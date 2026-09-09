#include "file_logger.hpp"

#include <array>
#include <cstdio>
#include <ctime>

namespace hota_twitch::platform
{
namespace
{

std::string timestamp()
{
    const std::time_t now = std::time(nullptr);
    std::tm parts{};
#if defined(_WIN32)
    localtime_s(&parts, &now);
#else
    localtime_r(&now, &parts);
#endif
    std::array<char, 24> text{};
    const std::size_t written = std::strftime(text.data(), text.size(), "%Y-%m-%d %H:%M:%S",
                                              &parts);
    return std::string(text.data(), written);
}

} // namespace

FileLogger::FileLogger(const std::string& path, LogLevel level)
    : m_file(path, std::ios::out | std::ios::app), m_level(level)
{
}

void FileLogger::write(LogLevel level, std::string_view message)
{
    const std::lock_guard<std::mutex> lock(m_mutex);
    if (level > m_level || !m_file.is_open())
    {
        return;
    }
    m_file << timestamp() << ' ' << logLevelName(level) << ' ' << message << '\n';
    m_file.flush();
}

bool FileLogger::enabled(LogLevel level) const
{
    const std::lock_guard<std::mutex> lock(m_mutex);
    return level <= m_level && m_file.is_open();
}

} // namespace hota_twitch::platform
