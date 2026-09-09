#pragma once

#include "snapshot.hpp"

#include <array>
#include <cstdint>

namespace hota_twitch
{

inline constexpr int kSecondarySkillCount = 28;
inline constexpr int kArtifactSlotCount = 19;

using SecondarySkillLevels = std::array<std::int8_t, kSecondarySkillCount>;
using SecondarySkillOrder = std::array<std::uint8_t, kSecondarySkillCount>;

/// True for the body slots the card shows: the fourteen real artifact slots. The war machine
/// slots (ballista, ammo cart, first aid tent, catapult) and the spellbook slot are part of
/// the hero window rather than of the artifact list, so they are left out.
bool isReportedArtifactSlot(int slot);

/// The hero's learned secondary skills in the order the game displays them.
void collectSkills(const SecondarySkillLevels& levels, const SecondarySkillOrder& order,
                   std::vector<SkillEntry>& out);

} // namespace hota_twitch
