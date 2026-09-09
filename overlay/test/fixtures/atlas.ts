import type { FontAtlas } from '../../src/render/text/atlas.js';

/**
 * A synthetic atlas in the format `tools/` exports: fixed 6x10 glyphs for A, B and a space,
 * plus a narrow 'i' so tests can tell advance and left side bearing apart.
 */
export const TEST_ATLAS: FontAtlas = {
    name: 'test',
    lineHeight: 10,
    glyphs: {
        '32': { x: 0, y: 0, w: 0, h: 0, left: 0, advance: 4 },
        '65': { x: 0, y: 0, w: 6, h: 10, left: 0, advance: 7 },
        '66': { x: 6, y: 0, w: 6, h: 10, left: 0, advance: 7 },
        '105': { x: 12, y: 0, w: 2, h: 10, left: 1, advance: 4 },
    },
};
