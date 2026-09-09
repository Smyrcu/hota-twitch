import { fallbackAdvance, glyphFor, type FontAtlas } from './atlas.js';

export type TextAlign = 'left' | 'center' | 'right';

export interface TextOptions {
    readonly align?: TextAlign;
    /** Wraps on word boundaries; a single word longer than this is left overflowing. */
    readonly maxWidth?: number;
}

/** One glyph to blit: source rectangle in the atlas, destination relative to the text origin. */
export interface GlyphBlit {
    readonly code: number;
    readonly sx: number;
    readonly sy: number;
    readonly sw: number;
    readonly sh: number;
    readonly dx: number;
    readonly dy: number;
}

export interface TextLayout {
    readonly width: number;
    readonly height: number;
    readonly lines: number;
    readonly glyphs: readonly GlyphBlit[];
}

function advanceOf(atlas: FontAtlas, code: number, fallback: number): number {
    return glyphFor(atlas, code)?.advance ?? fallback;
}

function measure(atlas: FontAtlas, codes: readonly number[], fallback: number): number {
    return codes.reduce((width, code) => width + advanceOf(atlas, code, fallback), 0);
}

function codesOf(text: string): number[] {
    const codes: number[] = [];
    for (const character of text) codes.push(character.codePointAt(0) ?? 0);
    return codes;
}

function wrap(atlas: FontAtlas, text: string, maxWidth: number | undefined, fallback: number): number[][] {
    const lines: number[][] = [];
    for (const paragraph of text.split('\n')) {
        if (maxWidth === undefined) {
            lines.push(codesOf(paragraph));
            continue;
        }
        let current: number[] = [];
        for (const word of paragraph.split(' ')) {
            const codes = codesOf(word);
            const candidate = current.length === 0 ? codes : [...current, 32, ...codes];
            if (current.length > 0 && measure(atlas, candidate, fallback) > maxWidth) {
                lines.push(current);
                current = codes;
            } else {
                current = candidate;
            }
        }
        lines.push(current);
    }
    return lines;
}

function originFor(align: TextAlign, lineWidth: number, blockWidth: number): number {
    if (align === 'center') return Math.round((blockWidth - lineWidth) / 2);
    if (align === 'right') return blockWidth - lineWidth;
    return 0;
}

/**
 * Lays out a string glyph by glyph. Pure: it needs only the atlas descriptor, so it is the
 * unit that tests cover; the painter blits the result.
 */
export function layoutText(atlas: FontAtlas, text: string, options: TextOptions = {}): TextLayout {
    const fallback = fallbackAdvance(atlas);
    const lines = wrap(atlas, text, options.maxWidth, fallback);
    const widths = lines.map((codes) => measure(atlas, codes, fallback));
    const blockWidth = Math.max(0, ...widths);
    const align = options.align ?? 'left';
    const glyphs: GlyphBlit[] = [];

    lines.forEach((codes, line) => {
        let pen = originFor(align, widths[line] ?? 0, blockWidth);
        const top = line * atlas.lineHeight;
        for (const code of codes) {
            const glyph = glyphFor(atlas, code);
            if (glyph === undefined) {
                pen += fallback;
                continue;
            }
            if (glyph.w > 0 && glyph.h > 0) {
                glyphs.push({
                    code,
                    sx: glyph.x,
                    sy: glyph.y,
                    sw: glyph.w,
                    sh: glyph.h,
                    dx: pen + glyph.left,
                    dy: top,
                });
            }
            pen += glyph.advance;
        }
    });

    return { width: blockWidth, height: lines.length * atlas.lineHeight, lines: lines.length, glyphs };
}

export function measureText(atlas: FontAtlas, text: string, options: TextOptions = {}): number {
    return layoutText(atlas, text, options).width;
}
