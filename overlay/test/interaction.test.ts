import { describe, expect, it } from 'vitest';
import { advance, matches, selectionFor } from '../src/overlay/interaction.js';
import { cardScale, placeCard } from '../src/overlay/scale.js';
import type { Zone } from '../src/zones/index.js';
import { MOCK_STATE } from './fixtures/state.js';

const state = MOCK_STATE;

const zone = (kind: 'hero' | 'town', entry: number): Zone => ({
  kind,
  row: entry,
  entry,
  rect: { x: 100, y: 50, width: 64, height: 32 },
});

describe('clicking a card', () => {
  it('expands and collapses a hero', () => {
    const first = selectionFor(zone('hero', 0));
    const expanded = advance(first, null);

    expect(expanded.expanded).toBe(true);
    expect(advance(expanded, null).expanded).toBe(false);
  });

  it('steps a town through its heroes and back to the popup', () => {
    const dolere = state.towns[0]!;
    const start = selectionFor(zone('town', 0));

    const opened = advance(start, dolere);
    expect(opened).toMatchObject({ expanded: true, view: 'town' });

    const visiting = advance(opened, dolere);
    expect(visiting).toMatchObject({ expanded: true, view: 'visitingHero' });

    expect(advance(visiting, dolere)).toMatchObject({ expanded: false, view: 'town' });
  });

  it('skips the hero slots a town does not have', () => {
    const outpost = state.towns[2]!;
    const opened = advance(selectionFor(zone('town', 2)), outpost);

    expect(advance(opened, outpost)).toMatchObject({ expanded: false, view: 'town' });
  });

  it('matches a selection to its zone by kind and entry', () => {
    const selection = selectionFor(zone('hero', 3));

    expect(matches(selection, zone('hero', 3))).toBe(true);
    expect(matches(selection, zone('town', 3))).toBe(false);
    expect(matches(selection, zone('hero', 4))).toBe(false);
  });
});

describe('card scale', () => {
  it('follows the interface scale mapped onto the player', () => {
    expect(cardScale(1, 1)).toBe(1);
    expect(cardScale(1, 0.75)).toBe(1);
    expect(cardScale(2, 0.75)).toBe(2);
    expect(cardScale(2, 1)).toBe(2);
    expect(cardScale(1, 2)).toBe(2);
  });

  it('never drops below one player pixel per card pixel', () => {
    expect(cardScale(1, 0.2)).toBe(1);
    expect(cardScale(0, 0)).toBe(1);
    expect(cardScale(Number.NaN, 1)).toBe(1);
  });
});

describe('card placement', () => {
  const player = { width: 1920, height: 1080 };
  const card = { width: 194, height: 186 };

  it('opens to the left of the panel entry', () => {
    expect(placeCard({ x: 800, y: 200, width: 64, height: 32 }, card, player)).toEqual({ left: 800 - 8 - 194, top: 200 });
  });

  it('falls back to the right when there is no room on the left', () => {
    expect(placeCard({ x: 10, y: 20, width: 64, height: 32 }, card, player).left).toBe(82);
  });

  it('keeps the card inside the player', () => {
    const at = placeCard({ x: 1900, y: 1050, width: 64, height: 32 }, card, player);

    expect(at.left).toBeLessThanOrEqual(player.width - card.width);
    expect(at.top).toBe(player.height - card.height);
  });
});
