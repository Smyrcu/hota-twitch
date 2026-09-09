#include "town_rules.hpp"

namespace hota_twitch
{
namespace
{

/// Building bits in the town's mask (see docs/hota-memory-layout.md).
constexpr int kFortBits[] = {7, 8, 9};
constexpr int kHallBits[] = {10, 11, 12, 13};

/// Slot states HotA writes: 0 the tier has no such slot, 1 an ordinary spell,
/// 2 a spell being researched. Anything above this ceiling means the record is not a HotA
/// extension record.
constexpr std::int32_t kStateResearch = 2;
constexpr std::int32_t kStateMax = 15;

template<std::size_t N>
int highestSet(std::uint64_t mask, const int (&bits)[N])
{
    int level = 0;
    for (std::size_t i = 0; i < N; ++i)
    {
        if ((mask & (std::uint64_t{1} << bits[i])) != 0)
        {
            level = static_cast<int>(i) + 1;
        }
    }
    return level;
}

} // namespace

int fortLevel(std::uint64_t builtMask)
{
    return highestSet(builtMask, kFortBits);
}

int hallLevel(std::uint64_t builtMask)
{
    // Every town has a village hall, so the lowest building bit is level 0, not level 1.
    const int built = highestSet(builtMask, kHallBits);
    return built > 0 ? built - 1 : 0;
}

int guildSlotCount(int slotsAvailable)
{
    if (slotsAvailable < 0)
    {
        return 0;
    }
    return slotsAvailable > kGuildSlotsPerTier ? kGuildSlotsPerTier : slotsAvailable;
}

int reportedSpellIndex(const GuildTierSpells& tierSpells, int slotCount, int rawSlot)
{
    const int slots = guildSlotCount(slotCount);
    if (rawSlot < 0 || rawSlot >= slots || tierSpells[static_cast<std::size_t>(rawSlot)] < 0)
    {
        return -1;
    }
    int index = 0;
    for (int slot = 0; slot < rawSlot; ++slot)
    {
        if (tierSpells[static_cast<std::size_t>(slot)] >= 0)
        {
            ++index;
        }
    }
    return index;
}

bool findResearchSlot(const GuildSlotStates& states, GuildSlot& out)
{
    bool found = false;
    for (int tier = 0; tier < kGuildTiers; ++tier)
    {
        for (int slot = 0; slot < kGuildSlotsPerTier; ++slot)
        {
            const std::int32_t state = states[static_cast<std::size_t>(tier * kGuildSlotsPerTier + slot)];
            if (state < 0 || state > kStateMax)
            {
                return false;
            }
            if (state == kStateResearch && !found)
            {
                out = {tier, slot};
                found = true;
            }
        }
    }
    return found;
}

} // namespace hota_twitch
