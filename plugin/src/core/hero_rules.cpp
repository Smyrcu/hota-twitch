#include "hero_rules.hpp"

#include <algorithm>
#include <array>
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
    // A hero can hold at most one of each secondary skill, so the working set fits on the
    // stack. It has to: this runs on the game thread, once per hero, several times a second.
    std::array<std::pair<std::uint8_t, SkillEntry>, kSecondarySkillCount> learned{};
    std::size_t count = 0;
    for (int id = 0; id < kSecondarySkillCount; ++id)
    {
        const std::int8_t level = levels[static_cast<std::size_t>(id)];
        if (level < kMinSkillLevel || level > kMaxSkillLevel)
        {
            continue;
        }
        learned[count++] = {order[static_cast<std::size_t>(id)], SkillEntry{id, level}};
    }
    const auto last = learned.begin() + static_cast<std::ptrdiff_t>(count);
    std::stable_sort(learned.begin(), last,
                     [](const auto& a, const auto& b) { return a.first < b.first; });

    out.clear();
    for (auto entry = learned.begin(); entry != last; ++entry)
    {
        out.push_back(entry->second);
    }
}

} // namespace hota_twitch
