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

void logSettingsFile(Logger& log, const std::string& path, int& budget)
{
    std::ifstream file(path);
    if (!file)
    {
        log.debug("hd mod: cannot open " + path);
        return;
    }
    log.debug("hd mod: settings from " + path);
    std::string line;
    while (budget > 0 && std::getline(file, line))
    {
        if (line.empty())
        {
            continue;
        }
        log.debug("hd mod:   " + line);
        --budget;
    }
}

void logSettingsDirectory(Logger& log, const std::string& directory)
{
    WIN32_FIND_DATAA entry{};
    const HANDLE search = FindFirstFileA((directory + "\\*.ini").c_str(), &entry);
    if (search == INVALID_HANDLE_VALUE)
    {
        log.debug("hd mod: no ini files under " + directory);
        return;
    }
    int budget = kMaxSettingLinesLogged;
    do
    {
        logSettingsFile(log, directory + "\\" + entry.cFileName, budget);
    } while (budget > 0 && FindNextFileA(search, &entry) != 0);
    FindClose(search);
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

    const Patcher* const p = patcher();
    if (p == nullptr || !isHDModPresent(p))
    {
        m_log.debug("hd mod: not present, reading the resolution from the game");
        return;
    }

    const char* const version = getHDModVersionString(p);
    m_log.debug(std::string("hd mod: version ") + (version != nullptr ? version : "unknown") +
                (isHDPlusPresent(p) ? ", HD+ on" : ", HD+ off"));

    const TPoint resolution = getHDModResolution(p);
    m_log.debug("hd mod: HD.Rez " + std::to_string(resolution.x) + "x" +
                std::to_string(resolution.y) + ", game resolution field " +
                std::to_string(readAbsolute<std::int32_t>(layout::kScreenWidthAddress)) + "x" +
                std::to_string(readAbsolute<std::int32_t>(layout::kScreenHeightAddress)));

    const char* const directory = getHDModDirectory(p);
    if (directory == nullptr)
    {
        m_log.debug("hd mod: the mod does not say where _HD3_Data lives");
        return;
    }
    logSettingsDirectory(m_log, std::string(directory) + "\\Settings");
}

} // namespace hota_twitch::game
