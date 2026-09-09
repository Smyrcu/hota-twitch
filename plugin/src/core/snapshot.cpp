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

} // namespace hota_twitch
