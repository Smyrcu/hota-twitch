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

    /// Which screen the game is showing. Cheap: a handful of pointer comparisons, and no
    /// game data is touched - the manager hooks use it to decide whether a full read is worth
    /// doing at all.
    Screen currentScreen() const;

private:
    /// What the last read of the panel scroll managed to do. Kept so the log carries one line
    /// per change instead of one per snapshot.
    enum class ScrollRead
    {
        Unknown,
        NotOnAdventure,
        WindowUnreadable,
        Read,
    };

    const playerData* findStreamer() const;
    void readPanelScroll(PlayerSnapshot& player);
    /// Writes the raw words behind `heroListTop` and `townListTop` at debug level. A list that
    /// cannot scroll and an offset that is not the scroll at all both end up reporting 0, and
    /// this is what tells the two apart on a running game.
    void noteScroll(ScrollRead state, const void* window, std::int32_t rawHero,
                    std::int32_t rawTown);
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
    ScrollRead m_scrollState = ScrollRead::Unknown;
    const void* m_scrollWindow = nullptr;
    std::int32_t m_scrollHeroRaw = 0;
    std::int32_t m_scrollTownRaw = 0;
};

} // namespace hota_twitch::game
