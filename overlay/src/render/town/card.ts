import { fortName, hallName, spellName, townTypeName } from '../../data/names.js';
import { fortIcon, hallIcon, spellIcon, townPicture } from '../../data/sprites.js';
import { armyOps } from '../army.js';
import { SINGLE_LINE, SectionBuilder, type LineCounter } from '../build.js';
import { sprite, text, type Card, type DrawOp } from '../display.js';
import {
  BUILDING_ROW,
  CARD_HEIGHT,
  CARD_WIDTH,
  EXPANSION,
  HEADER,
  PORTRAIT,
  cellIconX,
} from '../layout.js';
import type { ArmyEntry, Research, Town } from '../../state/protocol.js';

/** When a hero stands in the garrison the game keeps the army on the hero (docs/protocol.md). */
export function garrisonArmy(town: Town): readonly ArmyEntry[] {
  return town.garrisonHero?.army ?? town.garrison;
}

/** The town popup: picture, name, hall and fort, garrison. */
export function townHoverCard(town: Town): Card {
  const fort = fortIcon(town.fort);
  const ops: DrawOp[] = [
    { kind: 'panel', panel: 'town', x: 0, y: 0, width: CARD_WIDTH, height: CARD_HEIGHT },
    sprite(townPicture(town.type, town.fort), PORTRAIT.x, PORTRAIT.y, PORTRAIT.width, PORTRAIT.height),
    text(town.name, 'big', 'yellow', HEADER.x, HEADER.nameY, 'left', HEADER.width),
    text(townTypeName(town.type), 'medium', 'white', HEADER.x, HEADER.lineY, 'left', HEADER.width),
    sprite(hallIcon(town.hall), HEADER.x, BUILDING_ROW.y, BUILDING_ROW.icon, BUILDING_ROW.icon),
    ...armyOps(garrisonArmy(town)),
  ];
  if (fort !== null) {
    const at = HEADER.x + BUILDING_ROW.icon + BUILDING_ROW.gap;
    ops.push(sprite(fort, at, BUILDING_ROW.y, BUILDING_ROW.icon, BUILDING_ROW.icon));
  }
  return { width: CARD_WIDTH, height: CARD_HEIGHT, ops };
}

/**
 * Where the spell under research sits in the level's list. The contract puts it at
 * `spells[level-1][slot]`; the spell id is matched first so the mark still lands correctly if
 * the producer ever omits slots the faction does not have.
 */
function researchIndex(level: readonly number[], research: Research): number {
  const byId = level.indexOf(research.spell);
  return byId >= 0 ? byId : research.slot;
}

function guildSection(section: SectionBuilder, town: Town): void {
  if (town.guild <= 0) return;
  section.heading(`Mage Guild level ${town.guild}`);
  const { cell, iconWidth, iconHeight, perRow } = EXPANSION.spell;

  for (let level = 1; level <= town.guild; level += 1) {
    const spells = town.spells[level - 1] ?? [];
    if (spells.length === 0) continue;
    section.line(`Level ${level}`);
    const researched = town.research?.level === level ? researchIndex(spells, town.research) : -1;
    const cells = section.grid(spells.length, perRow, cell, iconHeight + 4);
    spells.forEach((spell, index) => {
      const at = cells[index];
      if (at === undefined) return;
      const x = cellIconX(at.x, cell, iconWidth, 0);
      section.push(sprite(spellIcon(spell), x, at.y, iconWidth, iconHeight));
      if (index !== researched) return;
      section.push({ kind: 'fill', x, y: at.y, width: iconWidth, height: iconHeight, colour: 'rgba(0, 0, 0, 0.55)' });
      section.push(
        text(
          `?${town.research?.rolls ?? 0}`,
          'tiny',
          'yellow',
          x + iconWidth / 2,
          at.y + Math.floor(iconHeight / 2) - 5,
          'center',
        ),
      );
    });
    section.advance(2);
  }
}

/** The click expansion: mage guild with the researched slot marked, and the heroes in the town. */
export function townExpansion(
  town: Town,
  top: number,
  countLines: LineCounter = SINGLE_LINE,
): { ops: readonly DrawOp[]; height: number } {
  const section = new SectionBuilder(top + EXPANSION.paddingTop, countLines);

  section.line(`${hallName(town.hall)} · ${fortName(town.fort)}`);
  section.advance(4);
  guildSection(section, town);

  if (town.research !== null) {
    section.line(`Research open, ${town.research.rolls} roll(s): ${spellName(town.research.spell)}`);
  }
  if (town.garrisonHero !== null) section.line(`Garrison: ${town.garrisonHero.name}`);
  if (town.visitingHero !== null) section.line(`Visiting: ${town.visitingHero.name}`);
  if (town.garrisonHero !== null || town.visitingHero !== null) section.note('Click again for the hero');

  return section.finish('expansion', CARD_WIDTH, top);
}

export function townCard(town: Town, expanded: boolean, countLines: LineCounter = SINGLE_LINE): Card {
  const hover = townHoverCard(town);
  if (!expanded) return hover;
  const expansion = townExpansion(town, CARD_HEIGHT, countLines);
  return { width: CARD_WIDTH, height: CARD_HEIGHT + expansion.height, ops: [...hover.ops, ...expansion.ops] };
}
