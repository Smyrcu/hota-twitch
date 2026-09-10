import { readUiScale } from './settings.js';

/** The channel's own settings, kept whether or not a token is bound (docs/protocol.md §4). */
export interface ChannelSettings {
  readonly uiScale: number;
}

export interface ChannelStatus {
  readonly hasToken: boolean;
  readonly tokenHint: string | null;
  readonly lastStateAt: string | null;
  readonly settings: ChannelSettings;
}

export const BACKEND_URL: string = __BACKEND_URL__;

class ApiError extends Error {}

async function send(url: string, jwt: string, method: string, body?: unknown): Promise<Response> {
  const headers: Record<string, string> = { Authorization: `Bearer ${jwt}` };
  if (body !== undefined) headers['Content-Type'] = 'application/json';
  const response = await fetch(url, {
    method,
    headers,
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
  });
  if (!response.ok) throw new ApiError(`${method} ${url} failed with ${response.status}`);
  return response;
}

async function requestJson(url: string, jwt: string, method: string): Promise<unknown> {
  return (await send(url, jwt, method)).json();
}

function readStatus(value: unknown): ChannelStatus {
  const raw = (value ?? {}) as Record<string, unknown>;
  const settings = (raw['settings'] ?? {}) as Record<string, unknown>;
  return {
    hasToken: raw['hasToken'] === true,
    tokenHint: typeof raw['tokenHint'] === 'string' ? raw['tokenHint'] : null,
    lastStateAt: typeof raw['lastStateAt'] === 'string' ? raw['lastStateAt'] : null,
    settings: { uiScale: readUiScale(settings['uiScale']) },
  };
}

/** The streamer configuration API of docs/protocol.md §4, called with the helper's JWT. */
export class ConfigApi {
  constructor(
    private readonly jwt: string,
    private readonly baseUrl: string = BACKEND_URL,
  ) {}

  async channel(): Promise<ChannelStatus> {
    return readStatus(await requestJson(`${this.baseUrl}/v1/config/channel`, this.jwt, 'GET'));
  }

  async issueToken(): Promise<string> {
    const issued = await requestJson(`${this.baseUrl}/v1/config/token`, this.jwt, 'POST');
    const token = (issued as Record<string, unknown> | null)?.['token'];
    if (typeof token !== 'string' || token === '') throw new ApiError('the backend returned no token');
    return token;
  }

  async revokeToken(): Promise<void> {
    await send(`${this.baseUrl}/v1/config/token`, this.jwt, 'DELETE');
  }

  async saveSettings(settings: ChannelSettings): Promise<void> {
    await send(`${this.baseUrl}/v1/config/settings`, this.jwt, 'PUT', { uiScale: settings.uiScale });
  }
}
