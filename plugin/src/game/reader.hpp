#pragma once

#include "core/logging.hpp"
#include "core/snapshot.hpp"
#include "game/hd_mod.hpp"

class hero;
class playerData;
class town;

namespace hota_twitch::game
{

/// Reads the state document out of the running game. Every method runs on the game thread,
/// inside a hook, and touches nothing but game memory and the snapshot it is filling.
class Reader
{
public:
    Reader(Logger& log, HdMod& hdMod);

    /// Replaces the contents of `state` with what the game holds right now.
    void read(StateSnapshot& state);

private:
    const playerData* findStreamer() const;
    Screen readScreen() const;
    void readPanelScroll(PlayerSnapshot& player) const;
    void readHeroes(const playerData& player, std::vector<HeroSnapshot>& heroes);
    void readTowns(const playerData& player, std::vector<TownSnapshot>& towns);
    /// The record the game keeps for `heroId`, or null when it does not describe that hero -
    /// which is the signal that the plugin no longer understands where heroes live.
    const hero* validHero(int heroId, int owner);
    void readHero(const hero& source, HeroSnapshot& out) const;
    void readTown(const town& source, TownSnapshot& out);
    void readGuild(const town& source, TownSnapshot& out) const;
    void readResearch(const town& source, TownSnapshot& out) const;

    Logger& m_log;
    HdMod& m_hdMod;
    /// Set once the hero accessor hands back something that is not the hero we asked for, so
    /// that the log carries one explanation rather than one per snapshot.
    bool m_heroLookupBroken = false;
};

} // namespace hota_twitch::game
