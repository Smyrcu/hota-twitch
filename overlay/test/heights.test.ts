import { describe, expect, it } from 'vitest';
import { cardFor, type Selection } from '../src/render/card.js';
import { CARD_HEIGHT } from '../src/render/layout.js';
import { MOCK_STATE } from './fixtures/state.js';

const state = MOCK_STATE;

const height = (selection: Selection): number => cardFor(state, selection)!.height;

describe('card height', () => {
  it('keeps the hover card at the size of the popup bitmap', () => {
    expect(height({ kind: 'hero', entry: 0, expanded: false, view: 'town' })).toBe(CARD_HEIGHT);
    expect(height({ kind: 'town', entry: 0, expanded: false, view: 'town' })).toBe(CARD_HEIGHT);
  });

  it('fits the widest fixtures into a 1080p player at scale 1', () => {
    const hero = height({ kind: 'hero', entry: 0, expanded: true, view: 'town' });
    const town = height({ kind: 'town', entry: 0, expanded: true, view: 'town' });

    expect(hero).toBeLessThanOrEqual(1080);
    expect(town).toBeLessThanOrEqual(1080);
  });
});
