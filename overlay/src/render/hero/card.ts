import { heroClassName } from '../../data/names.js';
import { artifactIcon, heroPortrait, skillIcon } from '../../data/sprites.js';
import { armyOps } from '../army.js';
import { SINGLE_LINE, SectionBuilder, type LineCounter } from '../build.js';
import { fieldText, sprite, text, type Card, type DrawOp } from '../display.js';
import {
  CARD_HEIGHT,
  CARD_WIDTH,
  EXPANSION,
  HEADER,
  MANA_FIELD,
  PORTRAIT,
  PRIMARY_ROW,
  cellIconX,
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

/** The popup as the game draws it on right-click: the bitmap carries the frames and the
 * primary-skill icons, so only the hero's own values are placed into them. */
export function heroHoverCard(hero: Hero): Card {
  const ops: DrawOp[] = [
    { kind: 'panel', panel: 'hero', x: 0, y: 0, width: CARD_WIDTH, height: CARD_HEIGHT },
    sprite(heroPortrait(hero.picture), PORTRAIT.x, PORTRAIT.y, PORTRAIT.width, PORTRAIT.height),
    fieldText(hero.name, 'medium', 'yellow', HEADER.x, HEADER.nameY, HEADER.width),
  ];

  hero.primary.forEach((value, index) => {
    const centre = PRIMARY_ROW.first + PRIMARY_ROW.cell * index;
    ops.push(text(String(value), 'small', 'gold', centre, PRIMARY_ROW.y, 'center'));
  });

  ops.push(text(String(hero.mana), 'tiny', 'white', MANA_FIELD.centre, MANA_FIELD.y, 'center'));
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

  section.line(`Level ${hero.level} ${heroClassName(hero.class)}`);
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

