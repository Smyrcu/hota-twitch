import { presenceOf, type Presence } from '../state/presence.js';
import type { GameState } from '../state/protocol.js';

/** How long the notice stays before it fades on its own. */
export const NOTICE_VISIBLE_MS = 7_000;

export interface PresenceHandlers {
  readonly show: (presence: Presence) => void;
  readonly hide: () => void;
}

type Phase = 'waiting' | 'showing' | 'done';

/**
 * Shows the notice once per page load, on the first state from a loaded game, for a few seconds.
 * PubSub does not replay earlier messages, so that state is fresh whenever it comes: a game the
 * streamer starts later is announced then. Until a game is heard nothing is shown.
 */
export class PresenceWatch {
  private phase: Phase = 'waiting';
  private timer: ReturnType<typeof setTimeout> | null = null;

  constructor(private readonly handlers: PresenceHandlers) {}

  received(state: GameState): void {
    if (this.phase !== 'waiting') return;
    const presence = presenceOf(state);
    if (presence === null) return;
    this.phase = 'showing';
    this.handlers.show(presence);
    this.timer = setTimeout(() => this.finish(), NOTICE_VISIBLE_MS);
  }

  /** The viewer has found the panel, so the notice has done its job. */
  dismiss(): void {
    if (this.phase === 'showing') this.finish();
  }

  /** Ends the watch; a notice still up is hidden with it. */
  stop(): void {
    if (this.phase === 'showing') this.handlers.hide();
    this.clear();
    this.phase = 'done';
  }

  private finish(): void {
    this.clear();
    this.phase = 'done';
    this.handlers.hide();
  }

  private clear(): void {
    if (this.timer !== null) clearTimeout(this.timer);
    this.timer = null;
  }
}
