export interface ChannelStatus {
    readonly hasToken: boolean;
    readonly tokenHint: string | null;
    readonly lastStateAt: string | null;
}

export interface IssuedToken {
    readonly token: string;
}

export const BACKEND_URL: string = __BACKEND_URL__;

class ApiError extends Error {}

async function request<T>(url: string, jwt: string, method: string): Promise<T> {
    const response = await fetch(url, {
        method,
        headers: { Authorization: `Bearer ${jwt}` },
    });
    if (!response.ok) throw new ApiError(`${method} ${url} failed with ${response.status}`);
    if (response.status === 204) return undefined as T;
    return (await response.json()) as T;
}

function readStatus(value: unknown): ChannelStatus {
    const raw = (value ?? {}) as Record<string, unknown>;
    return {
        hasToken: raw['hasToken'] === true,
        tokenHint: typeof raw['tokenHint'] === 'string' ? raw['tokenHint'] : null,
        lastStateAt: typeof raw['lastStateAt'] === 'string' ? raw['lastStateAt'] : null,
    };
}

/** The streamer configuration API of docs/protocol.md §4, called with the helper's JWT. */
export class ConfigApi {
    constructor(
        private readonly jwt: string,
        private readonly baseUrl: string = BACKEND_URL,
    ) {}

    async channel(): Promise<ChannelStatus> {
        return readStatus(await request<unknown>(`${this.baseUrl}/v1/config/channel`, this.jwt, 'GET'));
    }

    async issueToken(): Promise<string> {
        const issued = await request<IssuedToken>(`${this.baseUrl}/v1/config/token`, this.jwt, 'POST');
        return issued.token;
    }

    async revokeToken(): Promise<void> {
        await request<void>(`${this.baseUrl}/v1/config/token`, this.jwt, 'DELETE');
    }
}
