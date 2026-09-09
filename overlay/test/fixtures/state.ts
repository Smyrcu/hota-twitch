import { readFileSync } from 'node:fs';
import { decodeJson } from '../../src/state/decode.js';
import type { GameState } from '../../src/state/protocol.js';

/** The document the mock mode serves, which is also the fixture the render tests draw. */
export const MOCK_JSON = readFileSync(new URL('../../dev/state.json', import.meta.url), 'utf8');

function load(): GameState {
  const result = decodeJson(MOCK_JSON);
  if (!result.ok) throw new Error(`dev/state.json does not match the contract: ${result.reason}`);
  return result.state;
}

export const MOCK_STATE: GameState = load();
