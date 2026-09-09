# HotA memory layout used by the plugin

What `plugin/` reads out of `h3hota HD.exe` (HotA 1.8 + HD Mod), where each address comes from,
and which parts NH3API already describes correctly. Read it together with
`plugin/src/game/layout.hpp`, which is the only file in the plugin allowed to hold a raw game
address: everything listed under "From NH3API" is reached through NH3API's own types instead.

Sources referred to below:

- **NH3API** — `plugin/external/NH3API`, void_17's database of the SoD 3.2 executable. HotA
  keeps that executable and patches it, so most of the layout still holds.
- **spike** — `docs/research/spike-reader.cs`, findings
  from reading a live HotA 1.8.0 process in September 2026. Where the spike and NH3API
  disagree, the spike watched the running game and wins.

Everything marked *unconfirmed* is a reasoned inference that no one has yet seen move in a
live game. The live check listed at the end of the plugin's report is what settles them.

## What NH3API already covers

These need no constants of our own; the plugin uses the NH3API type and lets the header carry
the offset.

| Thing | Where | Note |
|---|---|---|
| `gpGame` | `0x699538` | the `game` object |
| `gpCurPlayer` | `0x69CCFC` | player whose turn it is - **not** necessarily the streamer |
| `gpExec` | `0x699550` | `executive`; `tailManager` (+0x4) owns the screen |
| `gpAdvManager` | `0x6992B8` | adventure screen manager |
| `gpCombatManager` | `0x699420` | combat manager; **non-null on the map too** under HotA, so it is only ever compared against `tailManager`, never used as an "in combat" flag |
| `game::GetHero(id)` | `THISCALL 0x4317D0` | see "Finding heroes" |
| `game::day/week/month` | `game + 0x1F63E/0x40/0x42` | uint16 each |
| `game::player[8]` | `game + 0x20AD0` | `playerData`, stride `0x168` |
| `game::townPool` | `game + 0x21610` | `exe_vector<town>`, stride `0x168` |
| `advManager::advWindow` | `advManager + 0x44` | `TAdventureMapWindow*` |
| `playerData` fields | `color +0x00`, `numHeroes +0x01`, `currHero +0x04`, `heroes[8] +0x08`, `numTowns +0x3E`, `towns[72] +0x40`, `cName[21] +0xCC`, `isLocal +0xE1`, `isHuman +0xE2` | see "Finding the streamer" |
| `hero` fields | `mana +0x18`, `id +0x1A`, `playerOwner +0x22`, `name[13] +0x23`, `hero_class +0x30`, `portrait +0x34`, `maxMobility +0x49`, `currMobility +0x4D`, `experience +0x51`, `Level +0x55`, `heroArmy +0x91`, `SSLevel[28] +0xC9`, `SSOrder[28] +0xE5`, `equipped[19] +0x12D`, `backpack[64] +0x1D4`, `backpack_count +0x3D4`, `stats[4] +0x476`; stride `0x492` | all confirmed against the spike |
| `town` fields | `id +0x00`, `playerOwner +0x01`, `townType +0x04`, `garrisonHero +0x0C`, `occupyingHero +0x10`, `mageLevel +0x14`, `townSpells[5][6] +0x44`, `maxTownSpellAvailable[5] +0xBC`, `cName +0xC4`, `town_army +0xE0`, `full_building_mask +0x158` | see "Towns" |
| `armyGroup` | `type[7] +0x00`, `amount[7] +0x1C` | used for both hero armies and town garrisons |
| `hero::GetMaxMana()` | `knowledge x 10 x GetIntelligenceFactor()` | `GetIntelligenceFactor` is `THISCALL 0x4E4B20`, so HotA's own 20/35/50% Intelligence applies rather than SoD's 25/50/100%. The spike confirmed the result on a live hero: knowledge 23 with Advanced Intelligence gave 310. |

`backpack_count` deserves a note. The old H3API put it at `+0x3D1`; the spike measured `+0x3D4`
on a live game, which is also what `0x1D4 + 64 * 8` works out to. NH3API v1.2 already says
`+0x3D4`, so the two agree and the plugin inherits the correct offset.

## Constants the plugin owns

All of these are in `plugin/src/game/layout.hpp`.

### Screen resolution - `0x69FE68` (width), `0x69FE6C` (height)

INT32 each, the size the game window renders at. Established by the spike, which read
`(1920, 1080)` here with the game running at 1080p.

These are the fallback. The plugin asks the HD Mod first, through the patcher variables
`HD.Rez.X` / `HD.Rez.Y` that NH3API's `hd_mod.hpp` documents, and only reads the addresses when
the mod does not answer.

### Panel scroll - `advWindow + 0x64` (heroes), `advWindow + 0x68` (towns)

INT32 each: the index of the first entry the right-hand panel shows, which the overlay needs to
know which hero or town a row belongs to after the streamer scrolls.

This is the one place where NH3API's SoD layout is provably wrong for HotA. NH3API has:

```
+0x60  int32_t topHero
+0x64  int32_t topTown
+0x68  bitmapBackedTextWidget* RolloverWidget
```

The spike watched `+0x68` count `0 -> 1 -> 2` while scrolling the town list, and return to 0 on
coming back from a town. A pointer is never 0, 1 or 2, so under HotA + HD Mod the pair sits four
bytes further along than SoD says. `+0x64` read 0 for a player who owned exactly eight heroes
and therefore could not scroll the hero list at all: consistent with `topHero`, but *unconfirmed*
- nobody has watched it move.

Both are read as raw INT32 at an offset rather than through the NH3API member, and both are
range-checked (`0 <= value < 48`); anything else is reported as 0.

### Town manager - `0x69954C`

The manager the executive holds while a town window is open. NH3API exposes the adventure and
combat manager globals but not this one. Used only to name the screen `town`. Source: spike.

### HotA town extension record - `town + 0xD4`

HotA hangs a per-town record off these four bytes, which SoD uses for `SpellDisabledMask`. The
record is `0xD0` bytes long and is the only place the mage guild's spell research state can be
read from:

| Offset | Type | Meaning |
|---|---|---|
| `+0x10` | INT32[5][6] | slot states: 0 the tier has no such slot, 1 an ordinary spell, 2 a spell being researched |
| `+0xA4` | INT32 | rolls made on the slot currently under research; -1 once the research is closed |

Found by the spike on 2026-09-09 by diffing memory across a research roll, and confirmed with
the owner: rolling Town Portal into a slot and accepting it moved the counter as expected, and
closing the research set both `+0xA0` and `+0xA4` back to -1. That is why `rolls` is only
meaningful while research is open, which matches what `docs/protocol.md` says about `research`
being null the rest of the time.

Because SoD uses this field for something else, the value is not trusted blindly. It has to be
four-byte aligned, `VirtualQuery` has to agree that all `0xD0` bytes are committed and readable,
and every slot state has to be in `0..15`; a single value outside that range means the record is
not a HotA extension record and the town simply reports no research.

## Finding heroes

HotA relocates the hero pool: `game::heroPool` at `game + 0x21620` does not hold the live
records, which is why the spike - reading from outside the process - had to scan memory for a
hero record and rank the candidates by how many pointers referred to them.

From inside the process there is a better route. `game::GetHero(id)` is the game's own accessor
at `THISCALL 0x4317D0`, part of the patched executable, so wherever HotA moved the heroes to,
the accessor goes there. The plugin calls it and validates the result: the record has to carry
the id that was asked for, be owned by the player whose panel it came from, and have a non-empty
name. That check is what would catch the accessor no longer leading where it used to; it logs
once and leaves the hero list empty rather than shipping nonsense.

Two things support the route beyond the reasoning. HotA hero ids run past SoD's
`MAX_HEROES = 156` - the spike's own test hero, Todd, is 184 - so HotA must have repointed the
accessor rather than merely moved the array. And the spike's scan found the live copy by
counting references from game code to the table base, which is to say the game reaches heroes
through exactly this kind of indirection.

The route is *unconfirmed* until the live check runs. If it fails, the fallback is the spike's
scan-and-rank, which is not in the plugin today because nothing yet justifies carrying it.

## Finding the streamer

Not `gpCurPlayer`: during the AI's turn that points at the AI, and an AI turn is exactly when
the viewer wants to see what is happening to the streamer's towns. The plugin takes
`gpCurPlayer` when it is both `isLocal` and `isHuman`, and otherwise walks `game::player[8]` for
the first player that is.

Note that `isLocal` is `+0xE1` and `isHuman` is `+0xE2`. The spike read `+0xE1` and called it
`isHuman`; in a local single-player game both are true, so the mix-up never showed.

## Towns

`fort` and `hall` are read out of `full_building_mask` at `town + 0x158`, bits 7/8/9 for
fort/citadel/castle and 10..13 for the four halls. Every town has a village hall, so the lowest
hall bit is level 0.

The spike used `town + 0x150` for this and got the right answers on a live game. NH3API calls
`+0x150` the *visible* buildings mask and `+0x158` the *built* one; the two agree on walls and
halls, which is why the spike's reading worked. The plugin uses the semantically correct one,
which makes `+0x158` *unconfirmed* in the same sense as above - a town reporting `fort: 0` at
the live check would mean NH3API is wrong here too and `+0x150` is what HotA uses.

The number of real slots per guild tier comes from `maxTownSpellAvailable[tier]` rather than
from the fixed 5/4/3/2/1 table, because HotA keeps the count there and it already accounts for
the Tower's Library, which adds a slot to every tier. The rest of each six-entry row is
leftovers and must not be read. Tiers the guild has not been built up to are reported empty:
the spells are already drawn in memory, but the streamer has not seen them either.

## Hooks

| Address | Function | Why |
|---|---|---|
| `0x40F1D0` | `advManager::UpdateScreen(this, bool, bool)` | the adventure map redraw; where hero and town changes show up. Throttled to one snapshot per 300 ms. |
| `0x4B0880` | `executive::AddManager(this, baseManager*, int32)` | a screen opened |
| `0x4B0950` | `executive::RemoveManager(this, baseManager*)` | a screen closed |

All three are `__thiscall` and are hooked through patcher_x86 as `SPLICE_ EXTENDED_ THISCALL_`,
calling the original first and taking the snapshot afterwards.

The manager hooks exist because the adventure hook stops being called the moment a town or a
battle opens. Without them the last document posted would still say `screen: "adventure"`, and
the overlay would keep drawing cards over a town screen for as long as the streamer stayed
there.

## Things that are not read

- **Spell, creature and secondary skill name tables.** The spike located these by scanning
  memory for "Magic Arrow" and by following `0x6747B0` / `0x67DCF0`, because it printed names.
  `docs/protocol.md` sends ids and the overlay owns the tables, so the plugin does not need any
  of it - which is what removes the last memory scan from the plugin.
- **`playerData + 0x74`.** The spike measured -1 there under HotA + HD Mod, so the hero list
  scroll is read from the adventure window instead.
- **The HD Mod interface scale.** `docs/protocol.md` has a `display.uiScale` field for it and
  the plugin does not send it, because nobody has found where the mod keeps it. NH3API's
  `hd_mod.hpp` documents no variable for it, and the spike's evidence - a 1080p screenshot whose
  panel rows measured about 1.6x the 1440p ones - came from a screenshot that had been stretched
  to the monitor, so it says nothing certain. The consumer assumes 1 when the field is missing.
  To make the next session with a running game cheap, the plugin logs, at debug level and once
  per run, the HD Mod version, `HD.Rez.X/Y` against the raw resolution fields, and every line of
  every ini file under `<HD.Dir>/Settings`.
