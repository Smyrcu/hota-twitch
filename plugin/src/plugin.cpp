#include "plugin.hpp"

#include "platform/module_path.hpp"

#include <fstream>
#include <sstream>

namespace hota_twitch
{
namespace
{

constexpr const char* kConfigFile = "hota-twitch.ini";

std::string readFile(const std::string& path)
{
    std::ifstream file(path, std::ios::binary);
    if (!file)
    {
        return {};
    }
    std::ostringstream contents;
    contents << file.rdbuf();
    return contents.str();
}

} // namespace

Plugin& Plugin::instance()
{
    static Plugin plugin;
    return plugin;
}

void Plugin::initialise(HMODULE module)
{
    const std::lock_guard<std::mutex> lock(m_mutex);
    if (!m_setUp)
    {
        setUp(module);
        m_setUp = true;
    }
    if (m_hooks != nullptr && !m_hooks->install())
    {
        m_log->warn("the HD Mod patcher is not loaded yet, so no hooks are in place; the "
                    "plugin will try again if it is initialised once the game is up");
    }
}

void Plugin::setUp(HMODULE module)
{
    const std::string directory = platform::moduleDirectory(module);
    std::vector<std::string> warnings;
    const bool usable =
        configFromIni(IniDocument::parse(readFile(directory + kConfigFile)), m_config, warnings);

    m_log = std::make_unique<platform::FileLogger>(directory + m_config.logFile,
                                                   m_config.logLevel);
    m_log->info("hota-twitch starting, configuration from " + directory + kConfigFile);
    for (const std::string& warning : warnings)
    {
        m_log->warn(warning);
    }
    if (!usable)
    {
        m_log->error("hota-twitch.ini has no [backend] token, so there is nothing to post to. "
                     "Generate one on the extension configuration page on Twitch and put it "
                     "in the file next to the DLL.");
        return;
    }

    m_hdMod = std::make_unique<game::HdMod>(*m_log);
    m_reader = std::make_unique<game::Reader>(*m_log, *m_hdMod);
    m_hooks = std::make_unique<game::Hooks>(*m_log, *m_reader, *this);
    m_poster = std::make_unique<platform::Poster>(*m_log, m_mailbox, m_config);
}

void Plugin::publish(StateSnapshot& state)
{
    std::call_once(m_workerStarted, [this] { m_poster->start(); });
    m_mailbox.publish(state);
}

} // namespace hota_twitch
