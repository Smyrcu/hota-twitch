#include "framework.hpp"

#include "core/town_rules.hpp"

#include <initializer_list>

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

GuildTierSpells tierWith(std::initializer_list<std::int32_t> spells)
{
    GuildTierSpells tier{};
    tier.fill(-1);
    std::size_t index = 0;
    for (const std::int32_t spell : spells)
    {
        tier[index++] = spell;
    }
    return tier;
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

HOTA_TEST(a_full_tier_reports_slots_at_their_own_index)
{
    const GuildTierSpells tier = tierWith({15, 27, 38, 9, 36});

    HOTA_CHECK_EQ(reportedSpellIndex(tier, 5, 0), 0);
    HOTA_CHECK_EQ(reportedSpellIndex(tier, 5, 3), 3);
    HOTA_CHECK_EQ(reportedSpellIndex(tier, 5, 4), 4);
}

HOTA_TEST(an_empty_slot_before_the_researched_one_shifts_its_index)
{
    // The list the plugin sends holds only filled slots, so raw slot 3 is the second entry.
    GuildTierSpells tier = tierWith({15, -1, -1, 38});

    HOTA_CHECK_EQ(reportedSpellIndex(tier, 4, 3), 1);
    HOTA_CHECK_EQ(reportedSpellIndex(tier, 4, 0), 0);
}

HOTA_TEST(a_slot_the_tier_does_not_have_is_not_reported)
{
    const GuildTierSpells tier = tierWith({15, 27});

    HOTA_CHECK_EQ(reportedSpellIndex(tier, 2, 2), -1);
    HOTA_CHECK_EQ(reportedSpellIndex(tier, 2, 5), -1);
    HOTA_CHECK_EQ(reportedSpellIndex(tier, 0, 0), -1);
}

HOTA_TEST(an_empty_slot_is_not_reported_because_it_is_not_in_the_list)
{
    const GuildTierSpells tier = tierWith({15, -1, 38});

    HOTA_CHECK_EQ(reportedSpellIndex(tier, 3, 1), -1);
}

HOTA_TEST(a_negative_raw_slot_is_rejected)
{
    HOTA_CHECK_EQ(reportedSpellIndex(tierWith({15}), 5, -1), -1);
}

HOTA_TEST(the_library_slot_counts_as_a_real_slot)
{
    // A Tower with a Library has six slots on the first tier.
    const GuildTierSpells tier = tierWith({15, 27, 38, 9, 36, 55});

    HOTA_CHECK_EQ(reportedSpellIndex(tier, 6, 5), 5);
}

HOTA_TEST(a_tier_counts_its_rolls_from_the_spells_drawn_beyond_its_own)
{
    // The game as it stood on 2026-10-24: tier 5 offered five spells to one slot, tier 4 four
    // to two slots - one roll drawing two - and tier 1 nothing beyond its own.
    HOTA_CHECK_EQ(tierRolls(4, 5, 1), 4);
    HOTA_CHECK_EQ(tierRolls(3, 4, 2), 1);
    HOTA_CHECK_EQ(tierRolls(0, 5, 5), 0);
    // A last roll that drew one spell, the tier having run out.
    HOTA_CHECK_EQ(tierRolls(3, 5, 2), 2);
}

HOTA_TEST(research_kept_earlier_on_the_tier_takes_one_roll_each_and_the_open_one_the_rest)
{
    HOTA_CHECK_EQ(openResearchRolls(4, 0), 4);
    HOTA_CHECK_EQ(openResearchRolls(5, 2), 3);
    HOTA_CHECK_EQ(openResearchRolls(0, 0), 1);
}

HOTA_TEST(an_offered_set_with_a_bit_past_the_last_spell_is_not_hotas)
{
    OfferedSpells offered{};
    offered[2] = std::uint32_t{1} << 3;   // spell 67
    HOTA_CHECK(isSpellSet(offered));
    HOTA_CHECK(isOffered(offered, 67));

    offered[2] |= std::uint32_t{1} << 6;  // bit 70
    HOTA_CHECK(!isSpellSet(offered));
}

HOTA_TEST(offered_spells_are_read_across_the_word_boundaries)
{
    OfferedSpells offered{};
    offered[0] = std::uint32_t{1} << 31;  // spell 31
    offered[1] = 1;                       // spell 32
    offered[2] = 1;                       // spell 64

    HOTA_CHECK(isOffered(offered, 31));
    HOTA_CHECK(isOffered(offered, 32));
    HOTA_CHECK(!isOffered(offered, 63));
    HOTA_CHECK(isOffered(offered, 64));
    HOTA_CHECK(!isOffered(offered, -1));
}
