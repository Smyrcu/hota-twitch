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

/// Holds the parts together and owns their order of construction. There is one of these for
/// the life of the process; `initialise` may be called from either entry point, and from both.
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

    void setUp(HMODULE module);

    std::mutex m_mutex;
    bool m_setUp = false;
    Config m_config;
    /// Declaration order is destruction order reversed, and that matters here: the poster has
    /// to go first, because stopping it means signalling the mailbox and joining the worker,
    /// and the mailbox has to outlive that. Everything the worker or a hook can still reach -
    /// the mailbox, the reader, the log - is declared above it for the same reason.
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
