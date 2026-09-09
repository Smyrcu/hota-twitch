import { afterEach, describe, expect, it, vi } from 'vitest';
import { BACKEND_URL, ConfigApi } from '../src/config/api.js';

interface Call {
  readonly url: string;
  readonly method: string;
  readonly authorization: string | undefined;
}

function stubFetch(reply: (call: Call) => Response): Call[] {
  const calls: Call[] = [];
  vi.stubGlobal('fetch', (url: string, init: RequestInit) => {
    const headers = init.headers as Record<string, string>;
    const call = { url, method: init.method ?? 'GET', authorization: headers['Authorization'] };
    calls.push(call);
    return Promise.resolve(reply(call));
  });
  return calls;
}

const json = (body: unknown, status = 200): Response =>
  new Response(JSON.stringify(body), { status, headers: { 'content-type': 'application/json' } });

describe('streamer configuration API', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('reads the channel with the helper JWT as bearer', async () => {
    const calls = stubFetch(() => json({ hasToken: true, tokenHint: 'hts_ab…', lastStateAt: '2026-09-09T22:00:00Z' }));

    const channel = await new ConfigApi('jwt').channel();

    expect(calls[0]).toEqual({
      url: `${BACKEND_URL}/v1/config/channel`,
      method: 'GET',
      authorization: 'Bearer jwt',
    });
    expect(channel).toEqual({ hasToken: true, tokenHint: 'hts_ab…', lastStateAt: '2026-09-09T22:00:00Z' });
  });

  it('fills in the missing fields of a channel answer', async () => {
    stubFetch(() => json({}));

    expect(await new ConfigApi('jwt').channel()).toEqual({
      hasToken: false,
      tokenHint: null,
      lastStateAt: null,
    });
  });

  it('issues a token with POST', async () => {
    const calls = stubFetch(() => json({ token: 'hts_secret' }));

    expect(await new ConfigApi('jwt').issueToken()).toBe('hts_secret');
    expect(calls[0]?.method).toBe('POST');
    expect(calls[0]?.url).toBe(`${BACKEND_URL}/v1/config/token`);
  });

  it('never shows the streamer a token the backend did not send', async () => {
    stubFetch(() => json({}));

    await expect(new ConfigApi('jwt').issueToken()).rejects.toThrow('no token');
  });

  it('revokes with DELETE and accepts an empty 204', async () => {
    const calls = stubFetch(() => new Response(null, { status: 204 }));

    await new ConfigApi('jwt').revokeToken();

    expect(calls[0]?.method).toBe('DELETE');
  });

  it('reports a refused call instead of rendering a broken page', async () => {
    stubFetch(() => json({ error: 'nope' }, 401));

    await expect(new ConfigApi('jwt').channel()).rejects.toThrow('401');
  });
});
