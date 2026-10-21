import { fortName, hallName, spellName, townTypeName } from '../../data/names.js';
import { fortIcon, hallIcon, spellIcon, townPicture } from '../../data/sprites.js';
import { armyOps } from '../army.js';
import { SINGLE_LINE, SectionBuilder, type LineCounter } from '../build.js';
import { fieldText, sprite, text, type Card, type DrawOp } from '../display.js';
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
    fieldText(town.name, 'medium', 'yellow', HEADER.x, HEADER.nameY, HEADER.width),
    sprite(hallIcon(town.hall), BUILDING_ROW.x, BUILDING_ROW.y, BUILDING_ROW.icon, BUILDING_ROW.icon),
    ...armyOps(garrisonArmy(town)),
  ];
  if (fort !== null) {
    const at = BUILDING_ROW.x + BUILDING_ROW.gap;
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

const GUILD_LEVELS = 5;

/** The roll count's box in the icon's bottom-right corner: medfont digits, a gold rim on a dark fill. */
const ROLL_BADGE = { digit: 9, padding: 2, height: 16, inkTop: 2 } as const;

/**
 * How many rolls a slot took, as a figure in the icon's corner. Nothing is drawn below one roll:
 * a count the producer could not read would otherwise show as a confident zero.
 */
function rollBadge(rolls: number, x: number, y: number, width: number, height: number): DrawOp[] {
  if (rolls < 1) return [];
  const figure = String(rolls);
  const boxWidth = figure.length * ROLL_BADGE.digit + ROLL_BADGE.padding * 2;
  const left = x + width - boxWidth;
  const top = y + height - ROLL_BADGE.height;
  return [
    { kind: 'fill', x: left, y: top, width: boxWidth, height: ROLL_BADGE.height, colour: '#d4b24c' },
    { kind: 'fill', x: left + 1, y: top + 1, width: boxWidth - 2, height: ROLL_BADGE.height - 2, colour: 'rgba(0, 0, 0, 0.85)' },
    text(figure, 'medium', 'yellow', left + boxWidth / 2, top - ROLL_BADGE.inkTop, 'center'),
  ];
}

/** The research on this level, open or closed, by the index of its spell in the level's list. */
function researchMarks(town: Town, level: number, spells: readonly number[]): Map<number, { rolls: number; open: boolean }> {
  const marks = new Map<number, { rolls: number; open: boolean }>();
  for (const closed of town.researched) {
    if (closed.level === level) marks.set(researchIndex(spells, closed), { rolls: closed.rolls, open: false });
  }
  if (town.research?.level === level) {
    marks.set(researchIndex(spells, town.research), { rolls: town.research.rolls, open: true });
  }
  return marks;
}

function guildSection(section: SectionBuilder, town: Town): void {
  if (town.guild <= 0) return;
  section.heading(`Mage Guild level ${town.guild}`);
  const { cell, iconWidth, iconHeight, perRow } = EXPANSION.spell;

  // The contract sends five arrays, one per guild level; an unbuilt level is empty.
  for (let level = 1; level <= GUILD_LEVELS; level += 1) {
    const spells = town.spells[level - 1] ?? [];
    if (spells.length === 0) continue;
    section.line(`Level ${level}`);
    const marks = researchMarks(town, level, spells);
    const cells = section.grid(spells.length, perRow, cell, iconHeight + 4);
    spells.forEach((spell, index) => {
      const at = cells[index];
      if (at === undefined) return;
      const x = cellIconX(at.x, cell, iconWidth, 0);
      section.push(sprite(spellIcon(spell), x, at.y, iconWidth, iconHeight));
      const mark = marks.get(index);
      if (mark === undefined) return;
      // Heroes cannot learn a spell still under research, so only that one is dimmed.
      if (mark.open) {
        section.push({ kind: 'fill', x, y: at.y, width: iconWidth, height: iconHeight, colour: 'rgba(0, 0, 0, 0.55)' });
      }
      section.push(...rollBadge(mark.rolls, x, at.y, iconWidth, iconHeight));
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

  section.line(`${townTypeName(town.type)} · ${hallName(town.hall)} · ${fortName(town.fort)}`);
  section.advance(4);
  guildSection(section, town);

  if (town.research !== null) {
    const { rolls, spell } = town.research;
    section.line(rolls < 1 ? `Research open: ${spellName(spell)}` : `Research open, ${rolls} roll(s): ${spellName(spell)}`);
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
