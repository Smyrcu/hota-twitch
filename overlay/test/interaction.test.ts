import { describe, expect, it } from 'vitest';
import { advance, matches, selectionFor } from '../src/overlay/interaction.js';
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
