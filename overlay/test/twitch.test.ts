import { gzipSync } from 'node:zlib';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { decodeBroadcast } from '../src/twitch/broadcast.js';
import { BroadcastReceiver } from '../src/twitch/receiver.js';
import { parseDisplayResolution } from '../src/twitch/ext.js';
import type { GameState } from '../src/state/protocol.js';
import { MOCK_JSON } from './fixtures/state.js';

const MOCK = MOCK_JSON;
const encoded = `gz:${gzipSync(Buffer.from(MOCK, 'utf8')).toString('base64')}`;

describe('broadcast payloads', () => {
  it('decompresses what the backend sends', async () => {
    expect(await decodeBroadcast(encoded)).toBe(MOCK);
  });

  it('rejects a payload that is not the gz: format of the contract', async () => {
    expect(await decodeBroadcast('{"v":1}')).toBeNull();
  });

  it('returns null for a payload that is not gzip', async () => {
    expect(await decodeBroadcast('gz:bm90IGd6aXA=')).toBeNull();
    expect(await decodeBroadcast('gz:!!!')).toBeNull();
  });

  it('reads the player resolution the helper reports', () => {
    expect(parseDisplayResolution('1920x1080')).toEqual({ width: 1920, height: 1080 });
    expect(parseDisplayResolution('bad')).toBeNull();
    expect(parseDisplayResolution(undefined)).toBeNull();
  });
});

describe('broadcast receiver', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  it('applies a state immediately when the stream has no latency', async () => {
    const states: GameState[] = [];
    const receiver = new BroadcastReceiver((state) => states.push(state));

    await receiver.handle(encoded);

    expect(states).toHaveLength(1);
    expect(states[0]?.player.name).toBe('HaveFunMate');
  });

  it('holds the state for the broadcaster latency so it matches the video', async () => {
    const states: GameState[] = [];
    const receiver = new BroadcastReceiver((state) => states.push(state));
    receiver.setLatencySeconds(4);

    await receiver.handle(encoded);
    expect(states).toHaveLength(0);

    vi.advanceTimersByTime(3999);
    expect(states).toHaveLength(0);

    vi.advanceTimersByTime(1);
    expect(states).toHaveLength(1);
  });

  it('keeps the previous state when a message is malformed', async () => {
    const states: GameState[] = [];
    const problems: string[] = [];
    const receiver = new BroadcastReceiver(
      (state) => states.push(state),
      (reason) => problems.push(reason),
    );

    await receiver.handle('gz:not base64 at all');
    await receiver.handle('{"v":2}');
    await receiver.handle('nonsense');

    expect(states).toHaveLength(0);
    expect(problems).toHaveLength(3);
  });

  it('drops states that have not been applied yet when cancelled', async () => {
    const states: GameState[] = [];
    const receiver = new BroadcastReceiver((state) => states.push(state));
    receiver.setLatencySeconds(2);

    await receiver.handle(encoded);
    receiver.cancel();
    vi.advanceTimersByTime(5000);

    expect(states).toHaveLength(0);
  });
});
