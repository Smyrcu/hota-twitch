import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import { heroHoverCard } from '../src/render/hero/card.js';
import { townHoverCard } from '../src/render/town/card.js';
import { ARMY_ROWS, CARD_HEIGHT, CARD_WIDTH, armySlot } from '../src/render/layout.js';
import { parseAtlas, type FontAtlas } from '../src/render/text/atlas.js';
import { FONT_FILES, type FontId } from '../src/data/fonts.js';
import { alignOffset, layoutText } from '../src/render/text/layout.js';
import type { Card, DrawOp } from '../src/render/display.js';
import type { ArmyEntry, Hero, Town } from '../src/state/protocol.js';
import { MOCK_STATE } from './fixtures/state.js';

const loaded = new Map<FontId, FontAtlas>();

/**
 * The real fonts, because this is about the pixels the popup bitmap leaves free: a count is one
 * line tall and the strip of leather under an army row is exactly one line tall as well. Each
 * string is measured in the font it is drawn in, or a card could hide an overlap behind the
 * narrowest one.
 */
function atlasFor(font: FontId): FontAtlas {
  const cached = loaded.get(font);
  if (cached !== undefined) return cached;
  const file = `${FONT_FILES[font]}.json`;
  const atlas = parseAtlas(JSON.parse(readFileSync(new URL(`../assets/fonts/${file}`, import.meta.url), 'utf8')));
  if (atlas === null) throw new Error(`assets/fonts/${file} is not a font atlas`);
  loaded.set(font, atlas);
  return atlas;
}

interface Box {
  readonly left: number;
  readonly top: number;
  readonly right: number;
  readonly bottom: number;
}

/** The rectangle an operation covers. Panels are the card itself, so they bound nothing. */
function boxOf(op: DrawOp): Box | null {
  if (op.kind === 'panel') return null;
  if (op.kind !== 'text') {
    return { left: op.x, top: op.y, right: op.x + op.width, bottom: op.y + op.height };
  }
  const layout = layoutText(atlasFor(op.font), op.text, {
    align: op.align,
    ...(op.maxWidth === undefined ? {} : { maxWidth: op.maxWidth }),
    ...(op.maxLines === undefined ? {} : { maxLines: op.maxLines }),
  });
  const left = op.x + alignOffset(op.align, layout.width);
  return { left, top: op.y, right: left + layout.width, bottom: op.y + layout.height };
}

const overlaps = (a: Box, b: Box): boolean =>
  a.left < b.right && b.left < a.right && a.top < b.bottom && b.top < a.bottom;

const COUNT_ROWS: readonly number[] = ARMY_ROWS.map((row) => row.count.top);

type TextOp = Extract<DrawOp, { kind: 'text' }>;

/** The army counts on a card, told apart from the other small numbers by the row they sit on. */
const countOps = (card: Card): TextOp[] =>
  card.ops.filter((op): op is TextOp => op.kind === 'text' && COUNT_ROWS.includes(op.y));

/** A full army of wide counts, which is the case that used to run into the frames. */
const FULL_ARMY: readonly ArmyEntry[] = [
  [0, 13, 20],
  [1, 110, 999],
  [2, 116, 1],
  [3, 118, 12345],
  [4, 120, 7],
  [5, 127, 88],
  [6, 131, 6],
];

const hero: Hero = { ...MOCK_STATE.heroes[0]!, army: FULL_ARMY };
const town: Town = { ...MOCK_STATE.towns[0]!, garrison: FULL_ARMY, garrisonHero: null };

describe('army slots', () => {
  it('leaves each count a strip of leather exactly one line tall', () => {
    for (const row of ARMY_ROWS) {
      expect(row.count.bottom - row.count.top).toBe(atlasFor('tiny').lineHeight);
      for (const other of ARMY_ROWS) {
        const clear = row.count.top >= other.y + other.icon || row.count.bottom <= other.y;
        expect(clear, `the count under the row at y ${row.y} runs into the boxes at y ${other.y}`).toBe(true);
      }
    }
    expect(ARMY_ROWS[1]!.count.bottom).toBeLessThanOrEqual(CARD_HEIGHT);
  });

  it('never lets a count run into a creature icon or the picture', () => {
    for (const card of [heroHoverCard(hero), townHoverCard(town)]) {
      const drawn = countOps(card);
      expect(drawn).toHaveLength(7);

      for (const count of drawn) {
        const box = boxOf(count)!;
        for (const op of card.ops) {
          if (op === count) continue;
          const other = boxOf(op);
          if (other === null) continue;
          expect(overlaps(box, other), `the count ${count.text} covers ${JSON.stringify(other)}`).toBe(false);
        }
      }
    }
  });

  it('centres the count on its own box, inside the card', () => {
    for (const count of countOps(heroHoverCard(hero))) {
      const box = boxOf(count)!;
      expect(box.left).toBeGreaterThanOrEqual(0);
      expect(box.right).toBeLessThanOrEqual(CARD_WIDTH);
      expect(box.bottom).toBeLessThanOrEqual(CARD_HEIGHT);
    }
    expect(armySlot(0)).toEqual({ x: 45, y: 85, centre: 61, icon: 32, countY: 119 });
    expect(armySlot(3)).toEqual({ x: 27, y: 133, centre: 43, icon: 32, countY: 167 });
    expect(armySlot(7)).toBeNull();
  });
});
