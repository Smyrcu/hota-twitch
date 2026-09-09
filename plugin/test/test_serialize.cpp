#include "framework.hpp"

#include "core/serialize.hpp"

using namespace hota_twitch;

namespace
{

HeroSnapshot sampleHero()
{
    HeroSnapshot hero;
    hero.id = 184;
    hero.name = "Todd";
    hero.classId = 21;
    hero.picture = 196;
    hero.level = 22;
    hero.experience = 132486;
    hero.mana = 27;
    hero.manaMax = 390;
    hero.move = 1500;
    hero.moveMax = 1500;
    hero.primary = {26, 25, 25, 26};
    hero.skills = {{7, 3}, {19, 2}};
    hero.equipped = {{0, 12}, {18, 143}};
    hero.backpack = {5, 7, 122};
    hero.army = {{0, 13, 3}, {1, 110, 15}};
    return hero;
}

StateSnapshot sampleState()
{
    StateSnapshot state;
    state.screen = Screen::Adventure;
    state.timestamp = 1788907728157LL;
    state.date = {1, 3, 1};
    state.display = {2560, 1440};
    PlayerSnapshot player;
    player.id = 0;
    player.name = "HaveFunMate";
    player.currentHero = 184;
    player.heroListTop = 0;
    player.townListTop = 0;
    state.player = player;
    return state;
}

std::string serialize(const StateSnapshot& state, Codepage codepage = Codepage::Windows1252)
{
    std::string out;
    serializeState(state, codepage, out);
    return out;
}

bool contains(const std::string& haystack, const std::string& needle)
{
    return haystack.find(needle) != std::string::npos;
}

} // namespace

HOTA_TEST(an_empty_state_carries_the_protocol_version_and_no_game)
{
    const std::string json = serialize(StateSnapshot{});

    HOTA_CHECK(contains(json, R"("v":1)"));
    HOTA_CHECK(contains(json, R"("screen":"none")"));
    HOTA_CHECK(contains(json, R"("player":null)"));
    HOTA_CHECK(contains(json, R"("heroes":[])"));
    HOTA_CHECK(contains(json, R"("towns":[])"));
}

HOTA_TEST(the_envelope_matches_the_protocol_example)
{
    const std::string json = serialize(sampleState());

    HOTA_CHECK(contains(json, R"("ts":1788907728157)"));
    HOTA_CHECK(contains(json, R"("screen":"adventure")"));
    HOTA_CHECK(contains(json, R"("date":{"day":1,"week":3,"month":1})"));
    HOTA_CHECK(contains(json, R"("display":{"width":2560,"height":1440})"));
    HOTA_CHECK(contains(json,
                        R"("player":{"id":0,"name":"HaveFunMate","currentHero":184,)"
                        R"("heroListTop":0,"townListTop":0})"));
}

HOTA_TEST(the_ui_scale_is_left_out_of_the_display_block)
{
    HOTA_CHECK(!contains(serialize(sampleState()), "uiScale"));
}

HOTA_TEST(a_hero_matches_the_protocol_example)
{
    StateSnapshot state = sampleState();
    state.heroes.push_back(sampleHero());

    const std::string json = serialize(state);

    HOTA_CHECK(contains(json, R"("id":184,"name":"Todd","class":21,"picture":196)"));
    HOTA_CHECK(contains(json, R"("level":22,"exp":132486)"));
    HOTA_CHECK(contains(json, R"("mana":27,"manaMax":390,"move":1500,"moveMax":1500)"));
    HOTA_CHECK(contains(json, R"("primary":[26,25,25,26])"));
    HOTA_CHECK(contains(json, R"("skills":[[7,3],[19,2]])"));
    HOTA_CHECK(contains(json, R"("equipped":[[0,12],[18,143]])"));
    HOTA_CHECK(contains(json, R"("backpack":[5,7,122])"));
    HOTA_CHECK(contains(json, R"("army":[[0,13,3],[1,110,15]])"));
}

HOTA_TEST(a_town_matches_the_protocol_example)
{
    StateSnapshot state = sampleState();
    TownSnapshot town;
    town.id = 1;
    town.name = "New Dolere";
    town.type = 10;
    town.fort = 3;
    town.hall = 3;
    town.guild = 5;
    town.spells = {std::vector<int>{15, 27}, std::vector<int>{}, std::vector<int>{},
                   std::vector<int>{38, 9}, std::vector<int>{36}};
    town.research = Research{4, 1, 55, 2};
    town.garrison = {{0, 13, 1}};
    state.towns.push_back(town);

    const std::string json = serialize(state);

    HOTA_CHECK(contains(json, R"("id":1,"name":"New Dolere","type":10)"));
    HOTA_CHECK(contains(json, R"("fort":3,"hall":3,"guild":5)"));
    HOTA_CHECK(contains(json, R"("spells":[[15,27],[],[],[38,9],[36]])"));
    HOTA_CHECK(contains(json, R"("research":{"level":4,"slot":1,"spell":55,"rolls":2})"));
    HOTA_CHECK(contains(json, R"("garrison":[[0,13,1]])"));
    HOTA_CHECK(contains(json, R"("garrisonHero":null,"visitingHero":null)"));
}

HOTA_TEST(a_town_always_carries_five_spell_tiers)
{
    StateSnapshot state = sampleState();
    state.towns.emplace_back();

    HOTA_CHECK(contains(serialize(state), R"("spells":[[],[],[],[],[]])"));
}

HOTA_TEST(a_town_with_a_visiting_hero_nests_the_whole_card)
{
    StateSnapshot state = sampleState();
    TownSnapshot town;
    town.name = "Cove";
    town.visitingHero = sampleHero();
    state.towns.push_back(town);

    const std::string json = serialize(state);

    HOTA_CHECK(contains(json, R"("garrisonHero":null,"visitingHero":{"id":184)"));
    HOTA_CHECK(contains(json, R"("army":[[0,13,3],[1,110,15]]}})"));
}

HOTA_TEST(names_are_converted_from_the_configured_codepage)
{
    StateSnapshot state = sampleState();
    HeroSnapshot hero = sampleHero();
    // 0xB3 is "l with stroke" in Windows-1250.
    hero.name = "\xb3ukasz";
    state.heroes.push_back(hero);

    const std::string json = serialize(state, Codepage::Windows1250);

    HOTA_CHECK(contains(json, "\"name\":\"\xc5\x82ukasz\""));
}

HOTA_TEST(serialising_replaces_the_previous_document_in_the_buffer)
{
    std::string out = "leftover";
    serializeState(StateSnapshot{}, Codepage::Windows1252, out);

    HOTA_CHECK(out.front() == '{');
    HOTA_CHECK(!contains(out, "leftover"));
}

HOTA_TEST(every_screen_has_its_protocol_name)
{
    HOTA_CHECK_EQ(std::string(screenName(Screen::None)), std::string("none"));
    HOTA_CHECK_EQ(std::string(screenName(Screen::Adventure)), std::string("adventure"));
    HOTA_CHECK_EQ(std::string(screenName(Screen::Town)), std::string("town"));
    HOTA_CHECK_EQ(std::string(screenName(Screen::Combat)), std::string("combat"));
    HOTA_CHECK_EQ(std::string(screenName(Screen::Other)), std::string("other"));
}
