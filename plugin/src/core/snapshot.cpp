#include "snapshot.hpp"

namespace hota_twitch
{

std::string_view screenName(Screen screen)
{
    switch (screen)
    {
    case Screen::Adventure:
        return "adventure";
    case Screen::Town:
        return "town";
    case Screen::Combat:
        return "combat";
    case Screen::Other:
        return "other";
    case Screen::None:
        break;
    }
    return "none";
}

void HeroSnapshot::clear()
{
    name.clear();
    primary = {};
    skills.clear();
    equipped.clear();
    backpack.clear();
    army.clear();
}

void TownSnapshot::clear()
{
    name.clear();
    for (std::vector<int>& tier : spells)
    {
        tier.clear();
    }
    research.reset();
    garrison.clear();
    garrisonHero.reset();
    visitingHero.reset();
}

void StateSnapshot::clear()
{
    screen = Screen::None;
    timestamp = 0;
    date = {};
    display = {};
    player.reset();
    heroes.clear();
    towns.clear();
}

bool StateSnapshot::sameStateAs(const StateSnapshot& other) const
{
    return screen == other.screen && date == other.date && display == other.display &&
           player == other.player && heroes == other.heroes && towns == other.towns;
}

bool operator==(const ArmySlot& a, const ArmySlot& b)
{
    return a.slot == b.slot && a.creature == b.creature && a.count == b.count;
}

bool operator==(const SkillEntry& a, const SkillEntry& b)
{
    return a.id == b.id && a.level == b.level;
}

bool operator==(const EquippedEntry& a, const EquippedEntry& b)
{
    return a.slot == b.slot && a.artifact == b.artifact;
}

bool operator==(const HeroSnapshot& a, const HeroSnapshot& b)
{
    return a.id == b.id && a.name == b.name && a.classId == b.classId &&
           a.picture == b.picture && a.level == b.level && a.experience == b.experience &&
           a.mana == b.mana && a.manaMax == b.manaMax && a.move == b.move &&
           a.moveMax == b.moveMax && a.primary == b.primary && a.skills == b.skills &&
           a.equipped == b.equipped && a.backpack == b.backpack && a.army == b.army;
}

bool operator==(const Research& a, const Research& b)
{
    return a.level == b.level && a.slot == b.slot && a.spell == b.spell && a.rolls == b.rolls;
}

bool operator==(const TownSnapshot& a, const TownSnapshot& b)
{
    return a.id == b.id && a.name == b.name && a.type == b.type && a.fort == b.fort &&
           a.hall == b.hall && a.guild == b.guild && a.spells == b.spells &&
           a.research == b.research && a.garrison == b.garrison &&
           a.garrisonHero == b.garrisonHero && a.visitingHero == b.visitingHero;
}

bool operator==(const GameDate& a, const GameDate& b)
{
    return a.day == b.day && a.week == b.week && a.month == b.month;
}

bool operator==(const Display& a, const Display& b)
{
    return a.width == b.width && a.height == b.height;
}

bool operator==(const PlayerSnapshot& a, const PlayerSnapshot& b)
{
    return a.id == b.id && a.name == b.name && a.currentHero == b.currentHero &&
           a.heroListTop == b.heroListTop && a.townListTop == b.townListTop;
}

} // namespace hota_twitch
