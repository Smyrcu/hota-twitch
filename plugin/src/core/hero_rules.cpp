#include "hero_rules.hpp"

#include <algorithm>
#include <utility>

namespace hota_twitch
{
namespace
{

/// Slot numbers from NH3API's TArtifactSlot: 13..16 are the war machines, 17 the spellbook.
constexpr int kFirstWarMachineSlot = 13;
constexpr int kMisc5Slot = 18;

constexpr std::int8_t kMinSkillLevel = 1;
constexpr std::int8_t kMaxSkillLevel = 3;

} // namespace

bool isReportedArtifactSlot(int slot)
{
    if (slot < 0 || slot >= kArtifactSlotCount)
    {
        return false;
    }
    return slot < kFirstWarMachineSlot || slot == kMisc5Slot;
}

void collectSkills(const SecondarySkillLevels& levels, const SecondarySkillOrder& order,
                   std::vector<SkillEntry>& out)
{
    std::vector<std::pair<std::uint8_t, SkillEntry>> learned;
    for (int id = 0; id < kSecondarySkillCount; ++id)
    {
        const std::int8_t level = levels[static_cast<std::size_t>(id)];
        if (level < kMinSkillLevel || level > kMaxSkillLevel)
        {
            continue;
        }
        learned.emplace_back(order[static_cast<std::size_t>(id)], SkillEntry{id, level});
    }
    std::stable_sort(learned.begin(), learned.end(),
                     [](const auto& a, const auto& b) { return a.first < b.first; });

    out.clear();
    out.reserve(learned.size());
    for (const auto& [slot, skill] : learned)
    {
        out.push_back(skill);
    }
}

} // namespace hota_twitch
