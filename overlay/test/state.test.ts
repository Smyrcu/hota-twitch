import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import { decode, decodeJson } from '../src/state/decode.js';

const MOCK = readFileSync(new URL('../dev/state.json', import.meta.url), 'utf8');

function valid(): Record<string, unknown> {
  return JSON.parse(MOCK) as Record<string, unknown>;
}

describe('state decoding', () => {
  it('accepts the document the mock mode serves', () => {
    const result = decodeJson(MOCK);

    expect(result.ok).toBe(true);
    if (!result.ok) return;
    expect(result.state.screen).toBe('adventure');
    expect(result.state.heroes).toHaveLength(4);
    expect(result.state.towns).toHaveLength(3);
    expect(result.state.heroes[0]?.primary).toEqual([26, 25, 25, 26]);
    expect(result.state.towns[0]?.research).toEqual({ level: 4, slot: 1, spell: 21, rolls: 2 });
    expect(result.state.towns[1]?.garrisonHero?.name).toBe('Sandro');
  });

  it('defaults uiScale to 1 when the producer cannot read it', () => {
    const document = valid();
    document['display'] = { width: 1920, height: 1080 };
    const result = decode(document);

    expect(result.ok && result.state.display.uiScale).toBe(1);
  });

  it('rejects another protocol version', () => {
    const document = valid();
    document['v'] = 2;
    const result = decode(document);

    expect(result.ok).toBe(false);
    expect(result.ok || result.reason).toContain('protocol version 1');
  });

  it('rejects a screen the contract does not define', () => {
    const document = valid();
    document['screen'] = 'kingdom';

    expect(decode(document).ok).toBe(false);
  });

  it('rejects malformed documents without throwing', () => {
    const cases: readonly unknown[] = [
      null,
      42,
      [],
      {},
      { ...valid(), heroes: 'none' },
      { ...valid(), date: { day: 1, week: 3 } },
      { ...valid(), display: { width: 0, height: 1440 } },
    ];

    for (const value of cases) expect(decode(value).ok).toBe(false);
  });

  it('rejects army and skill entries of the wrong shape', () => {
    const document = valid();
    const heroes = document['heroes'] as Record<string, unknown>[];
    heroes[0] = { ...heroes[0], army: [[0, 13]] };

    expect(decode(document).ok).toBe(false);
  });

  it('reports the path of the first problem', () => {
    const document = valid();
    document['player'] = { ...(document['player'] as object), heroListTop: 'top' };

    const result = decode(document);
    expect(result.ok || result.reason).toContain('state.player.heroListTop');
  });

  it('rejects text that is not JSON', () => {
    expect(decodeJson('{').ok).toBe(false);
  });
});
