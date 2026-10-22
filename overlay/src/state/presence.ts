import type { GameState } from './protocol.js';

/**
 * How the streamer's game is feeding the overlay: `active` with the adventure map on screen, where
 * the cards open; `paused` with a game loaded but another screen up.
 */
export type Presence = 'active' | 'paused';

/** A reader with no game loaded says nothing yet about whether one is coming. */
export function presenceOf(state: GameState): Presence | null {
  if (state.screen === 'none') return null;
  return state.screen === 'adventure' ? 'active' : 'paused';
}
