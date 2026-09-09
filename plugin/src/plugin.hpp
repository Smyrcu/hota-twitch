#pragma once

#include "core/config.hpp"
#include "core/mailbox.hpp"
#include "core/snapshot_sink.hpp"
#include "game/hd_mod.hpp"
#include "game/hooks.hpp"
#include "game/reader.hpp"
#include "platform/file_logger.hpp"
#include "platform/poster.hpp"

#include <memory>
#include <mutex>

#include <windows.h>

namespace hota_twitch
{

/// Holds the parts together and owns their order of construction.
///
/// There is one of these and it is never destroyed. That is deliberate: the hooks stay in the
/// game's code for the life of the process, so a hook can fire at any moment, and the worker
/// can be several seconds deep in WinHTTP. Tearing that down from a static destructor means
/// joining a thread from `DLL_PROCESS_DETACH`, under the loader lock, which is how a game
/// hangs on exit. The handful of handles the process keeps until it dies cost nothing;
/// the operating system reclaims them.
class Plugin final : public SnapshotSink
{
public:
    static Plugin& instance();

    /// Reads `hota-twitch.ini` next to the DLL, opens the log and installs the hooks. Safe to
    /// call more than once: the setup happens once, and only the hook installation is retried,
    /// because the HD Mod patcher may not have existed the first time round.
    void initialise(HMODULE module);

    void publish(StateSnapshot& state) override;

private:
    Plugin() = default;
    ~Plugin() override = default;

    void setUp(HMODULE module);

    std::mutex m_mutex;
    bool m_setUp = false;
    Config m_config;
    std::unique_ptr<platform::FileLogger> m_log;
    std::unique_ptr<game::HdMod> m_hdMod;
    std::unique_ptr<game::Reader> m_reader;
    std::unique_ptr<game::Hooks> m_hooks;
    SnapshotMailbox m_mailbox;
    std::unique_ptr<platform::Poster> m_poster;
    /// The worker is started by the first snapshot rather than during setup: setup can run
    /// inside `DllMain`, where starting a thread risks deadlocking on the loader lock, while
    /// a snapshot always arrives on the game thread with no lock held.
    std::once_flag m_workerStarted;
};

} // namespace hota_twitch
