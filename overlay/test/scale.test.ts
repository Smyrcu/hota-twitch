import { describe, expect, it } from 'vitest';
import { MIN_CARD_SCALE, cardScale, placeCard, shrinkToFit, supersampleFactor } from '../src/overlay/scale.js';
import { CARD_HEIGHT, CARD_WIDTH } from '../src/render/layout.js';
import { containFit, videoRect } from '../src/zones/index.js';

const card = { width: CARD_WIDTH, height: CARD_HEIGHT };

/** What the card occupies on the video, the way the page computes it. */
function onScreen(game: { width: number; height: number }, player: { width: number; height: number }): number {
  return cardScale(1.5, containFit(game, player).scale);
}

describe('card scale', () => {
  it('matches the popup drawn at the interface scale and resized with the video', () => {
    const game = { width: 1920, height: 1080 };

    // A player showing the stream one to one: the popup keeps the size the game drew it at.
    expect(onScreen(game, { width: 1920, height: 1080 })).toBeCloseTo(1.5, 10);
    expect(CARD_WIDTH * onScreen(game, { width: 1920, height: 1080 })).toBeCloseTo(291, 10);

    // Two thirds of the width, so a 1.5 interface scale lands back on the bitmap's own size.
    expect(onScreen(game, { width: 1280, height: 720 })).toBeCloseTo(1, 10);
    expect(CARD_WIDTH * onScreen(game, { width: 1280, height: 720 })).toBeCloseTo(194, 10);
  });

  it('keeps the fractional value instead of snapping the card to whole pixels', () => {
    expect(cardScale(1.5, 0.75)).toBeCloseTo(1.125, 10);
    expect(cardScale(2, 0.55)).toBeCloseTo(1.1, 10);
  });

  it('does not size the card below its bitmaps to match a small player', () => {
    const game = { width: 2560, height: 1440 };

    // A 1440p stream at interface scale 1 on a 854x480 player would put the card at 65 pixels.
    const small = cardScale(1, containFit(game, { width: 854, height: 480 }).scale);
    expect(small).toBe(MIN_CARD_SCALE);
    expect(small).toBeGreaterThan(containFit(game, { width: 854, height: 480 }).scale);
    expect(cardScale(1, containFit(game, { width: 2560, height: 1440 }).scale)).toBe(MIN_CARD_SCALE);
  });

  it('still shrinks a floored card that is taller than the picture, so it always fits', () => {
    const game = { width: 2560, height: 1440 };
    const player = { width: 854, height: 480 };
    const video = videoRect(game, containFit(game, player));
    const target = cardScale(1, containFit(game, player).scale);

    expect(shrinkToFit(target, card, video)).toBe(MIN_CARD_SCALE);
    expect(shrinkToFit(target, { width: CARD_WIDTH, height: 864 }, video)).toBeCloseTo(480 / 864, 10);
  });

  it('falls back to the bitmap size when the numbers are unusable', () => {
    expect(cardScale(0, 1)).toBe(1);
    expect(cardScale(Number.NaN, 1)).toBe(1);
    expect(cardScale(-2, 1)).toBe(1);
  });

  it('scales with the picture, not with the player, on either axis', () => {
    const game = { width: 1920, height: 1080 };

    expect(videoRect(game, containFit(game, { width: 1920, height: 1200 }))).toEqual({
      x: 0,
      y: 60,
      width: 1920,
      height: 1080,
    });
    expect(onScreen(game, { width: 1920, height: 1200 })).toBeCloseTo(1.5, 10);

    expect(videoRect(game, containFit(game, { width: 2400, height: 1080 }))).toEqual({
      x: 240,
      y: 0,
      width: 1920,
      height: 1080,
    });
    expect(onScreen(game, { width: 2400, height: 1080 })).toBeCloseTo(1.5, 10);
  });
});

describe('supersampling factor', () => {
  it('rasterises at the next whole multiple so the pixels stay hard-edged', () => {
    expect(supersampleFactor(1.5)).toBe(2);
    expect(supersampleFactor(2)).toBe(2);
    expect(supersampleFactor(2.01)).toBe(3);
  });

  it('never magnifies a card that is presented smaller than the bitmap', () => {
    expect(supersampleFactor(1)).toBe(1);
    expect(supersampleFactor(0.6)).toBe(1);
    expect(supersampleFactor(Number.NaN)).toBe(1);
  });
});

describe('fitting the card to the picture', () => {
  const video = { width: 1920, height: 1080 };

  it('leaves the card alone when it fits', () => {
    expect(shrinkToFit(1.5, card, video)).toBeCloseTo(1.5, 10);
  });

  it('shrinks an expansion that would run off the bottom of the video', () => {
    expect(shrinkToFit(2, { width: CARD_WIDTH, height: 800 }, video)).toBeCloseTo(1.35, 10);
    expect(shrinkToFit(1, { width: CARD_WIDTH, height: 800 }, video)).toBeCloseTo(1, 10);
  });

  it('shrinks a card wider than the picture as well', () => {
    expect(shrinkToFit(4, card, { width: 400, height: 4000 })).toBeCloseTo(400 / CARD_WIDTH, 10);
  });

  it('bounds an expansion by the picture, not by the letterbox bars around it', () => {
    const game = { width: 1920, height: 1080 };
    const tall = { width: CARD_WIDTH, height: 800 };
    const wide = videoRect(game, containFit(game, { width: 1920, height: 1200 }));

    // The same stream in a taller player still shows a 1080-tall picture, so the card is the same.
    expect(wide.height).toBe(1080);
    expect(shrinkToFit(2, tall, wide)).toBeCloseTo(shrinkToFit(2, tall, { width: 1920, height: 1080 }), 10);
  });

  it('ignores a degenerate card or picture instead of collapsing the scale', () => {
    expect(shrinkToFit(2, { width: 0, height: 0 }, video)).toBe(2);
    expect(shrinkToFit(2, card, { width: 0, height: 0 })).toBe(2);
  });
});

describe('card placement', () => {
  const full = { x: 0, y: 0, width: 1920, height: 1080 };
  const size = { width: 291, height: 279 };

  it('opens the card to the left of the entry, as the game does', () => {
    expect(placeCard({ x: 1633, y: 297, width: 96, height: 48 }, size, full)).toEqual({ left: 1334, top: 297 });
  });

  it('flips to the right when there is no room on the left', () => {
    expect(placeCard({ x: 10, y: 0, width: 96, height: 48 }, size, full).left).toBe(114);
  });

  it('keeps the card inside the picture', () => {
    const at = placeCard({ x: 1900, y: 1060, width: 96, height: 48 }, size, full);

    expect(at.left).toBeGreaterThanOrEqual(0);
    expect(at.top).toBe(1080 - 279);
  });

  it('stays out of the letterbox bars', () => {
    const game = { width: 1920, height: 1080 };
    const letterboxed = videoRect(game, containFit(game, { width: 1920, height: 1200 }));
    const pillarboxed = videoRect(game, containFit(game, { width: 2400, height: 1080 }));

    expect(letterboxed).toEqual({ x: 0, y: 60, width: 1920, height: 1080 });
    expect(placeCard({ x: 900, y: 0, width: 96, height: 48 }, size, letterboxed).top).toBe(60);
    expect(placeCard({ x: 900, y: 2000, width: 96, height: 48 }, size, letterboxed).top).toBe(60 + 1080 - 279);

    expect(pillarboxed).toEqual({ x: 240, y: 0, width: 1920, height: 1080 });
    expect(placeCard({ x: 250, y: 0, width: 96, height: 48 }, size, pillarboxed).left).toBe(250 + 96 + 8);
    expect(placeCard({ x: 3000, y: 0, width: 96, height: 48 }, size, pillarboxed).left).toBe(240 + 1920 - 291);
  });
});
