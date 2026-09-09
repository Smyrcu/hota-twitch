export interface OverlayParams {
  /** Poll `dev/state.json` instead of listening to Twitch. */
  readonly mock: boolean;
  /** Draw the zones and a status line for calibration. */
  readonly debug: boolean;
  /** Screenshot to draw the zones over, as a path inside the bundle. */
  readonly background: string | null;
}

/**
 * The value goes into a CSS `url("…")`, so it is matched against an allowlist rather than
 * screened for bad prefixes: a blocklist misses tabs inside a scheme, CSS escapes such as
 * `\68` for `h`, and a closing `")` that opens a second background layer. Only a relative
 * path to a bundled image can pass.
 */
const BUNDLED_IMAGE = /^[A-Za-z0-9_-]+(?:\/[A-Za-z0-9_-]+)*\.(?:png|jpe?g)$/;

export function safeBackground(value: string | null): string | null {
  return value !== null && BUNDLED_IMAGE.test(value) ? value : null;
}

export function readParams(search: string): OverlayParams {
  const params = new URLSearchParams(search);
  return {
    mock: params.get('mock') === '1',
    debug: params.get('debug') === '1',
    background: safeBackground(params.get('bg')),
  };
}
