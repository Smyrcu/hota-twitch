#include "framework.hpp"

#include "core/snapshot.hpp"

using namespace hota_twitch;

namespace
{

StateSnapshot playing()
{
    StateSnapshot state;
    state.screen = Screen::Adventure;
    state.timestamp = 1000;
    state.date = {1, 3, 1};
    state.display = {2560, 1440};

    PlayerSnapshot player;
    player.id = 0;
    player.name = "HaveFunMate";
    player.currentHero = 184;
    state.player = player;

    HeroSnapshot hero;
    hero.id = 184;
    hero.name = "Todd";
    hero.army = {{0, 13, 3}};
    state.heroes.push_back(hero);

    TownSnapshot town;
    town.id = 1;
    town.name = "New Dolere";
    town.spells[0] = {15, 27};
    state.towns.push_back(town);
    return state;
}

} // namespace

HOTA_TEST(a_state_equals_itself)
{
    const StateSnapshot state = playing();

    HOTA_CHECK(state.sameStateAs(state));
}

HOTA_TEST(only_the_clock_moving_is_not_a_change)
{
    const StateSnapshot before = playing();
    StateSnapshot after = playing();
    after.timestamp = before.timestamp + 5000;

    HOTA_CHECK(before.sameStateAs(after));
}

HOTA_TEST(a_moved_army_is_a_change)
{
    const StateSnapshot before = playing();
    StateSnapshot after = playing();
    after.heroes[0].army[0].count = 4;

    HOTA_CHECK(!before.sameStateAs(after));
}

HOTA_TEST(a_changed_screen_is_a_change)
{
    const StateSnapshot before = playing();
    StateSnapshot after = playing();
    after.screen = Screen::Town;

    HOTA_CHECK(!before.sameStateAs(after));
}

HOTA_TEST(a_new_spell_in_a_guild_is_a_change)
{
    const StateSnapshot before = playing();
    StateSnapshot after = playing();
    after.towns[0].spells[0].push_back(38);

    HOTA_CHECK(!before.sameStateAs(after));
}

HOTA_TEST(research_opening_and_closing_is_a_change)
{
    const StateSnapshot before = playing();
    StateSnapshot after = playing();
    after.towns[0].research = Research{4, 1, 55, 2};

    HOTA_CHECK(!before.sameStateAs(after));

    StateSnapshot rolled = after;
    rolled.towns[0].research->rolls = 3;

    HOTA_CHECK(!after.sameStateAs(rolled));
}

HOTA_TEST(a_hero_entering_a_town_is_a_change)
{
    const StateSnapshot before = playing();
    StateSnapshot after = playing();
    after.towns[0].visitingHero = after.heroes[0];

    HOTA_CHECK(!before.sameStateAs(after));
}

HOTA_TEST(scrolling_the_panel_is_a_change)
{
    const StateSnapshot before = playing();
    StateSnapshot after = playing();
    after.player->townListTop = 1;

    HOTA_CHECK(!before.sameStateAs(after));
}

HOTA_TEST(losing_the_game_is_a_change)
{
    const StateSnapshot before = playing();
    StateSnapshot after = playing();
    after.player.reset();

    HOTA_CHECK(!before.sameStateAs(after));
}

HOTA_TEST(clearing_a_state_leaves_nothing_of_the_previous_game)
{
    StateSnapshot state = playing();
    state.clear();

    HOTA_CHECK(state.sameStateAs(StateSnapshot{}));
    HOTA_CHECK_EQ(state.timestamp, std::int64_t{0});
}

HOTA_TEST(clearing_a_hero_leaves_an_empty_card)
{
    HeroSnapshot hero;
    hero.name = "Todd";
    hero.primary = {26, 25, 25, 26};
    hero.skills = {{7, 3}};
    hero.equipped = {{0, 12}};
    hero.backpack = {5};
    hero.army = {{0, 13, 3}};
    hero.clear();

    HOTA_CHECK(hero == HeroSnapshot{});
}

HOTA_TEST(clearing_a_town_leaves_an_empty_card)
{
    TownSnapshot town;
    town.name = "New Dolere";
    town.spells[2] = {15};
    town.research = Research{4, 1, 55, 2};
    town.garrison = {{0, 13, 1}};
    town.garrisonHero = HeroSnapshot{};
    town.clear();

    HOTA_CHECK(town == TownSnapshot{});
}
