#pragma once

#include <array>
#include <cstdint>

namespace hota_twitch
{

/// Number of guild slots per tier and of tiers, as HotA lays the mage guild out.
inline constexpr int kGuildTiers = 5;
inline constexpr int kGuildSlotsPerTier = 6;
inline constexpr int kGuildSlots = kGuildTiers * kGuildSlotsPerTier;

using GuildSlotStates = std::array<std::int32_t, kGuildSlots>;

/// Fort level from the town's built-buildings mask: 0 none, 1 fort, 2 citadel, 3 castle.
int fortLevel(std::uint64_t builtMask);

/// Town hall level: 0 village hall, 1 town hall, 2 city hall, 3 capitol.
int hallLevel(std::uint64_t builtMask);

/// How many of a tier's six slots the town really has. HotA keeps the count per tier, which
/// already accounts for the Tower's Library; the rest of the row holds leftovers.
int guildSlotCount(int slotsAvailable);

/// One slot of the mage guild, tier and slot both zero-based.
struct GuildSlot
{
    int tier = 0;
    int slot = 0;
};

/// Finds the slot HotA marks as being researched. Returns false when nothing is under
/// research, and also when the states do not look like a HotA extension record at all -
/// the field the record hangs off is unused in SoD, so a plausibility check is what tells
/// a real record from leftovers.
bool findResearchSlot(const GuildSlotStates& states, GuildSlot& out);

} // namespace hota_twitch
