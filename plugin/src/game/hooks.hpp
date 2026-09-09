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
/// and a change there is snapshotted at once rather than waiting for the throttle.
///
/// Once installed the hooks stay for the life of the process: the plugin is never unloaded,
/// and there is no safe moment to take them out from under a running game.
class Hooks
{
public:
    Hooks(Logger& log, Reader& reader, SnapshotSink& sink);

    Hooks(const Hooks&) = delete;
    Hooks& operator=(const Hooks&) = delete;

    /// Installs the hooks. Returns false when the HD Mod's patcher is not available, which is
    /// the case when the DLL is loaded before it.
    bool install();

    /// Reads the game and hands the result to the worker. Only ever called on the game thread.
    void snapshot();

    /// The same, but only if the throttle interval has passed since the last snapshot.
    void snapshotIfDue();

    /// For the manager hooks: a full read costs real time on the game thread, and the game
    /// pushes and pops a manager for every message box and dialog. Only a screen the consumer
    /// would see differently is worth one.
    void snapshotIfScreenChanged();

private:
    Logger& m_log;
    Reader& m_reader;
    SnapshotSink& m_sink;
    /// The snapshot being filled. It is swapped with the one the worker is done with, so the
    /// handover itself neither copies nor allocates.
    StateSnapshot m_building;
    unsigned long m_lastSnapshotTicks = 0;
    /// The screen the game was on at the last snapshot, for the manager hooks' gate.
    Screen m_lastScreen = Screen::None;
    bool m_installed = false;
    /// Set while a snapshot is being taken. All three hooks run on the game thread and one can
    /// nest inside another - a redraw that opens a screen, say - and the second one would then
    /// be filling the same snapshot the first is halfway through.
    bool m_takingSnapshot = false;
};

} // namespace hota_twitch::game
