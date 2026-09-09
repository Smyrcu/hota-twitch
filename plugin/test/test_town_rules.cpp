#include "framework.hpp"

#include "core/town_rules.hpp"

using namespace hota_twitch;

namespace
{

constexpr std::uint64_t bit(int index)
{
    return std::uint64_t{1} << index;
}

GuildSlotStates statesWithRealSlots()
{
    // A town whose guild has the usual 5/4/3/2/1 slots: every real slot holds a spell.
    GuildSlotStates states{};
    const int perTier[kGuildTiers] = {5, 4, 3, 2, 1};
    for (int tier = 0; tier < kGuildTiers; ++tier)
    {
        for (int slot = 0; slot < perTier[tier]; ++slot)
        {
            states[static_cast<std::size_t>(tier * kGuildSlotsPerTier + slot)] = 1;
        }
    }
    return states;
}

} // namespace

HOTA_TEST(fort_level_follows_the_highest_built_wall)
{
    HOTA_CHECK_EQ(fortLevel(0), 0);
    HOTA_CHECK_EQ(fortLevel(bit(7)), 1);
    HOTA_CHECK_EQ(fortLevel(bit(7) | bit(8)), 2);
    HOTA_CHECK_EQ(fortLevel(bit(7) | bit(8) | bit(9)), 3);
}

HOTA_TEST(fort_level_ignores_unrelated_buildings)
{
    HOTA_CHECK_EQ(fortLevel(bit(0) | bit(17) | bit(30)), 0);
}

HOTA_TEST(hall_level_counts_the_village_hall_as_level_zero)
{
    HOTA_CHECK_EQ(hallLevel(bit(10)), 0);
    HOTA_CHECK_EQ(hallLevel(bit(10) | bit(11)), 1);
    HOTA_CHECK_EQ(hallLevel(bit(10) | bit(11) | bit(12)), 2);
    HOTA_CHECK_EQ(hallLevel(bit(10) | bit(11) | bit(12) | bit(13)), 3);
}

HOTA_TEST(hall_level_of_a_town_without_any_hall_bit_is_zero)
{
    HOTA_CHECK_EQ(hallLevel(0), 0);
}

HOTA_TEST(guild_slot_count_is_clamped_to_the_row_width)
{
    HOTA_CHECK_EQ(guildSlotCount(5), 5);
    HOTA_CHECK_EQ(guildSlotCount(6), 6);
    HOTA_CHECK_EQ(guildSlotCount(9), 6);
    HOTA_CHECK_EQ(guildSlotCount(0), 0);
    HOTA_CHECK_EQ(guildSlotCount(-1), 0);
}

HOTA_TEST(research_slot_is_found_where_hota_marks_it)
{
    GuildSlotStates states = statesWithRealSlots();
    states[3 * kGuildSlotsPerTier + 1] = 2;

    GuildSlot found{};
    HOTA_CHECK(findResearchSlot(states, found));
    HOTA_CHECK_EQ(found.tier, 3);
    HOTA_CHECK_EQ(found.slot, 1);
}

HOTA_TEST(no_research_slot_when_every_slot_is_ordinary)
{
    GuildSlot found{};

    HOTA_CHECK(!findResearchSlot(statesWithRealSlots(), found));
}

HOTA_TEST(states_outside_the_plausible_range_mean_this_is_not_a_hota_record)
{
    GuildSlotStates states = statesWithRealSlots();
    states[2] = 2;
    states[7] = 4096;

    GuildSlot found{};
    HOTA_CHECK(!findResearchSlot(states, found));
}

HOTA_TEST(a_negative_state_also_rejects_the_record)
{
    GuildSlotStates states = statesWithRealSlots();
    states[0] = -1;

    GuildSlot found{};
    HOTA_CHECK(!findResearchSlot(states, found));
}

HOTA_TEST(the_first_researched_slot_wins_when_several_are_marked)
{
    GuildSlotStates states = statesWithRealSlots();
    states[1 * kGuildSlotsPerTier + 2] = 2;
    states[4 * kGuildSlotsPerTier + 0] = 2;

    GuildSlot found{};
    HOTA_CHECK(findResearchSlot(states, found));
    HOTA_CHECK_EQ(found.tier, 1);
    HOTA_CHECK_EQ(found.slot, 2);
}
