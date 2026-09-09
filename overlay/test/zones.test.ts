import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import { HERO_LIST, TOWN_LIST, computeZones, containFit, visibleRows } from '../src/zones/index.js';
import type { GameState } from '../src/state/protocol.js';

interface Slot {
  x: number;
  y: number;
  w: number;
  h: number;
}

const reference = JSON.parse(
  readFileSync(new URL('../../docs/research/spike-zones-2560x1440.json', import.meta.url), 'utf8'),
) as { heroSlots: Slot[]; townSlots: Slot[] };

function state(overrides: Partial<GameState> = {}): GameState {
  return {
    v: 1,
    ts: 0,
    screen: 'adventure',
    date: { day: 1, week: 1, month: 1 },
    display: { width: 2560, height: 1440, uiScale: 1 },
    player: { id: 0, name: 'p', currentHero: -1, heroListTop: 0, townListTop: 0 },
    heroes: Array.from({ length: 8 }, (_, id) => ({ id }) as GameState['heroes'][number]),
    towns: Array.from({ length: 7 }, (_, id) => ({ id }) as GameState['towns'][number]),
    ...overrides,
  };
}

describe('hover zones', () => {
  it('reproduces the measured slots at 2560x1440, scale 1, player 1:1', () => {
    const zones = computeZones(state(), { width: 2560, height: 1440 });
    const heroes = zones.filter((zone) => zone.kind === 'hero').map((zone) => zone.rect);
    const towns = zones.filter((zone) => zone.kind === 'town').map((zone) => zone.rect);

    expect(heroes).toEqual(reference.heroSlots.map((slot) => ({ x: slot.x, y: slot.y, width: slot.w, height: slot.h })));
    expect(towns).toEqual(reference.townSlots.map((slot) => ({ x: slot.x, y: slot.y, width: slot.w, height: slot.h })));
  });

  it('scales the panel to a smaller player without letterboxing at the same aspect', () => {
    const fit = containFit({ width: 2560, height: 1440 }, { width: 1920, height: 1080 });
    expect(fit).toEqual({ scale: 0.75, offsetX: 0, offsetY: 0 });

    const first = computeZones(state(), { width: 1920, height: 1080 })[0];
    expect(first?.rect).toEqual({ x: 2369 * 0.75, y: 198 * 0.75, width: 48, height: 24 });
  });

  it('letterboxes vertically when the player is taller than the game aspect', () => {
    const fit = containFit({ width: 2560, height: 1440 }, { width: 1920, height: 1200 });
    expect(fit.scale).toBe(0.75);
    expect(fit.offsetY).toBe(60);

    const first = computeZones(state(), { width: 1920, height: 1200 })[0];
    expect(first?.rect.y).toBe(198 * 0.75 + 60);
    expect(first?.rect.x).toBe(2369 * 0.75);
  });

  it('letterboxes horizontally when the player is wider than the game aspect', () => {
    const player = { width: 2000, height: 1000 };
    const fit = containFit({ width: 2560, height: 1440 }, player);
    expect(fit.offsetX).toBeCloseTo(111.11, 2);

    const first = computeZones(state(), player)[0];
    expect(first?.rect.x).toBeCloseTo(fit.offsetX + 2369 * fit.scale, 6);
    expect(first?.rect.y).toBeCloseTo(198 * fit.scale, 6);
  });

  it('multiplies the logical offsets by the interface scale', () => {
    const scaled = state({ display: { width: 1920, height: 1080, uiScale: 2 } });
    const first = computeZones(scaled, { width: 1920, height: 1080 })[0];

    expect(first?.rect).toEqual({ x: 1920 - 191 * 2, y: 198 * 2, width: 128, height: 64 });
  });

  it('keeps the measured row counts whenever the window is tall enough', () => {
    expect(visibleRows(HERO_LIST, 1440)).toBe(8);
    expect(visibleRows(TOWN_LIST, 1440)).toBe(7);
    expect(visibleRows(HERO_LIST, 600)).toBe(8);
  });

  it('drops rows the window is too short to show', () => {
    expect(visibleRows(HERO_LIST, 300)).toBe(3);
    expect(visibleRows(HERO_LIST, 190)).toBe(0);
  });

  it('shows no more rows at scale 2 than the short window fits', () => {
    const small = state({ display: { width: 800, height: 600, uiScale: 2 } });
    const zones = computeZones(small, { width: 800, height: 600 });

    expect(zones.filter((zone) => zone.kind === 'hero')).toHaveLength(3);
  });

  it('shifts which entry a row shows by the list scroll offset', () => {
    const scrolled = state({
      player: { id: 0, name: 'p', currentHero: -1, heroListTop: 2, townListTop: 1 },
    });
    const zones = computeZones(scrolled, { width: 2560, height: 1440 });
    const heroes = zones.filter((zone) => zone.kind === 'hero');

    expect(heroes[0]).toMatchObject({ row: 0, entry: 2 });
    expect(heroes).toHaveLength(6);
    expect(heroes[0]?.rect.y).toBe(198);
  });

  it('exists only on the adventure map', () => {
    for (const screen of ['none', 'town', 'combat', 'other'] as const) {
      expect(computeZones(state({ screen }), { width: 2560, height: 1440 })).toHaveLength(0);
    }
  });

  it('needs a measured player', () => {
    expect(computeZones(state(), { width: 0, height: 0 })).toHaveLength(0);
  });
});
