#pragma once

#include "core/logging.hpp"
#include "core/snapshot.hpp"
#include "core/snapshot_sink.hpp"
#include "game/reader.hpp"

namespace hota_twitch::game
{

/// Takes the snapshots, on the game thread, from hooks in the game's own code.
///
/// Two things have to be caught. The adventure map redraws while the streamer plays, which is
/// where hero and town changes show up; that hook is throttled so it costs one comparison most
/// of the time. And the screen changes when a town or a battle opens, which the adventure hook
/// cannot see because it stops being called - so the executive's manager list is hooked too,
/// and those snapshots are taken at once rather than waiting for the throttle.
class Hooks
{
public:
    Hooks(Logger& log, Reader& reader, SnapshotSink& sink);
    ~Hooks();

    Hooks(const Hooks&) = delete;
    Hooks& operator=(const Hooks&) = delete;

    /// Installs the hooks. Returns false when the HD Mod's patcher is not available, which is
    /// the case when the DLL is loaded before it.
    bool install();

    /// Reads the game and hands the result to the worker. Only ever called on the game thread.
    void snapshot();

    /// The same, but only if the throttle interval has passed since the last snapshot.
    void snapshotIfDue();

private:
    Logger& m_log;
    Reader& m_reader;
    SnapshotSink& m_sink;
    /// The snapshot being filled. It is swapped with the one the worker is done with, so the two
    /// threads keep trading buffers instead of allocating a document per tick.
    StateSnapshot m_building;
    unsigned long m_lastSnapshotTicks = 0;
    bool m_installed = false;
};

} // namespace hota_twitch::game
