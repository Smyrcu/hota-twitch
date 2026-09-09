/** The subset of the Twitch Extension Helper the pages use. */
export interface TwitchAuth {
    readonly channelId: string;
    readonly clientId: string;
    readonly token: string;
    readonly userId: string;
}

export interface TwitchContext {
    readonly hlsLatencyBroadcaster?: number;
    readonly displayResolution?: string;
}

export type BroadcastListener = (target: string, contentType: string, message: string) => void;

export interface TwitchExt {
    onAuthorized(callback: (auth: TwitchAuth) => void): void;
    onContext(callback: (context: TwitchContext, changed: readonly string[]) => void): void;
    listen(target: string, callback: BroadcastListener): void;
    unlisten(target: string, callback: BroadcastListener): void;
}

/** Null outside Twitch, which is how the mock and debug modes run the pages locally. */
export function twitchExt(): TwitchExt | null {
    const host = globalThis as { Twitch?: { ext?: TwitchExt } };
    return host.Twitch?.ext ?? null;
}

/** `displayResolution` is reported as "WxH". */
export function parseDisplayResolution(value: string | undefined): { width: number; height: number } | null {
    if (value === undefined) return null;
    const match = /^(\d+)x(\d+)$/.exec(value.trim());
    if (match === null) return null;
    const width = Number(match[1]);
    const height = Number(match[2]);
    return width > 0 && height > 0 ? { width, height } : null;
}
