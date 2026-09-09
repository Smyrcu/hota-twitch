import { heroClassName } from '../../data/names.js';
import { artifactIcon, heroPortrait, primaryIcon, skillIcon } from '../../data/sprites.js';
import { armyOps } from '../army.js';
import { SINGLE_LINE, SectionBuilder, type LineCounter } from '../build.js';
import { sprite, text, type Card, type DrawOp } from '../display.js';
import {
  CARD_HEIGHT,
  CARD_WIDTH,
  EXPANSION,
  HEADER,
  PORTRAIT,
  PRIMARY_ROW,
  cellIconX,
  rowOrigin,
} from '../layout.js';
import type { Hero } from '../../state/protocol.js';

interface GridSpec {
  readonly cell: number;
  readonly icon: number;
  readonly perRow: number;
}

/** One labelled grid of icons in the expansion; nothing is drawn when the section is empty. */
function iconGrid<T>(
  section: SectionBuilder,
  spec: GridSpec,
  items: readonly T[],
  src: (item: T) => string,
  label: string,
): void {
  if (items.length === 0) return;
  section.heading(label);
  const cells = section.grid(items.length, spec.perRow, spec.cell, spec.icon + 2);
  items.forEach((item, index) => {
    const at = cells[index];
    if (at === undefined) return;
    section.push(sprite(src(item), cellIconX(at.x, spec.cell, spec.icon, 0), at.y, spec.icon, spec.icon));
  });
  section.advance(4);
}

/** The popup as the game draws it on right-click: portrait, name, primary skills, army. */
export function heroHoverCard(hero: Hero): Card {
  const primaryOrigin = rowOrigin(PRIMARY_ROW.cell, PRIMARY_ROW.count);
  const ops: DrawOp[] = [
    { kind: 'panel', panel: 'hero', x: 0, y: 0, width: CARD_WIDTH, height: CARD_HEIGHT },
    sprite(heroPortrait(hero.picture), PORTRAIT.x, PORTRAIT.y, PORTRAIT.width, PORTRAIT.height),
    text(hero.name, 'big', 'yellow', HEADER.x, HEADER.nameY, 'left', HEADER.width),
    text(`Level ${hero.level}`, 'medium', 'white', HEADER.x, HEADER.lineY),
    text(heroClassName(hero.class), 'small', 'white', HEADER.x, HEADER.subLineY, 'left', HEADER.width),
  ];

  hero.primary.forEach((value, index) => {
    const x = cellIconX(primaryOrigin, PRIMARY_ROW.cell, PRIMARY_ROW.icon, index);
    const centre = primaryOrigin + PRIMARY_ROW.cell * index + PRIMARY_ROW.cell / 2;
    ops.push(sprite(primaryIcon(index), x, PRIMARY_ROW.y, PRIMARY_ROW.icon, PRIMARY_ROW.icon));
    ops.push(text(String(value), 'small', 'gold', centre, PRIMARY_ROW.valueY, 'center'));
  });

  ops.push(...armyOps(hero.army));
  return { width: CARD_WIDTH, height: CARD_HEIGHT, ops };
}

/**
 * The click expansion: what the hero screen adds over the popup — secondary skills, the
 * artifacts on the hero and in the backpack, movement and mana.
 */
export function heroExpansion(
  hero: Hero,
  top: number,
  countLines: LineCounter = SINGLE_LINE,
): { ops: readonly DrawOp[]; height: number } {
  const section = new SectionBuilder(top + EXPANSION.paddingTop, countLines);

  section.line(`Movement ${hero.move} / ${hero.moveMax}`);
  section.line(`Mana ${hero.mana} / ${hero.manaMax}`);
  section.line(`Experience ${hero.exp}`);
  section.advance(4);

  iconGrid(section, EXPANSION.skill, hero.skills, ([skill, level]) => skillIcon(skill, level), 'Skills');

  const equipped = [...hero.equipped].sort((left, right) => left[0] - right[0]);
  iconGrid(section, EXPANSION.equipped, equipped, ([, artifact]) => artifactIcon(artifact), 'Artifacts');

  iconGrid(section, EXPANSION.backpack, hero.backpack, artifactIcon, `Backpack (${hero.backpack.length})`);

  return section.finish('expansion', CARD_WIDTH, top);
}

export function heroCard(hero: Hero, expanded: boolean, countLines: LineCounter = SINGLE_LINE): Card {
  const hover = heroHoverCard(hero);
  if (!expanded) return hover;
  const expansion = heroExpansion(hero, CARD_HEIGHT, countLines);
  return {
    width: CARD_WIDTH,
    height: CARD_HEIGHT + expansion.height,
    ops: [...hover.ops, ...expansion.ops],
  };
}

