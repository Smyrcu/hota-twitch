export interface ConnectionStatus {
    readonly connected: boolean;
    readonly text: string;
}

const STALE_AFTER_SECONDS = 60;

/** What the streamer sees about the plugin: connected with an age, or what is still missing. */
export function describeConnection(
    hasToken: boolean,
    lastStateAt: string | null,
    now: number = Date.now(),
): ConnectionStatus {
    if (!hasToken) return { connected: false, text: 'No token yet. Generate one and paste it into hota-twitch.ini.' };
    if (lastStateAt === null) return { connected: false, text: 'Token ready, waiting for the first state from the game.' };

    const at = Date.parse(lastStateAt);
    if (Number.isNaN(at)) return { connected: false, text: 'Token ready, waiting for the first state from the game.' };

    const age = Math.max(0, Math.round((now - at) / 1000));
    if (age > STALE_AFTER_SECONDS) return { connected: false, text: `No data for ${age} s. Is the game running?` };
    return { connected: true, text: `Connected, last state ${age} s ago.` };
}
