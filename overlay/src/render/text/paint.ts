import type { FontId, TextColour } from '../../data/fonts.js';
import type { FontStore } from '../fonts.js';
import { layoutText, type GlyphBlit } from './layout.js';

/** Blits a laid-out string, moving it so that its origin lands at `x`, `y`. */
export function blitGlyphs(
  context: CanvasRenderingContext2D,
  sheet: CanvasImageSource,
  glyphs: readonly GlyphBlit[],
  x: number,
  y: number,
): void {
  for (const glyph of glyphs) {
    context.drawImage(sheet, glyph.sx, glyph.sy, glyph.sw, glyph.sh, x + glyph.dx, y + glyph.dy, glyph.sw, glyph.sh);
  }
}

export interface LabelStyle {
  readonly font: FontId;
  readonly colour: TextColour;
}

/**
 * Draws a run of page text into a canvas in one of the game's bitmap fonts, wrapped to `maxWidth`.
 * The glyphs are blitted at a whole multiple and the canvas is presented at its logical size, so
 * the text stays hard-edged on a dense display and is never resampled at a fractional scale.
 *
 * Returns false when the font has not loaded, so the caller can leave plain text in its place.
 */
export function paintLabel(
  canvas: HTMLCanvasElement,
  fonts: FontStore,
  text: string,
  style: LabelStyle,
  maxWidth: number,
  supersample: number,
): boolean {
  const atlas = fonts.atlas(style.font);
  const sheet = fonts.sheet(style.font, style.colour);
  const context = canvas.getContext('2d');
  if (atlas === null || sheet === null || context === null) return false;

  const layout = layoutText(atlas, text, { maxWidth });
  const width = Math.max(1, layout.width);
  const height = Math.max(1, layout.height);
  canvas.width = width * supersample;
  canvas.height = height * supersample;
  canvas.style.width = `${width}px`;
  canvas.style.height = `${height}px`;
  context.setTransform(supersample, 0, 0, supersample, 0, 0);
  context.imageSmoothingEnabled = false;
  blitGlyphs(context, sheet, layout.glyphs, 0, 0);
  return true;
}
