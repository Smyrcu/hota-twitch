export interface OverlayParams {
    /** Poll `dev/state.json` instead of listening to Twitch. */
    readonly mock: boolean;
    /** Draw the zones and a status line for calibration. */
    readonly debug: boolean;
    /** Screenshot to draw the zones over, as a path inside the bundle. */
    readonly background: string | null;
}

/** Only bundled files may be shown; anything with a scheme or a parent segment is rejected. */
export function safeBackground(value: string | null): string | null {
    if (value === null || value === '') return null;
    if (/^[a-z][a-z0-9+.-]*:/i.test(value) || value.startsWith('//') || value.startsWith('/')) return null;
    if (value.split('/').includes('..')) return null;
    return value;
}

export function readParams(search: string): OverlayParams {
    const params = new URLSearchParams(search);
    return {
        mock: params.get('mock') === '1',
        debug: params.get('debug') === '1',
        background: safeBackground(params.get('bg')),
    };
}
