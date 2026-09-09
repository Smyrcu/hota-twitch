import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import { decodeJson } from '../src/state/decode.js';
import { cardFor, type Selection } from '../src/render/card.js';
import { fitScale } from '../src/overlay/scale.js';
import { CARD_HEIGHT } from '../src/render/layout.js';

const result = decodeJson(readFileSync(new URL('../dev/state.json', import.meta.url), 'utf8'));
if (!result.ok) throw new Error(result.reason);
const state = result.state;

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

    it('shrinks the card instead of letting it run off the video', () => {
        expect(fitScale(2, 800, 1080)).toBe(1);
        expect(fitScale(2, 186, 1080)).toBe(2);
        expect(fitScale(3, 300, 1080)).toBe(3);
        expect(fitScale(2, 2000, 1080)).toBe(1);
        expect(fitScale(2, 0, 1080)).toBe(2);
    });
});
