#include "hd_mod.hpp"

#include "layout.hpp"
#include "memory.hpp"

#include <nh3api/hd_mod.hpp>

#include <windows.h>

#include <fstream>
#include <string>

namespace hota_twitch::game
{
namespace
{

/// A settings file rarely runs past a few dozen lines; the cap keeps a surprising one from
/// filling the streamer's log.
constexpr int kMaxSettingLinesLogged = 200;

const Patcher* patcher()
{
    static const Patcher* const instance = GetPatcher();
    return instance;
}

void collectSettingsFile(const std::string& path, std::string& into, int& budget)
{
    std::ifstream file(path);
    if (!file)
    {
        into += "\n  cannot open " + path;
        return;
    }
    into += "\n  [" + path + "]";
    std::string line;
    while (budget > 0 && std::getline(file, line))
    {
        if (line.empty())
        {
            continue;
        }
        into += "\n    " + line;
        --budget;
    }
}

/// Gathers the settings into one string rather than logging a line at a time: this runs on the
/// game thread inside a redraw, and every write to the log is a flush to disk.
std::string collectSettings(const std::string& directory)
{
    WIN32_FIND_DATAA entry{};
    const HANDLE search = FindFirstFileA((directory + "\\*.ini").c_str(), &entry);
    if (search == INVALID_HANDLE_VALUE)
    {
        return "\n  no ini files under " + directory;
    }
    std::string collected;
    int budget = kMaxSettingLinesLogged;
    do
    {
        collectSettingsFile(directory + "\\" + entry.cFileName, collected, budget);
    } while (budget > 0 && FindNextFileA(search, &entry) != 0);
    FindClose(search);
    return collected;
}

} // namespace

HdMod::HdMod(Logger& log) : m_log(log)
{
}

Display HdMod::display() const
{
    const Patcher* const p = patcher();
    if (p != nullptr && isHDModPresent(p))
    {
        const TPoint resolution = getHDModResolution(p);
        if (resolution.x > 0 && resolution.y > 0)
        {
            return {resolution.x, resolution.y};
        }
    }
    return {readAbsolute<std::int32_t>(layout::kScreenWidthAddress),
            readAbsolute<std::int32_t>(layout::kScreenHeightAddress)};
}

void HdMod::logSettingsOnce()
{
    if (m_logged)
    {
        return;
    }
    m_logged = true;
    // Reading every settings file is only worth doing if the streamer will see the result;
    // at any other level this would be file I/O on the game thread for nothing.
    if (!m_log.enabled(LogLevel::Debug))
    {
        return;
    }

    const Patcher* const p = patcher();
    if (p == nullptr || !isHDModPresent(p))
    {
        m_log.debug("hd mod: not present, reading the resolution from the game");
        return;
    }

    const char* const version = getHDModVersionString(p);
    const TPoint resolution = getHDModResolution(p);
    std::string report = "hd mod: version ";
    report += version != nullptr ? version : "unknown";
    report += isHDPlusPresent(p) ? ", HD+ on" : ", HD+ off";
    report += "\n  HD.Rez " + std::to_string(resolution.x) + "x" + std::to_string(resolution.y);
    report += ", game resolution field " +
              std::to_string(readAbsolute<std::int32_t>(layout::kScreenWidthAddress)) + "x" +
              std::to_string(readAbsolute<std::int32_t>(layout::kScreenHeightAddress));

    const char* const directory = getHDModDirectory(p);
    if (directory == nullptr)
    {
        report += "\n  the mod does not say where _HD3_Data lives";
    }
    else
    {
        report += collectSettings(std::string(directory) + "\\Settings");
    }
    m_log.debug(report);
}

} // namespace hota_twitch::game
