import type { Rect, Size } from '../zones/index.js';

/**
 * How many player pixels one card pixel takes. The game draws the popup at the HD Mod interface
 * scale and the stream then resizes the whole frame onto the player, so the card sits on the
 * video at exactly `uiScale * containFit`. The value is deliberately left fractional: rounding it
 * would make the card a different size from the panel it belongs to.
 */
export function cardScale(uiScale: number, containFit: number): number {
  const exact = uiScale * containFit;
  return Number.isFinite(exact) && exact > 0 ? exact : 1;
}

/**
 * Shrinks the card until it fits the picture. An expanded card is several times taller than the
 * popup, so at a large interface scale it would otherwise run off the video — and the bound is
 * the picture rather than the player, or a letterboxed stream would grow the card into the bars.
 */
export function shrinkToFit(scale: number, card: Size, video: Size): number {
  const limits = [scale];
  if (card.width > 0 && video.width > 0) limits.push(video.width / card.width);
  if (card.height > 0 && video.height > 0) limits.push(video.height / card.height);
  return Math.min(...limits);
}

/**
 * The integer factor the card is rasterised at before being presented at its exact size. Drawing
 * at a whole multiple keeps the bitmaps and the bitmap fonts pixel-exact; the browser then does
 * the fractional step with smooth interpolation, so the card degrades the way the video does
 * instead of dropping glyph rows.
 */
export function supersampleFactor(scale: number): number {
  return Number.isFinite(scale) && scale > 1 ? Math.ceil(scale) : 1;
}

export interface Placement {
  readonly left: number;
  readonly top: number;
}

/**
 * Places the card next to the panel entry it belongs to: to its left, as the game opens the popup
 * beside the list, kept inside the picture rather than the letterbox bars around it.
 */
export function placeCard(zone: Rect, card: Size, video: Rect, gap = 8): Placement {
  const right = video.x + video.width;
  const preferred = zone.x - gap - card.width;
  const left = preferred >= video.x ? preferred : Math.min(zone.x + zone.width + gap, right - card.width);
  return {
    left: clamp(left, video.x, Math.max(video.x, right - card.width)),
    top: clamp(zone.y, video.y, Math.max(video.y, video.y + video.height - card.height)),
  };
}

const clamp = (value: number, low: number, high: number): number => Math.max(low, Math.min(value, high));
