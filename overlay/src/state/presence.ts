import type { GameState } from './protocol.js';

/**
 * Whether the streamer's game is feeding the overlay: `active` with the adventure map on screen,
 * where the cards open; `paused` with a game loaded but another screen up; `inactive` when no
 * game has been heard. The order is the order of evidence, so a later verdict only ever upgrades.
 */
export type Presence = 'inactive' | 'paused' | 'active';

const RANK: Readonly<Record<Presence, number>> = { inactive: 0, paused: 1, active: 2 };

export const outranks = (candidate: Presence, current: Presence): boolean => RANK[candidate] > RANK[current];

/** A reader with no game loaded says nothing yet about whether one is coming. */
export function presenceOf(state: GameState): Presence | null {
  if (state.screen === 'none') return null;
  return state.screen === 'adventure' ? 'active' : 'paused';
}
