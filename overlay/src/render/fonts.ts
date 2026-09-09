import { FONT_IDS, FONT_ROOT, TEXT_COLOURS, fontAtlasPath, fontImagePath, type FontId, type TextColour } from '../data/fonts.js';
import { parseAtlas, type FontAtlas } from './text/atlas.js';
import { layoutText } from './text/layout.js';
import type { LineCounter } from './build.js';

interface LoadedFont {
    readonly atlas: FontAtlas;
    readonly image: HTMLImageElement;
    readonly tinted: Map<TextColour, HTMLCanvasElement>;
}

function tint(image: HTMLImageElement, colour: string): HTMLCanvasElement {
    const canvas = document.createElement('canvas');
    canvas.width = image.naturalWidth;
    canvas.height = image.naturalHeight;
    const context = canvas.getContext('2d');
    if (context === null) return canvas;
    context.imageSmoothingEnabled = false;
    context.drawImage(image, 0, 0);
    context.globalCompositeOperation = 'source-in';
    context.fillStyle = colour;
    context.fillRect(0, 0, canvas.width, canvas.height);
    return canvas;
}

function loadImage(src: string): Promise<HTMLImageElement> {
    return new Promise((resolve, reject) => {
        const image = new Image();
        image.addEventListener('load', () => resolve(image));
        image.addEventListener('error', () => reject(new Error(`cannot load ${src}`)));
        image.src = src;
    });
}

async function loadFont(root: string, font: FontId): Promise<LoadedFont | null> {
    try {
        const response = await fetch(fontAtlasPath(root, font));
        if (!response.ok) return null;
        const atlas = parseAtlas(await response.json());
        if (atlas === null) return null;
        const image = await loadImage(fontImagePath(root, font));
        return { atlas, image, tinted: new Map() };
    } catch {
        return null;
    }
}

async function loadFirst(roots: readonly string[], font: FontId): Promise<LoadedFont | null> {
    for (const root of roots) {
        const loaded = await loadFont(root, font);
        if (loaded !== null) return loaded;
    }
    return null;
}

/**
 * The game's bitmap fonts. The atlases hold white glyphs with alpha, so a colour is applied
 * once per (font, colour) into an offscreen canvas rather than per glyph on every repaint.
 */
export class FontStore {
    private readonly fonts = new Map<FontId, LoadedFont>();

    /** Later roots are stand-ins used when a font has not been exported into the bundle yet. */
    async load(roots: readonly string[] = [FONT_ROOT]): Promise<void> {
        const loaded = await Promise.all(FONT_IDS.map(async (font) => [font, await loadFirst(roots, font)] as const));
        for (const [font, result] of loaded) {
            if (result !== null) this.fonts.set(font, result);
        }
    }

    atlas(font: FontId): FontAtlas | null {
        return this.fonts.get(font)?.atlas ?? null;
    }

    sheet(font: FontId, colour: TextColour): CanvasImageSource | null {
        const loaded = this.fonts.get(font);
        if (loaded === undefined) return null;
        const cached = loaded.tinted.get(colour);
        if (cached !== undefined) return cached;
        const painted = tint(loaded.image, TEXT_COLOURS[colour]);
        loaded.tinted.set(colour, painted);
        return painted;
    }

    /** Counts wrapped lines in the font the card expansions use. */
    lineCounter(font: FontId): LineCounter {
        return (value, maxWidth) => {
            const atlas = this.atlas(font);
            return atlas === null ? 1 : layoutText(atlas, value, { maxWidth }).lines;
        };
    }
}
