#pragma once

#include <array>
#include <cstdint>
#include <optional>
#include <string>
#include <string_view>
#include <vector>

namespace hota_twitch
{

/// Screen the game is showing, as `docs/protocol.md` names it.
enum class Screen
{
    None,
    Adventure,
    Town,
    Combat,
    Other,
};

std::string_view screenName(Screen screen);

struct ArmySlot
{
    int slot = 0;
    int creature = 0;
    int count = 0;
};

struct SkillEntry
{
    int id = 0;
    int level = 0;
};

struct EquippedEntry
{
    int slot = 0;
    int artifact = 0;
};

/// Names are the game's own single-byte text; the code page is applied when the snapshot is
/// serialised, so the game thread never pays for the conversion.
struct HeroSnapshot
{
    int id = 0;
    std::string name;
    int classId = 0;
    int picture = 0;
    int level = 0;
    int experience = 0;
    int mana = 0;
    int manaMax = 0;
    int move = 0;
    int moveMax = 0;
    std::array<int, 4> primary{};
    std::vector<SkillEntry> skills;
    std::vector<EquippedEntry> equipped;
    std::vector<int> backpack;
    std::vector<ArmySlot> army;

    void clear();
};

/// HotA spell research open in a town's mage guild.
struct Research
{
    int level = 0;
    int slot = 0;
    int spell = 0;
    int rolls = 0;
};

struct TownSnapshot
{
    int id = 0;
    std::string name;
    int type = 0;
    int fort = 0;
    int hall = 0;
    int guild = 0;
    std::array<std::vector<int>, 5> spells;
    std::optional<Research> research;
    std::vector<ArmySlot> garrison;
    std::optional<HeroSnapshot> garrisonHero;
    std::optional<HeroSnapshot> visitingHero;

    void clear();
};

struct GameDate
{
    int day = 0;
    int week = 0;
    int month = 0;
};

/// The game window. `docs/protocol.md` also allows a `uiScale`; the plugin does not send it
/// because the HD Mod interface scale has not been located yet (see docs/hota-memory-layout.md),
/// and the consumer assumes 1 when the field is missing.
struct Display
{
    int width = 0;
    int height = 0;
};

struct PlayerSnapshot
{
    int id = 0;
    std::string name;
    int currentHero = -1;
    int heroListTop = 0;
    int townListTop = 0;
};

/// One state document, in the shape `docs/protocol.md` describes. The game thread fills this
/// and hands it to the worker by swapping it with the worker's spent snapshot, so the two
/// threads keep trading the same two allocations instead of building a new document each tick.
struct StateSnapshot
{
    Screen screen = Screen::None;
    std::int64_t timestamp = 0;
    GameDate date;
    Display display;
    std::optional<PlayerSnapshot> player;
    std::vector<HeroSnapshot> heroes;
    std::vector<TownSnapshot> towns;

    void clear();

    /// Whether this describes the same game state as `other`. The timestamp is left out: it
    /// moves on every tick, and a document that only differs by its clock is not news the
    /// backend needs to hear about.
    bool sameStateAs(const StateSnapshot& other) const;
};

bool operator==(const ArmySlot& a, const ArmySlot& b);
bool operator==(const SkillEntry& a, const SkillEntry& b);
bool operator==(const EquippedEntry& a, const EquippedEntry& b);
bool operator==(const HeroSnapshot& a, const HeroSnapshot& b);
bool operator==(const Research& a, const Research& b);
bool operator==(const TownSnapshot& a, const TownSnapshot& b);
bool operator==(const GameDate& a, const GameDate& b);
bool operator==(const Display& a, const Display& b);
bool operator==(const PlayerSnapshot& a, const PlayerSnapshot& b);

} // namespace hota_twitch
