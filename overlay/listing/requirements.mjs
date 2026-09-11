/**
 * What the Twitch developer console demands of a version before it can be submitted for review,
 * as documented on https://dev.twitch.tv/docs/extensions/life-cycle/ (Version Details).
 * `listing/README.md` quotes the page; this file is the same table in the form the renderer and
 * the checks read.
 */

export const DOCUMENTATION = 'https://dev.twitch.tv/docs/extensions/life-cycle/';

/**
 * The family the discovery image's word mark is set in, and the first in that file's stack. The
 * renderer refuses to draw without it rather than let a substitute change the asset silently.
 */
export const WORDMARK_FONT = 'Liberation Serif';

/**
 * The image assets. Every one of them is a PNG of an exact size; the documentation states no
 * file-size limit for them, so the cap here is our own and only guards against an asset that
 * turned out far heavier than a flat drawing should be.
 */
export const ASSETS = [
  {
    field: 'Logo Image',
    source: 'logo.svg',
    file: 'logo.png',
    width: 100,
    height: 100,
    maxBytes: 64 * 1024,
  },
  {
    field: 'Taskbar Icon Image',
    source: 'icon.svg',
    file: 'icon.png',
    width: 24,
    height: 24,
    maxBytes: 8 * 1024,
  },
  {
    field: 'Discovery Image',
    source: 'discovery.svg',
    file: 'discovery.png',
    width: 300,
    height: 200,
    maxBytes: 128 * 1024,
  },
];

/**
 * The screenshots. The documentation names 1024x768 as both the minimum and the recommended
 * size and requires a 4:3 aspect ratio, which no stream frame has: each shot is a 4:3 crop of
 * the 16:9 frame around the card and the game's panel. "Less than 10MB" is read as the decimal
 * megabyte, the smaller of the two readings.
 */
export const SCREENSHOT = {
  width: 1024,
  height: 768,
  maxBytes: 10_000_000,
};
