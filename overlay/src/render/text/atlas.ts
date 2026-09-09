/** One glyph in a font atlas exported by `tools/` (spec section 3, "Fonts"). */
export interface Glyph {
    readonly x: number;
    readonly y: number;
    readonly w: number;
    readonly h: number;
    /** Left side bearing: horizontal offset from the pen position to the glyph box. */
    readonly left: number;
    /** How far the pen moves after the glyph. */
    readonly advance: number;
}

export interface FontAtlas {
    readonly name: string;
    readonly lineHeight: number;
    /** Keyed by character code, as exported. */
    readonly glyphs: Readonly<Record<string, Glyph>>;
}

const SPACE = 32;

export function glyphFor(atlas: FontAtlas, code: number): Glyph | undefined {
    return atlas.glyphs[String(code)];
}

/** Advance used for characters the atlas does not contain, so text keeps its rhythm. */
export function fallbackAdvance(atlas: FontAtlas): number {
    return glyphFor(atlas, SPACE)?.advance ?? Math.round(atlas.lineHeight / 2);
}

function isGlyph(value: unknown): value is Glyph {
    if (typeof value !== 'object' || value === null) return false;
    const raw = value as Record<string, unknown>;
    return (['x', 'y', 'w', 'h', 'left', 'advance'] as const).every(
        (key) => typeof raw[key] === 'number' && Number.isFinite(raw[key]),
    );
}

/** Validates an atlas descriptor. Returns null when the file does not match the format. */
export function parseAtlas(value: unknown): FontAtlas | null {
    if (typeof value !== 'object' || value === null) return null;
    const raw = value as Record<string, unknown>;
    const name = raw['name'];
    const lineHeight = raw['lineHeight'];
    const glyphs = raw['glyphs'];
    if (typeof name !== 'string' || typeof lineHeight !== 'number' || lineHeight <= 0) return null;
    if (typeof glyphs !== 'object' || glyphs === null) return null;
    const parsed: Record<string, Glyph> = {};
    for (const [code, glyph] of Object.entries(glyphs as Record<string, unknown>)) {
        if (!isGlyph(glyph)) return null;
        parsed[code] = glyph;
    }
    return { name, lineHeight, glyphs: parsed };
}
