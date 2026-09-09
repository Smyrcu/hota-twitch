#include "reader.hpp"

#include "core/codepage.hpp"
#include "core/hero_rules.hpp"
#include "core/town_rules.hpp"
#include "game/layout.hpp"
#include "game/memory.hpp"

#include <nh3api/core.hpp>

#include <algorithm>
#include <chrono>

namespace hota_twitch::game
{
namespace
{

constexpr int kMaxHeroesPerPlayer = 8;
constexpr int kMaxTownsPerPlayer = 72;
constexpr int kArmySlots = 7;

std::int64_t nowInMilliseconds()
{
    using namespace std::chrono;
    return duration_cast<milliseconds>(system_clock::now().time_since_epoch()).count();
}

void readArmy(const armyGroup& source, std::vector<ArmySlot>& out)
{
    out.clear();
    for (int slot = 0; slot < kArmySlots; ++slot)
    {
        const auto index = static_cast<std::size_t>(slot);
        const std::int32_t creature = source.type[index];
        const std::int32_t count = source.amount[index];
        if (creature < 0 || count <= 0)
        {
            continue;
        }
        out.push_back({slot, creature, count});
    }
}

/// The panel lists a hero the plugin can also reach through the record it was handed, so a
/// record that describes a different hero means the accessor no longer leads where it used to.
bool describesHero(const hero& record, int heroId, int owner)
{
    return record.id == heroId && record.playerOwner == owner &&
           !untilNul(record.name.data(), record.name.size()).empty();
}

/// The value the game itself would print in the hero popup: the raw `stats` field plus every
/// artifact and bonus on top of it.
std::int32_t effectivePrimary(const hero& source, std::int32_t primary)
{
    return THISCALL_2(std::int32_t, layout::kHeroGetPrimary, &source, primary);
}

std::int32_t readListTop(const void* window, std::size_t offset)
{
    const std::int32_t top = readAt<std::int32_t>(window, offset);
    return top >= 0 && top < layout::kMaxListTop ? top : 0;
}

} // namespace

Reader::Reader(Logger& log, HdMod& hdMod) : m_log(log), m_hdMod(hdMod)
{
}

void Reader::read(StateSnapshot& state)
{
    state.clear();
    state.timestamp = nowInMilliseconds();

    if (gpGame == nullptr)
    {
        return;
    }

    m_hdMod.logSettingsOnce();
    state.display = m_hdMod.display();
    state.date = {gpGame->day, gpGame->week, gpGame->month};

    // `docs/protocol.md` ties the two together: `player` is null exactly when `screen` is
    // "none". A loaded game with no local human in it is not a game this plugin has anything
    // to say about, so it is reported as no game rather than as a screen with no player.
    const playerData* const streamer = findStreamer();
    if (streamer == nullptr)
    {
        return;
    }
    state.screen = currentScreen();

    PlayerSnapshot player;
    player.id = streamer->color;
    player.name.assign(untilNul(streamer->cName.data(), streamer->cName.size()));
    player.currentHero = streamer->currHero;
    readPanelScroll(player);
    state.player = player;

    readHeroes(*streamer, state.heroes);
    readTowns(*streamer, state.towns);
}

/// The streamer is the human playing on this machine. During the AI's turn `gpCurPlayer`
/// points at the AI, so the plugin looks the streamer up in the player table instead of
/// following the current player - the viewer should keep seeing the cards while the AI moves.
const playerData* Reader::findStreamer() const
{
    if (gpCurPlayer != nullptr && gpCurPlayer->isLocal && gpCurPlayer->isHuman)
    {
        return gpCurPlayer;
    }
    for (const playerData& candidate : gpGame->player)
    {
        if (candidate.isLocal && candidate.isHuman)
        {
            return &candidate;
        }
    }
    return nullptr;
}

/// The executive keeps the open managers as a list; the one at the tail owns the screen.
/// Its `currentManager` is only set while a message is being handled, so it is useless here.
Screen Reader::currentScreen() const
{
    if (gpExec == nullptr || gpExec->tailManager == nullptr)
    {
        return Screen::None;
    }
    const baseManager* const active = gpExec->tailManager;
    if (active == static_cast<const baseManager*>(gpAdvManager))
    {
        return Screen::Adventure;
    }
    if (active == static_cast<const baseManager*>(gpCombatManager))
    {
        return Screen::Combat;
    }
    if (active == readAbsolute<const baseManager*>(layout::kTownManagerPointer))
    {
        return Screen::Town;
    }
    return Screen::Other;
}

/// The adventure window only exists while the adventure manager owns the screen. The manager
/// hooks fire the moment it is torn down - quitting to the menu, loading a game - and
/// `gpAdvManager` can still be set with its window already freed, so the scroll is read only
/// while that screen is up, and only after the pages behind the pointer are confirmed present.
void Reader::readPanelScroll(PlayerSnapshot& player) const
{
    if (currentScreen() != Screen::Adventure || gpAdvManager == nullptr)
    {
        return;
    }
    const void* const window = gpAdvManager->advWindow;
    if (!isReadable(window, layout::kAdvWindowTopTown + sizeof(std::int32_t)))
    {
        return;
    }
    player.heroListTop = readListTop(window, layout::kAdvWindowTopHero);
    player.townListTop = readListTop(window, layout::kAdvWindowTopTown);
}

void Reader::readHeroes(const playerData& player, std::vector<HeroSnapshot>& heroes)
{
    const int count = std::clamp(static_cast<int>(player.numHeroes), 0, kMaxHeroesPerPlayer);
    for (int index = 0; index < count; ++index)
    {
        const int heroId = player.heroes[static_cast<std::size_t>(index)];
        const hero* const record = validHero(heroId, player.color);
        if (record == nullptr)
        {
            continue;
        }
        heroes.emplace_back();
        readHero(*record, heroes.back());
    }
}

const hero* Reader::validHero(int heroId, int owner)
{
    if (heroId < 0)
    {
        return nullptr;
    }
    const hero* const record = gpGame->GetHero(heroId);
    if (record != nullptr && describesHero(*record, heroId, owner))
    {
        return record;
    }
    if (!m_heroLookupBroken)
    {
        m_heroLookupBroken = true;
        m_log.error("the game's hero accessor did not return hero " + std::to_string(heroId) +
                    "; hero cards stay empty. This build of HotA moves heroes somewhere the "
                    "plugin does not follow - please report it.");
    }
    return nullptr;
}

void Reader::readHero(const hero& source, HeroSnapshot& out) const
{
    out.clear();
    out.id = source.id;
    out.name.assign(untilNul(source.name.data(), source.name.size()));
    out.classId = source.hero_class;
    out.picture = source.portrait;
    out.level = source.Level;
    out.experience = source.experience;
    out.mana = source.mana;
    out.manaMax = source.GetMaxMana();
    out.move = source.currMobility;
    out.moveMax = source.maxMobility;
    for (std::size_t index = 0; index < out.primary.size(); ++index)
    {
        out.primary[index] = effectivePrimary(source, static_cast<std::int32_t>(index));
    }

    collectSkills(source.SSLevel, source.SSOrder, out.skills);

    for (int slot = 0; slot < kArtifactSlotCount; ++slot)
    {
        const std::int32_t artifact = source.equipped[static_cast<std::size_t>(slot)].type;
        if (artifact < 0 || !isReportedArtifactSlot(slot))
        {
            continue;
        }
        out.equipped.push_back({slot, artifact});
    }

    const int carried = std::clamp(static_cast<int>(source.backpack_count), 0,
                                   static_cast<int>(source.backpack.size()));
    for (int index = 0; index < carried; ++index)
    {
        const std::int32_t artifact = source.backpack[static_cast<std::size_t>(index)].type;
        if (artifact >= 0)
        {
            out.backpack.push_back(artifact);
        }
    }

    readArmy(source.heroArmy, out.army);
}

void Reader::readTowns(const playerData& player, std::vector<TownSnapshot>& towns)
{
    const std::size_t owned = gpGame->townPool.size();
    const int count = std::clamp(static_cast<int>(player.numTowns), 0, kMaxTownsPerPlayer);
    for (int index = 0; index < count; ++index)
    {
        const int townId = player.towns[static_cast<std::size_t>(index)];
        if (townId < 0 || static_cast<std::size_t>(townId) >= owned)
        {
            continue;
        }
        const town& record = gpGame->townPool[static_cast<std::size_t>(townId)];
        if (record.id != townId)
        {
            continue;
        }
        towns.emplace_back();
        readTown(record, towns.back());
    }
}

void Reader::readTown(const town& source, TownSnapshot& out)
{
    out.clear();
    out.id = source.id;
    out.name.assign(source.cName.c_str(), source.cName.size());
    out.type = source.townType;
    out.fort = fortLevel(source.full_building_mask);
    out.hall = hallLevel(source.full_building_mask);
    out.guild = source.mageLevel;
    readGuild(source, out);
    readResearch(source, out);
    readArmy(source.town_army, out.garrison);

    const hero* const garrison = validHero(source.garrisonHero, source.playerOwner);
    if (garrison != nullptr)
    {
        out.garrisonHero.emplace();
        readHero(*garrison, *out.garrisonHero);
    }
    const hero* const visiting = validHero(source.occupyingHero, source.playerOwner);
    if (visiting != nullptr)
    {
        out.visitingHero.emplace();
        readHero(*visiting, *out.visitingHero);
    }
}

/// Only the tiers the guild has actually been built up to are reported: the spells sitting in
/// an unbuilt tier are already drawn but the streamer has not seen them either.
void Reader::readGuild(const town& source, TownSnapshot& out) const
{
    for (int tier = 0; tier < kGuildTiers; ++tier)
    {
        if (source.mageLevel < tier + 1)
        {
            continue;
        }
        const int slots =
            guildSlotCount(source.maxTownSpellAvailable[static_cast<std::size_t>(tier)]);
        for (int slot = 0; slot < slots; ++slot)
        {
            const std::int32_t spell =
                source.townSpells[static_cast<std::size_t>(tier)][static_cast<std::size_t>(slot)];
            if (spell >= 0)
            {
                out.spells[static_cast<std::size_t>(tier)].push_back(spell);
            }
        }
    }
}

void Reader::readResearch(const town& source, TownSnapshot& out) const
{
    const void* const record = readAt<const void*>(&source, layout::kTownHotaExtension);
    if ((reinterpret_cast<std::uintptr_t>(record) % 4) != 0 ||
        !isReadable(record, layout::kHotaExtensionSize))
    {
        return;
    }
    const auto states =
        readAt<GuildSlotStates>(record, layout::kHotaExtensionSlotStates);
    GuildSlot slot{};
    if (!findResearchSlot(states, slot))
    {
        return;
    }
    if (source.mageLevel < slot.tier + 1)
    {
        // Research on a tier the guild has not been built up to: the tier is reported empty,
        // so there is no slot for the consumer to point at.
        return;
    }
    GuildTierSpells tierSpells{};
    for (std::size_t index = 0; index < tierSpells.size(); ++index)
    {
        tierSpells[index] = source.townSpells[static_cast<std::size_t>(slot.tier)][index];
    }
    const int reported = reportedSpellIndex(
        tierSpells, source.maxTownSpellAvailable[static_cast<std::size_t>(slot.tier)], slot.slot);
    if (reported < 0)
    {
        return;
    }

    const std::int32_t rolls = readAt<std::int32_t>(record, layout::kHotaExtensionRollCount);
    Research research;
    research.level = slot.tier + 1;
    research.slot = reported;
    research.spell = tierSpells[static_cast<std::size_t>(slot.slot)];
    research.rolls = rolls > 0 ? rolls : 0;
    out.research = research;
}

} // namespace hota_twitch::game
