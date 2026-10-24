#include "town_rules.hpp"

namespace hota_twitch
{
namespace
{

/// Building bits in the town's mask (see docs/hota-memory-layout.md).
constexpr int kFortBits[] = {7, 8, 9};
constexpr int kHallBits[] = {10, 11, 12, 13};

/// Slot states HotA writes: 0 research kept or no such slot, 1 an ordinary spell, 2 a spell
/// being researched. Anything above this ceiling means the record is not a HotA extension
/// record.
constexpr std::int32_t kStateKept = 0;
constexpr std::int32_t kStateResearch = 2;
constexpr std::int32_t kStateMax = 15;

/// Spells a roll draws: two on tiers 1 to 4, one on tier 5.
constexpr int kDrawnPerRoll = 2;
constexpr int kDrawnPerRollOnTopTier = 1;

constexpr int kBitsPerWord = 32;

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

bool isKeptResearch(std::int32_t state)
{
    return state == kStateKept;
}

bool isOffered(const OfferedSpells& offered, int spell)
{
    if (spell < 0 || spell >= static_cast<int>(offered.size()) * kBitsPerWord)
    {
        return false;
    }
    const auto word = static_cast<std::size_t>(spell / kBitsPerWord);
    return (offered[word] & (std::uint32_t{1} << (spell % kBitsPerWord))) != 0;
}

bool isSpellSet(const OfferedSpells& offered)
{
    for (int bit = kSpellCount; bit < static_cast<int>(offered.size()) * kBitsPerWord; ++bit)
    {
        if (isOffered(offered, bit))
        {
            return false;
        }
    }
    return true;
}

int tierRolls(int tier, int offered, int slots)
{
    const int drawn = offered > slots ? offered - slots : 0;
    const int perRoll = tier == kGuildTiers - 1 ? kDrawnPerRollOnTopTier : kDrawnPerRoll;
    return (drawn + perRoll - 1) / perRoll;
}

int openResearchRolls(int tierRolls, int keptSlots)
{
    const int rest = tierRolls - keptSlots;
    return rest > 1 ? rest : 1;
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
