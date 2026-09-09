/**
 * How many player pixels one card pixel takes. The game draws the popup at the HD Mod
 * interface scale, and the stream maps game pixels onto the player with the contain fit, so
 * the card matches the video at `uiScale * fitScale` — rounded to an integer so the bitmaps
 * and the bitmap fonts stay pixel-exact, and never below 1 so the card stays readable.
 */
export function cardScale(uiScale: number, fitScale: number): number {
    const exact = uiScale * fitScale;
    if (!Number.isFinite(exact) || exact <= 0) return 1;
    return Math.max(1, Math.round(exact));
}

/**
 * Shrinks the card until it fits the player's height. An expanded card is much taller than the
 * popup, so at a doubled interface scale it would otherwise run off the bottom of the video.
 */
export function fitScale(scale: number, cardHeight: number, playerHeight: number): number {
    if (cardHeight <= 0 || playerHeight <= 0) return scale;
    return Math.max(1, Math.min(scale, Math.floor(playerHeight / cardHeight)));
}

export interface Placement {
    readonly left: number;
    readonly top: number;
}

/**
 * Places the card next to the panel entry it belongs to: to its left, as the game opens the
 * popup beside the list, kept inside the player.
 */
export function placeCard(
    zone: { x: number; y: number; width: number; height: number },
    card: { width: number; height: number },
    player: { width: number; height: number },
    gap = 8,
): Placement {
    const preferred = zone.x - gap - card.width;
    const left = preferred >= 0 ? preferred : Math.min(zone.x + zone.width + gap, player.width - card.width);
    const top = zone.y;
    return {
        left: Math.max(0, Math.min(left, Math.max(0, player.width - card.width))),
        top: Math.max(0, Math.min(top, Math.max(0, player.height - card.height))),
    };
}
