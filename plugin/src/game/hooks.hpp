#pragma once

#include "core/logging.hpp"
#include "core/snapshot.hpp"
#include "core/snapshot_sink.hpp"
#include "game/reader.hpp"

#include <atomic>

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

    /// Asks for a snapshot at the next hooked call, whatever the throttle and the screen gate
    /// would otherwise decide. The plugin can be set up long after a game has been loaded, and
    /// then there is a state worth sending before anything in the game changes. This is the
    /// only method safe to call from a thread other than the game's.
    void requestSnapshot();

    /// Reads the game and hands the result to the worker. Only ever called on the game thread.
    void snapshot();

    /// The same, but only if the throttle interval has passed since the last snapshot.
    void snapshotIfDue();

    /// For the manager hooks: a full read costs real time on the game thread, and the game
    /// pushes and pops a manager for every message box and dialog. Only a screen the consumer
    /// would see differently is worth one.
    void snapshotIfScreenChanged();

private:
    /// Whether a snapshot has been asked for and not yet taken.
    bool requested() const;
    /// Puts back a request whose snapshot could not be read.
    void restoreRequest(bool servingRequest);
    /// Logs how long the game took to call a hooked function after the hooks went in.
    void reportFirstSnapshot();

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
    /// Asked for from whichever thread set the plugin up, cleared on the game thread once the
    /// snapshot has been taken.
    std::atomic<bool> m_requested{false};
    /// When the hooks went in, and whether the first snapshot since has been reported. The
    /// gap between the two is how long the game left the overlay with nothing to show.
    unsigned long m_installedTicks = 0;
    bool m_firstSnapshotReported = false;
};

} // namespace hota_twitch::game
