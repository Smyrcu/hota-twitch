/**
 * The game fonts the cards use. The key is the id used across the renderer, the value is the
 * base name of the atlas pair exported by `tools/` into `assets/fonts/`.
 */
export const FONT_FILES = {
  big: 'bigfont',
  medium: 'medfont',
  small: 'smalfont',
  tiny: 'tiny',
} as const;

export type FontId = keyof typeof FONT_FILES;

export const FONT_IDS = Object.keys(FONT_FILES) as readonly FontId[];

/** Where the atlases ship. `dev/fonts` holds stand-ins used only by the mock mode. */
export const FONT_ROOT = 'assets/fonts';
export const DEV_FONT_ROOT = 'dev/fonts';

export const fontAtlasPath = (root: string, font: FontId): string => `${root}/${FONT_FILES[font]}.json`;
export const fontImagePath = (root: string, font: FontId): string => `${root}/${FONT_FILES[font]}.png`;

/** The game's text colours: white for body text, yellow for headings, gold for values. */
export const TEXT_COLOURS = {
  white: '#ffffff',
  yellow: '#ffe794',
  gold: '#d4b24c',
  grey: '#9c9c9c',
} as const;

export type TextColour = keyof typeof TEXT_COLOURS;
