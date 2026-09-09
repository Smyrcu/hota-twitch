#pragma once

#include "core/logging.hpp"
#include "core/snapshot.hpp"

namespace hota_twitch::game
{

/// What the plugin needs from the HD Mod: the size the game window is rendered at, and a
/// one-off dump of the mod's own settings. `docs/protocol.md` also has a `display.uiScale`
/// field for the interface scale, which has not been located yet - the dump exists so that a
/// session with the game running can find it without another round of memory archaeology.
class HdMod
{
public:
    explicit HdMod(Logger& log);

    /// The game window size. Reads the HD Mod's own `HD.Rez.X` / `HD.Rez.Y` variables when the
    /// mod answers, and the game's resolution fields otherwise.
    Display display() const;

    /// Writes what is known about the HD Mod installation at debug level. Does nothing after
    /// the first call.
    void logSettingsOnce();

private:
    Logger& m_log;
    bool m_logged = false;
};

} // namespace hota_twitch::game
