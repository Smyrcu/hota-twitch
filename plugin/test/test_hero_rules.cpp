#include "framework.hpp"

#include "core/hero_rules.hpp"

using namespace hota_twitch;

HOTA_TEST(the_fourteen_real_artifact_slots_are_reported)
{
    for (int slot = 0; slot <= 12; ++slot)
    {
        HOTA_CHECK(isReportedArtifactSlot(slot));
    }
    HOTA_CHECK(isReportedArtifactSlot(18));
}

HOTA_TEST(war_machine_and_spellbook_slots_are_left_out)
{
    for (int slot = 13; slot <= 17; ++slot)
    {
        HOTA_CHECK(!isReportedArtifactSlot(slot));
    }
}

HOTA_TEST(slots_outside_the_body_are_rejected)
{
    HOTA_CHECK(!isReportedArtifactSlot(-1));
    HOTA_CHECK(!isReportedArtifactSlot(19));
}

HOTA_TEST(skills_come_back_in_the_order_the_game_displays_them)
{
    SecondarySkillLevels levels{};
    SecondarySkillOrder order{};
    levels[7] = 3;
    order[7] = 2;
    levels[19] = 2;
    order[19] = 0;
    levels[3] = 1;
    order[3] = 1;

    std::vector<SkillEntry> skills;
    collectSkills(levels, order, skills);

    HOTA_CHECK_EQ(skills.size(), std::size_t{3});
    HOTA_CHECK_EQ(skills[0].id, 19);
    HOTA_CHECK_EQ(skills[0].level, 2);
    HOTA_CHECK_EQ(skills[1].id, 3);
    HOTA_CHECK_EQ(skills[2].id, 7);
    HOTA_CHECK_EQ(skills[2].level, 3);
}

HOTA_TEST(unlearned_skills_are_skipped)
{
    SecondarySkillLevels levels{};
    SecondarySkillOrder order{};
    levels[5] = 0;
    levels[6] = 1;

    std::vector<SkillEntry> skills;
    collectSkills(levels, order, skills);

    HOTA_CHECK_EQ(skills.size(), std::size_t{1});
    HOTA_CHECK_EQ(skills[0].id, 6);
}

HOTA_TEST(levels_outside_one_to_three_are_treated_as_not_learned)
{
    SecondarySkillLevels levels{};
    SecondarySkillOrder order{};
    levels[1] = 4;
    levels[2] = -1;

    std::vector<SkillEntry> skills;
    collectSkills(levels, order, skills);

    HOTA_CHECK(skills.empty());
}

HOTA_TEST(collecting_skills_replaces_whatever_the_target_held)
{
    SecondarySkillLevels levels{};
    SecondarySkillOrder order{};
    levels[0] = 1;

    std::vector<SkillEntry> skills{{99, 3}, {98, 2}};
    collectSkills(levels, order, skills);

    HOTA_CHECK_EQ(skills.size(), std::size_t{1});
    HOTA_CHECK_EQ(skills[0].id, 0);
}
