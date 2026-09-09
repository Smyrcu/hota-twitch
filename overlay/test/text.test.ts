import { describe, expect, it } from 'vitest';
import { layoutText, measureText } from '../src/render/text/layout.js';
import { parseAtlas } from '../src/render/text/atlas.js';
import { TEST_ATLAS } from './fixtures/atlas.js';

describe('bitmap text layout', () => {
    it('advances the pen by the glyph advance and applies the left bearing', () => {
        const layout = layoutText(TEST_ATLAS, 'AiB');

        expect(layout.width).toBe(7 + 4 + 7);
        expect(layout.height).toBe(10);
        expect(layout.glyphs.map((glyph) => glyph.dx)).toEqual([0, 8, 11]);
        expect(layout.glyphs[1]).toMatchObject({ sx: 12, sw: 2, dy: 0 });
    });

    it('skips glyphs with no pixels but keeps their advance', () => {
        expect(layoutText(TEST_ATLAS, 'A B').glyphs).toHaveLength(2);
        expect(measureText(TEST_ATLAS, 'A B')).toBe(7 + 4 + 7);
    });

    it('uses the space advance for characters the atlas does not have', () => {
        expect(measureText(TEST_ATLAS, 'AŻB')).toBe(7 + 4 + 7);
    });

    it('wraps on word boundaries and reports the block height', () => {
        const layout = layoutText(TEST_ATLAS, 'AA BB AA', { maxWidth: 40 });

        expect(layout.lines).toBe(2);
        expect(layout.height).toBe(20);
        expect(layout.width).toBe(7 * 2 + 4 + 7 * 2);
    });

    it('keeps an explicit line break', () => {
        expect(layoutText(TEST_ATLAS, 'A\nB').lines).toBe(2);
    });

    it('centres shorter lines inside the block', () => {
        const layout = layoutText(TEST_ATLAS, 'AA\nA', { align: 'center' });
        const second = layout.glyphs.filter((glyph) => glyph.dy === 10);

        expect(second[0]?.dx).toBe(4);
    });

    it('rejects a descriptor that is not in the exported format', () => {
        expect(parseAtlas({ name: 'x', lineHeight: 10, glyphs: { '65': { x: 0 } } })).toBeNull();
        expect(parseAtlas({ name: 'x', glyphs: {} })).toBeNull();
        expect(parseAtlas(TEST_ATLAS)).not.toBeNull();
    });
});
