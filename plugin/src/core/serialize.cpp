#include "serialize.hpp"

#include "json_writer.hpp"

namespace hota_twitch
{
namespace
{

constexpr std::int64_t kProtocolVersion = 1;

void writeArmy(JsonWriter& json, const std::vector<ArmySlot>& army)
{
    json.beginArray();
    for (const ArmySlot& entry : army)
    {
        json.beginArray();
        json.number(entry.slot);
        json.number(entry.creature);
        json.number(entry.count);
        json.endArray();
    }
    json.endArray();
}

void writeHero(JsonWriter& json, const HeroSnapshot& hero, Codepage codepage)
{
    json.beginObject();
    json.key("id");
    json.number(hero.id);
    json.key("name");
    json.string(toUtf8(hero.name, codepage));
    json.key("class");
    json.number(hero.classId);
    json.key("picture");
    json.number(hero.picture);
    json.key("level");
    json.number(hero.level);
    json.key("exp");
    json.number(hero.experience);
    json.key("mana");
    json.number(hero.mana);
    json.key("manaMax");
    json.number(hero.manaMax);
    json.key("move");
    json.number(hero.move);
    json.key("moveMax");
    json.number(hero.moveMax);

    json.key("primary");
    json.beginArray();
    for (const int value : hero.primary)
    {
        json.number(value);
    }
    json.endArray();

    json.key("skills");
    json.beginArray();
    for (const SkillEntry& skill : hero.skills)
    {
        json.beginArray();
        json.number(skill.id);
        json.number(skill.level);
        json.endArray();
    }
    json.endArray();

    json.key("equipped");
    json.beginArray();
    for (const EquippedEntry& entry : hero.equipped)
    {
        json.beginArray();
        json.number(entry.slot);
        json.number(entry.artifact);
        json.endArray();
    }
    json.endArray();

    json.key("backpack");
    json.beginArray();
    for (const int artifact : hero.backpack)
    {
        json.number(artifact);
    }
    json.endArray();

    json.key("army");
    writeArmy(json, hero.army);
    json.endObject();
}

void writeHeroOrNull(JsonWriter& json, const std::optional<HeroSnapshot>& hero, Codepage codepage)
{
    if (hero.has_value())
    {
        writeHero(json, *hero, codepage);
    }
    else
    {
        json.nullValue();
    }
}

void writeTown(JsonWriter& json, const TownSnapshot& town, Codepage codepage)
{
    json.beginObject();
    json.key("id");
    json.number(town.id);
    json.key("name");
    json.string(toUtf8(town.name, codepage));
    json.key("type");
    json.number(town.type);
    json.key("fort");
    json.number(town.fort);
    json.key("hall");
    json.number(town.hall);
    json.key("guild");
    json.number(town.guild);

    json.key("spells");
    json.beginArray();
    for (const std::vector<int>& tier : town.spells)
    {
        json.beginArray();
        for (const int spell : tier)
        {
            json.number(spell);
        }
        json.endArray();
    }
    json.endArray();

    json.key("research");
    if (town.research.has_value())
    {
        json.beginObject();
        json.key("level");
        json.number(town.research->level);
        json.key("slot");
        json.number(town.research->slot);
        json.key("spell");
        json.number(town.research->spell);
        json.key("rolls");
        json.number(town.research->rolls);
        json.endObject();
    }
    else
    {
        json.nullValue();
    }

    json.key("garrison");
    writeArmy(json, town.garrison);
    json.key("garrisonHero");
    writeHeroOrNull(json, town.garrisonHero, codepage);
    json.key("visitingHero");
    writeHeroOrNull(json, town.visitingHero, codepage);
    json.endObject();
}

} // namespace

void serializeState(const StateSnapshot& state, Codepage codepage, std::string& out)
{
    out.clear();
    JsonWriter json(out);
    json.beginObject();
    json.key("v");
    json.number(kProtocolVersion);
    json.key("ts");
    json.number(state.timestamp);
    json.key("screen");
    json.string(screenName(state.screen));

    json.key("date");
    json.beginObject();
    json.key("day");
    json.number(state.date.day);
    json.key("week");
    json.number(state.date.week);
    json.key("month");
    json.number(state.date.month);
    json.endObject();

    json.key("display");
    json.beginObject();
    json.key("width");
    json.number(state.display.width);
    json.key("height");
    json.number(state.display.height);
    json.endObject();

    json.key("player");
    if (state.player.has_value())
    {
        json.beginObject();
        json.key("id");
        json.number(state.player->id);
        json.key("name");
        json.string(toUtf8(state.player->name, codepage));
        json.key("currentHero");
        json.number(state.player->currentHero);
        json.key("heroListTop");
        json.number(state.player->heroListTop);
        json.key("townListTop");
        json.number(state.player->townListTop);
        json.endObject();
    }
    else
    {
        json.nullValue();
    }

    json.key("heroes");
    json.beginArray();
    for (const HeroSnapshot& hero : state.heroes)
    {
        writeHero(json, hero, codepage);
    }
    json.endArray();

    json.key("towns");
    json.beginArray();
    for (const TownSnapshot& town : state.towns)
    {
        writeTown(json, town, codepage);
    }
    json.endArray();
    json.endObject();
}

} // namespace hota_twitch
