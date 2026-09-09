#pragma once

#include <cstddef>
#include <cstdint>

/// Every address and offset the plugin uses that NH3API does not already describe correctly
/// for HotA 1.8 + HD Mod. Each entry names where it comes from; `docs/hota-memory-layout.md`
/// carries the long version. Nothing outside this file may contain a raw game address.
namespace hota_twitch::layout
{

/// Game window size the HD Mod renders at. NH3API's `hd_mod.hpp` exposes the same numbers as
/// the patcher variables `HD.Rez.X` / `HD.Rez.Y`, which is the route the plugin takes first;
/// these addresses are the fallback for the case where the patcher does not answer.
/// Source: spike, 2026-09-09 - reading (1920, 1080) here while the game ran windowed at 1080p.
inline constexpr std::uintptr_t kScreenWidthAddress = 0x69FE68;
inline constexpr std::uintptr_t kScreenHeightAddress = 0x69FE6C;

/// Scroll position of the two panel lists, inside the adventure map window
/// (`advManager::advWindow`, itself at advManager + 0x44).
///
/// NH3API places `topHero` at +0x60 and `topTown` at +0x64 for SoD, and a
/// `bitmapBackedTextWidget*` at +0x68. Under HotA + HD Mod the spike watched +0x68 count
/// 0 -> 1 -> 2 as the town list scrolled, which no pointer ever does, so the pair sits four
/// bytes further along than the SoD layout says. The hero counterpart at +0x64 read 0 with a
/// player who owned exactly eight heroes and therefore could not scroll: consistent, but not
/// yet confirmed by watching it move. Both are range-checked before use.
/// Source: spike, 2026-09-09.
inline constexpr std::size_t kAdvWindowTopHero = 0x64;
inline constexpr std::size_t kAdvWindowTopTown = 0x68;

/// Largest scroll offset either panel list can hold; anything else means the field is not
/// what we think it is and the plugin reports 0.
inline constexpr std::int32_t kMaxListTop = 48;

/// HotA hangs a per-town extension record off `town + 0xD4`, bytes SoD uses for
/// `SpellDisabledMask`. The record is 0xD0 bytes and holds the mage guild slot states and the
/// spell research counter; it is the only place the research state can be read from.
/// Source: spike, 2026-09-09, found by diffing memory across a research roll.
inline constexpr std::size_t kTownHotaExtension = 0xD4;
inline constexpr std::size_t kHotaExtensionSize = 0xD0;

/// INT32[5][6] slot states inside the extension record: 0 the tier has no such slot,
/// 1 an ordinary spell, 2 a spell being researched.
inline constexpr std::size_t kHotaExtensionSlotStates = 0x10;

/// INT32 number of rolls made on the slot currently under research. Holds -1 once the
/// streamer closes the research, so the count is only readable while research is open.
inline constexpr std::size_t kHotaExtensionRollCount = 0xA4;

/// The town screen manager. NH3API exposes the adventure and combat manager globals but not
/// this one; the plugin needs it only to tell the town screen apart from the rest.
/// Source: spike, 2026-09-07 - the pointer the executive holds while a town window is open.
inline constexpr std::uintptr_t kTownManagerPointer = 0x69954C;

/// Functions the plugin hooks. All three are __thiscall.
///
/// `advManager::UpdateScreen` is the adventure map redraw; NH3API calls it as
/// THISCALL_3(void, 0x40F1D0, this, false, false). `executive::AddManager` and
/// `executive::RemoveManager` are how the game pushes and pops the manager that owns the
/// screen, so they are exactly the screen transitions.
/// Source: NH3API (adventure.hpp, base_manager.hpp).
inline constexpr std::uintptr_t kAdvManagerUpdateScreen = 0x40F1D0;
inline constexpr std::uintptr_t kExecutiveAddManager = 0x4B0880;
inline constexpr std::uintptr_t kExecutiveRemoveManager = 0x4B0950;

/// Smallest address the plugin will follow as a pointer. Below this lies the null page and
/// the small integers that leftover memory is full of.
inline constexpr std::uintptr_t kLowestValidPointer = 0x10000;

} // namespace hota_twitch::layout
