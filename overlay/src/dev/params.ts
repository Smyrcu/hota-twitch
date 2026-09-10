export interface OverlayParams {
  /** Poll a hand-written document instead of listening to Twitch. */
  readonly mock: boolean;
  /** Draw the zones and a status line for calibration. */
  readonly debug: boolean;
  /** Screenshot to draw the zones over, as a path inside the bundle. */
  readonly background: string | null;
  /** Which document mock mode polls, as a path inside the bundle. */
  readonly state: string | null;
}

/**
 * A path from the query string is matched against an allowlist rather than screened for bad
 * prefixes: a blocklist misses tabs inside a scheme, CSS escapes such as `\68` for `h`, and a
 * closing `")` that opens a second background layer. Only a relative path to a bundled file of
 * the expected kind can pass.
 */
const bundled = (extensions: string): RegExp =>
  new RegExp(`^[A-Za-z0-9_-]+(?:/[A-Za-z0-9_-]+)*\\.(?:${extensions})$`);

const BUNDLED_IMAGE = bundled('png|jpe?g');
const BUNDLED_STATE = bundled('json');

const match = (value: string | null, pattern: RegExp): string | null =>
  value !== null && pattern.test(value) ? value : null;

export const safeBackground = (value: string | null): string | null => match(value, BUNDLED_IMAGE);

export const safeStateSource = (value: string | null): string | null => match(value, BUNDLED_STATE);

export function readParams(search: string): OverlayParams {
  const params = new URLSearchParams(search);
  return {
    mock: params.get('mock') === '1',
    debug: params.get('debug') === '1',
    background: safeBackground(params.get('bg')),
    state: safeStateSource(params.get('state')),
  };
}
