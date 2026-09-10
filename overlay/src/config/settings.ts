/**
 * The HD Mod interface scale the streamer plays with. The contract allows anything from 1 to 4
 * with two decimals; these are the steps the mod itself offers, which is what the page lists.
 */
export const UI_SCALES: readonly number[] = [1, 1.25, 1.5, 1.75, 2, 3, 4];

const MIN_UI_SCALE = 1;
const MAX_UI_SCALE = 4;

export const DEFAULT_UI_SCALE = MIN_UI_SCALE;

/** Reads a scale the backend sent. Anything outside the contract falls back to a plain 1. */
export function readUiScale(value: unknown): number {
  if (typeof value !== 'number' || !Number.isFinite(value)) return DEFAULT_UI_SCALE;
  if (value < MIN_UI_SCALE || value > MAX_UI_SCALE) return DEFAULT_UI_SCALE;
  return value;
}

/** How a scale is written in the page and sent to the backend: 1, 1.25, 1.5 — never 1.50. */
export const formatUiScale = (value: number): string => String(value);

/** The steps to offer, with the channel's own value folded in when it is not one of them. */
export function uiScaleOptions(current: number): readonly number[] {
  return UI_SCALES.includes(current) ? UI_SCALES : [...UI_SCALES, current].sort((left, right) => left - right);
}

/**
 * What the control shows while a choice is on its way to the backend: the streamer's own pick
 * stands until the backend answers with it, so a poll landing mid-flight cannot undo it.
 */
export function settleUiScale(chosen: number | null, stored: number): { show: number; chosen: number | null } {
  return chosen === null || chosen === stored ? { show: stored, chosen: null } : { show: chosen, chosen };
}
