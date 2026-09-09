import { decodeJson } from '../state/decode.js';
import type { GameState } from '../state/protocol.js';

const MOCK_SOURCE = 'dev/state.json';
const POLL_MS = 1000;

/**
 * Development source of state: polls a hand-written document so the pages can be worked on and
 * screenshotted without the game, the plugin or Twitch.
 */
export function pollMock(onState: (state: GameState) => void, onProblem: (reason: string) => void): () => void {
  let lastText = '';

  const tick = async (): Promise<void> => {
    try {
      const response = await fetch(`${MOCK_SOURCE}?t=${Date.now()}`);
      if (!response.ok) return;
      const text = await response.text();
      if (text === lastText) return;
      lastText = text;
      const result = decodeJson(text);
      if (result.ok) onState(result.state);
      else onProblem(result.reason);
    } catch {
      onProblem('mock: cannot read dev/state.json');
    }
  };

  void tick();
  const timer = setInterval(() => void tick(), POLL_MS);
  return () => clearInterval(timer);
}
