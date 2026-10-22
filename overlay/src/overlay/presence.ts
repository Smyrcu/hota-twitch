import { outranks, presenceOf, type Presence } from '../state/presence.js';
import type { GameState } from '../state/protocol.js';

/**
 * How long the overlay waits for the streamer's state before calling the overlay inactive. PubSub
 * does not replay earlier messages, so a viewer who just arrived hears only what is sent next: the
 * producer posts at least every 10 s while a game is loaded, the backend coalesces to one message a
 * second, and the helper needs a moment to subscribe. A state that still comes later corrects the
 * verdict while the notice is up.
 */
export const PRESENCE_WAIT_MS = 15_000;

/** How long the notice stays after its latest verdict before it fades on its own. */
export const NOTICE_VISIBLE_MS = 7_000;

export interface PresenceHandlers {
  readonly show: (presence: Presence) => void;
  readonly hide: () => void;
}

type Phase =
  | { readonly kind: 'idle' }
  | { readonly kind: 'waiting' }
  | { readonly kind: 'showing'; readonly presence: Presence }
  | { readonly kind: 'done' };

/**
 * Decides once per page load whether the streamer's game is feeding the overlay, shows the verdict
 * for a few seconds and then lets it go. A state from a loaded game decides at once; silence until
 * the deadline decides `inactive`. While the notice is up a stronger verdict replaces a weaker one
 * and restarts its clock; once it has gone, later states change nothing.
 */
export class PresenceWatch {
  private phase: Phase = { kind: 'idle' };
  private timer: ReturnType<typeof setTimeout> | null = null;

  constructor(private readonly handlers: PresenceHandlers) {}

  start(): void {
    if (this.phase.kind !== 'idle') return;
    this.phase = { kind: 'waiting' };
    this.arm(() => this.decide('inactive'), PRESENCE_WAIT_MS);
  }

  received(state: GameState): void {
    const presence = presenceOf(state);
    if (presence === null) return;
    const phase = this.phase;
    if (phase.kind === 'waiting' || (phase.kind === 'showing' && outranks(presence, phase.presence))) {
      this.decide(presence);
    }
  }

  /** The viewer has found the panel, so the notice has done its job. */
  dismiss(): void {
    if (this.phase.kind === 'showing') this.finish();
  }

  /** Ends the watch; a notice still up is hidden with it. */
  stop(): void {
    if (this.phase.kind === 'showing') this.handlers.hide();
    this.clear();
    this.phase = { kind: 'done' };
  }

  private decide(presence: Presence): void {
    this.phase = { kind: 'showing', presence };
    this.handlers.show(presence);
    this.arm(() => this.finish(), NOTICE_VISIBLE_MS);
  }

  private finish(): void {
    this.clear();
    this.phase = { kind: 'done' };
    this.handlers.hide();
  }

  private arm(callback: () => void, delay: number): void {
    this.clear();
    this.timer = setTimeout(callback, delay);
  }

  private clear(): void {
    if (this.timer !== null) clearTimeout(this.timer);
    this.timer = null;
  }
}
