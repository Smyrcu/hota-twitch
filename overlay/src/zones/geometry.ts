import type { Display } from '../state/protocol.js';

export interface Rect {
  readonly x: number;
  readonly y: number;
  readonly width: number;
  readonly height: number;
}

export interface Size {
  readonly width: number;
  readonly height: number;
}

/**
 * A list in the adventure map's right-hand panel, in logical pixels: the panel is anchored to
 * the top-right corner of the game window and drawn at the HD Mod interface scale, so an entry
 * is an offset from that corner multiplied by `display.uiScale`.
 *
 * `right` is the distance from the right edge of the window to the left edge of an entry.
 * `rows` is the number of entries the list widget holds; it was measured on the reference
 * screenshot (2560x1440, scale 1) and is the calibration open point of the design spec.
 */
export interface ListGeometry {
  readonly right: number;
  readonly top: number;
  readonly width: number;
  readonly height: number;
  readonly pitch: number;
  readonly rows: number;
}

export const HERO_LIST: ListGeometry = { right: 191, top: 198, width: 64, height: 32, pitch: 32, rows: 8 };
export const TOWN_LIST: ListGeometry = { right: 54, top: 214, width: 48, height: 32, pitch: 32, rows: 7 };

/** Maps the game frame onto the player with a contain fit, letterboxing the shorter axis. */
export interface Fit {
  readonly scale: number;
  readonly offsetX: number;
  readonly offsetY: number;
}

export function containFit(game: Size, player: Size): Fit {
  const scale = Math.min(player.width / game.width, player.height / game.height);
  return {
    scale,
    offsetX: (player.width - game.width * scale) / 2,
    offsetY: (player.height - game.height * scale) / 2,
  };
}

/**
 * How many entries of the list the window is tall enough to show. The list is a fixed-size
 * widget, so this is the measured row count unless the window is too short to hold it.
 */
export function visibleRows(list: ListGeometry, logicalHeight: number): number {
  const fits = Math.floor((logicalHeight - list.top) / list.pitch);
  return Math.max(0, Math.min(list.rows, fits));
}

/** The entry's rectangle in game pixels. */
export function rowRectInGame(list: ListGeometry, display: Display, row: number): Rect {
  const scale = display.uiScale;
  return {
    x: display.width - list.right * scale,
    y: (list.top + list.pitch * row) * scale,
    width: list.width * scale,
    height: list.height * scale,
  };
}

export function mapRect(rect: Rect, fit: Fit): Rect {
  return {
    x: fit.offsetX + rect.x * fit.scale,
    y: fit.offsetY + rect.y * fit.scale,
    width: rect.width * fit.scale,
    height: rect.height * fit.scale,
  };
}
