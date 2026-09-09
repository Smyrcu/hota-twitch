import { decodeJson } from '../state/decode.js';
import type { GameState } from '../state/protocol.js';
import { decodeBroadcast } from './broadcast.js';

export type StateSink = (state: GameState) => void;
export type ProblemSink = (reason: string) => void;

/**
 * Turns broadcast messages into states, applied `hlsLatencyBroadcaster` seconds late so the
 * card matches the video the viewer is watching. A message that does not decode is reported
 * and dropped: the previously applied state stays on screen.
 */
export class BroadcastReceiver {
  private latencyMs = 0;
  private applied = 0;
  private timers = new Set<ReturnType<typeof setTimeout>>();

  constructor(
    private readonly onState: StateSink,
    private readonly onProblem: ProblemSink = () => {},
  ) {}

  setLatencySeconds(seconds: number): void {
    this.latencyMs = Number.isFinite(seconds) && seconds > 0 ? seconds * 1000 : 0;
  }

  async handle(message: string): Promise<void> {
    const json = await decodeBroadcast(message);
    if (json === null) {
      this.onProblem('broadcast: cannot decompress');
      return;
    }
    const result = decodeJson(json);
    if (!result.ok) {
      this.onProblem(result.reason);
      return;
    }
    this.schedule(result.state);
  }

  /** Drops states that have not been applied yet, for when the viewer leaves the page. */
  cancel(): void {
    for (const timer of this.timers) clearTimeout(timer);
    this.timers.clear();
  }

  private schedule(state: GameState): void {
    if (this.latencyMs <= 0) {
      this.apply(state);
      return;
    }
    const timer = setTimeout(() => {
      this.timers.delete(timer);
      this.apply(state);
    }, this.latencyMs);
    this.timers.add(timer);
  }

  /**
     * Never steps backwards. A latency change re-times the messages still in flight, and two
     * decodes can finish out of order, so an older document must not overwrite a newer one.
     */
  private apply(state: GameState): void {
    if (state.ts <= this.applied) return;
    this.applied = state.ts;
    this.onState(state);
  }
}
